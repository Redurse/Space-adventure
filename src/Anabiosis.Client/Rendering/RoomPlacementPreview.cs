using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Content-каталог отсеков - click-to-place UI's own client-side mirror of World.ShipBuilding.cs's
// FindAutoPlacement/AppendRoomIfValid: NOT the authoritative check (the server always re-validates
// the exact position TryBuildRoom actually receives, per its own doc comment - "never trust the
// client's own math"), just enough geometry to give the player instant, live visual feedback while
// they're pointing at their own ship, using ONLY what the snapshot already carries (Rooms,
// PendingRoomBuilds) - no server round trip needed to see which tiles are currently valid.
//
// Deliberately skips CustomShipValidator's own airlock-adjacency rule (an existing room's airlock
// can't now border the new room) - reconstructing which side of which room has an airlock from the
// snapshot's own AirlockOuterDoor positions is real work for a rare edge case, and the server
// silently refusing that one specific spot (same "no charge, nothing happens" outcome every other
// refused build already has) is an acceptable gap for a purely cosmetic preview.
public static class RoomPlacementPreview
{
    public readonly record struct Candidate(float X, float Y, float Width, float Height);

    public static IReadOnlyList<Candidate> FindCandidates(WorldSnapshot snapshot, RoomCatalogEntry entry)
    {
        var existingRooms = snapshot.Rooms.Select(r => new CustomRoomDef(r.Id, r.Name, r.X, r.Y, r.Width, r.Height)).ToList();
        var occupied = existingRooms.Concat((snapshot.PendingRoomBuilds ?? System.Array.Empty<PendingRoomBuildState>())
            .Select(p => new CustomRoomDef(p.Id, p.Name, p.X, p.Y, p.Width, p.Height))).ToList();
        return FindCandidates(existingRooms, occupied, entry);
    }

    // Редактор корабля в духе Cosmoteer (humble-soaring-cat.md, Step 3) - the offline Ship Editor has
    // no WorldSnapshot/PendingRoomBuilds at all, only its own local room list, and for it every
    // already-placed room is simultaneously the anchor set AND the occupied set (nothing is "still
    // under construction" there - a placed module is placed).
    public static IReadOnlyList<Candidate> FindCandidates(IReadOnlyList<CustomRoomDef> rooms, RoomCatalogEntry entry) =>
        FindCandidates(rooms, rooms, entry);

    // Core geometry, unchanged from the original WorldSnapshot-only version - anchorRooms is what a
    // candidate must touch the edge of (a floating, disconnected compartment isn't valid), occupiedRooms
    // is what a candidate must not overlap (anchor rooms plus anything else that already claims the
    // footprint, e.g. a build still in progress).
    private static IReadOnlyList<Candidate> FindCandidates(IReadOnlyList<CustomRoomDef> anchorRooms, IReadOnlyList<CustomRoomDef> occupiedRooms, RoomCatalogEntry entry)
    {
        var results = new List<Candidate>();
        var previewIndex = 0;
        foreach (var anchor in anchorRooms)
        {
            var attempts = new (float X, float Y)[]
            {
                (anchor.X, anchor.Y + anchor.Height),
                (anchor.X + anchor.Width, anchor.Y),
                (anchor.X, anchor.Y - entry.Height),
                (anchor.X - entry.Width, anchor.Y),
            };
            foreach (var (x, y) in attempts)
            {
                var attempt = new CustomRoomDef($"preview-{previewIndex++}", entry.Name, x, y, entry.Width, entry.Height);
                if (occupiedRooms.Any(r => Overlaps(r, attempt)))
                    continue;

                var withAttempt = anchorRooms.Append(attempt).ToList();
                var overlaps = ShipLayoutGeometry.FindRoomPairOverlaps(withAttempt);
                if (!overlaps.Any(o => o.RoomAId == attempt.Id || o.RoomBId == attempt.Id))
                    continue; // doesn't actually touch anything - a floating compartment isn't valid

                results.Add(new Candidate(x, y, entry.Width, entry.Height));
            }
        }
        return results;
    }

    // Whichever candidate's own centre sits closest to the cursor - null only when there's nowhere
    // at all to attach right now (every side of every room is already occupied or blocked).
    public static Candidate? NearestTo(IReadOnlyList<Candidate> candidates, Vec2 mouseLocal)
    {
        Candidate? best = null;
        var bestDistance = double.MaxValue;
        foreach (var candidate in candidates)
        {
            var center = new Vec2(candidate.X + candidate.Width / 2f, candidate.Y + candidate.Height / 2f);
            var distance = (center - mouseLocal).Length();
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = candidate;
        }
        return best;
    }

    private static bool Overlaps(CustomRoomDef a, CustomRoomDef b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}
