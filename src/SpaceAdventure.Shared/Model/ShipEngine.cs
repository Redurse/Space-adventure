namespace SpaceAdventure.Shared.Model;

// A marching engine, modeled after Cosmoteer's own directional thrusters (direct user request -
// "давай вначале проработаем средний двигатель, а потом по его образу сделаем все остальные").
// Unlike every other fixture in the game, this is a genuine 3-tile-long device laid out in a
// straight line along Facing, each tile with its own independent damage state (World.Engines.cs):
//   1. Control  - ordinary interior floor, inside the engine's own compartment. The crew's throttle
//      lever physically lives here; breaking it doesn't stop the engine, it just seizes whatever
//      power level was already set (World.Engines.cs's frozen-throttle mechanic).
//   2. Bulkhead - directly outward from Control, standing where a hull wall would otherwise be. It
//      holds pressure exactly like a WallBlock while intact and starts leaking the instant it's
//      breached - the engine's own housing IS the hull plating at that point.
//   3. Nozzle   - one tile further out, in open space - the only part that's actually outside the
//      hull. Breaching it kills the engine's own thrust outright, independent of Control/throttle.
// MaxThrust is this one engine's own full-power contribution - added to the ship's existing flat
// thrust budget for a Marching engine, or to its flat TURN budget for an Rcs one (World.ShipField.cs
// still only ever adds ONE of the two per engine, mirroring the old ThrustBonus/TurnBonus split that
// never set both on the same fixture) - purely additive either way, so a hull with none behaves
// exactly as before.
public enum EngineRole { Marching, Rcs }

public sealed record ShipEngine(string Id, string RoomId, float X, float Y, TileSide Facing, float MaxThrust, EngineRole Role = EngineRole.Marching)
{
    public Vec2 ControlPosition => new(X, Y);
    public Vec2 BulkheadPosition => ControlPosition + Step(Facing);
    public Vec2 NozzlePosition => ControlPosition + Step(Facing) * 2;

    private static Vec2 Step(TileSide side) => side switch
    {
        TileSide.North => new Vec2(0, -1),
        TileSide.South => new Vec2(0, 1),
        TileSide.East => new Vec2(1, 0),
        TileSide.West => new Vec2(-1, 0),
        _ => Vec2.Zero,
    };
}

// The Ship Editor's own placement record (parallels CustomDeviceDef) - X/Y is the Control tile's
// own position, the same "anchor tile" convention every other multi-tile fixture (the Reactor's
// own 4x4 footprint) already uses.
public sealed record CustomEngineDef(float X, float Y, TileSide Facing, float MaxThrust, EngineRole Role = EngineRole.Marching);
