using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// An airlock that is a plain door placed onto open space (the editor's "Шлюз" tool - a Ship.DoorEdges entry with one room side missing) rather than a
// rectangular vacuum door (Ship.VacuumDoors). The ships built in the editor, the built-in "КРУТОЙ КОРАБЛЬ" among them, have only this kind, and the
// crossing code (World.Eva.cs) used to know only the other: an open airlock of that kind vented the room but a crew member could not walk out of it.
public sealed partial class World
{
    // The seam of such a door in the hull's own frame: its middle, and the direction that leads out of the ship through it.
    private readonly record struct VacuumEdge(string Id, string RoomId, Vec2 Center, Vec2 Outward)
    {
        // Where a point lies relative to the seam: how far out past it (negative = still inside), and how far along it from the middle.
        public (float Past, float Along) Locate(Vec2 point)
        {
            var offset = point - Center;
            var past = (float)(offset.X * Outward.X + offset.Y * Outward.Y);
            var along = (float)(offset.X * -Outward.Y + offset.Y * Outward.X);
            return (past, along);
        }
    }

    private IEnumerable<VacuumEdge> VacuumDoorEdges()
    {
        foreach (var edge in Ship.DoorEdges)
        {
            if (edge.RoomAId is not null && edge.RoomBId is not null)
                continue;
            var roomId = edge.RoomAId ?? edge.RoomBId;
            if (roomId is null)
                continue;
            // The edge is stored on the East or South side of its first tile; with the room on the far side the way out is the other way.
            var sign = edge.RoomAId is null ? -1.0 : 1.0;
            var outward = edge.Side switch
            {
                TileSide.East => new Vec2(sign, 0),
                TileSide.West => new Vec2(-sign, 0),
                TileSide.South => new Vec2(0, sign),
                _ => new Vec2(0, -sign),
            };
            yield return new VacuumEdge(edge.Id, roomId, DoorEdgeSeamCenter(edge), outward);
        }
    }

    // While docked the first airlock is the connector to the station and leads nowhere into space (World.StationDocking.cs's ResolveShipAirlock).
    private string? DockedConnectorEdgeId() =>
        IsDocked && Ship.VacuumDoors.Count == 0 ? ResolveShipAirlock()?.Id : null;

    private bool IsOpenVacuumEdgeOf(VacuumEdge edge, string roomId) => edge.RoomId == roomId && IsDoorOpen(edge.Id);

    // The open airlock-edge a character walking out of `roomId` is about to cross: standing at its seam (the tile collision stops a body one
    // clearance short of the plane) and moving outward through it.
    private VacuumEdge? FindVacuumEdgeExit(Character character, Vec2 next, Vec2 moveDelta)
    {
        var connectorId = DockedConnectorEdgeId();
        foreach (var edge in VacuumDoorEdges())
        {
            if (edge.Id == connectorId || !IsOpenVacuumEdgeOf(edge, character.RoomId))
                continue;
            var (past, along) = edge.Locate(next);
            var heading = moveDelta.X * edge.Outward.X + moveDelta.Y * edge.Outward.Y;
            if (heading > 0 && MathF.Abs(along) <= 0.6f && past >= -(HullWalkClearance + 0.15f) && past <= 0.6f)
                return edge;
        }
        return null;
    }

    // Standing outside against an open airlock-edge and stepping toward the hull: the way back in.
    private VacuumEdge? FindVacuumEdgeEntry(Vec2 hullLocalPoint)
    {
        foreach (var edge in VacuumDoorEdges())
        {
            if (!IsDoorOpen(edge.Id))
                continue;
            var (past, along) = edge.Locate(hullLocalPoint);
            if (MathF.Abs(along) <= 0.6f && past >= -0.5f && past <= HullWalkClearance + 0.6f)
                return edge;
        }
        return null;
    }

    // Just inside the door, in the tile it opens from, at the same distance along the seam as the character came in at.
    private static Vec2 InsideAirlockEdge(VacuumEdge edge, Vec2 hullLocalPoint)
    {
        var (_, along) = edge.Locate(hullLocalPoint);
        var tangent = new Vec2(-edge.Outward.Y, edge.Outward.X);
        return edge.Center - edge.Outward * 0.6f + tangent * along;
    }

    // Just outside the door, flush against the plating, at the same distance along the seam as the character crossed at.
    private static Vec2 OutsideAirlockEdge(VacuumEdge edge, Vec2 hullLocalPoint, float clearance)
    {
        var (_, along) = edge.Locate(hullLocalPoint);
        var tangent = new Vec2(-edge.Outward.Y, edge.Outward.X);
        return edge.Center + edge.Outward * clearance + tangent * along;
    }

    // Flying in on the jetpack: an open airlock (either kind) lets a drifting crew member straight in instead of leaving them to bounce off the
    // plating beside it - and, once the hull's own protection from a body that has just pushed off it applies, to sail on through the ship. Walking
    // in on magnetic boots was the only way back before (StepMagnetizedWalk); that is still how a hull is climbed, this is for a door met in flight.
    private bool TryEnterOpenAirlockWhileDrifting(Character character, Vec2 from, Vec2 to)
    {
        if (Ship.VacuumDoors.Count == 0 && !Ship.DoorEdges.Any(e => e.RoomAId is null || e.RoomBId is null))
            return false;
        // A deliberate push-off from the hull is a decision to leave it: that flight does not turn round at the door it started from.
        if (character.PushedOffFrom == PushOffOrigin.Ship)
            return false;
        var (hullCenter, _) = GetHullLocalBounds();
        Vec2 ToHullLocal(Vec2 world) => hullCenter + RotateWorldToLocal(world - _shipFieldPosition, _shipRotationDegrees);
        var travelled = (float)(to - from).Length();
        var samples = Math.Max(1, (int)MathF.Ceiling(travelled / 0.2f));
        var heading = ToHullLocal(to) - ToHullLocal(from);
        var connectorId = DockedConnectorEdgeId();

        for (var i = 1; i <= samples; i++)
        {
            var local = ToHullLocal(from + (to - from) * (i / (float)samples));

            foreach (var edge in VacuumDoorEdges())
            {
                if (edge.Id == connectorId || !IsDoorOpen(edge.Id))
                    continue;
                var (past, along) = edge.Locate(local);
                var inward = heading.X * edge.Outward.X + heading.Y * edge.Outward.Y < 0;
                if (inward && MathF.Abs(along) <= 0.5f && past >= -0.2f && past <= HullWalkClearance + 0.6f)
                {
                    Enter(character, edge.RoomId, InsideAirlockEdge(edge, local));
                    return true;
                }
            }

            foreach (var door in Ship.VacuumDoors)
            {
                if (!IsDoorOpen(door.Id) || !door.Contains(local))
                    continue;
                var towardHull = hullCenter - door.Position;
                if (heading.X * towardHull.X + heading.Y * towardHull.Y <= 0)
                    continue; // flying out through it, not in
                Enter(character, door.RoomAId, local + towardHull.Normalized() * EvaEntryNudge);
                return true;
            }
        }
        return false;
    }

    // Test hook: sets how fast an outside character is drifting (world frame), without the suit and tank a jetpack burn would need.
    public void DebugSetEvaVelocity(int playerId, Vec2 velocity) => _characters[playerId].EvaVelocity = velocity;

    private static void Enter(Character character, string roomId, Vec2 position)
    {
        character.IsOutside = false;
        character.RoomId = roomId;
        character.Position = position;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaLocalOffset = Vec2.Zero;
        character.EvaVelocity = Vec2.Zero;
    }}
