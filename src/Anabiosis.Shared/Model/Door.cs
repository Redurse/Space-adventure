namespace Anabiosis.Shared.Model;

// A gap in the wall shared by two rooms: a small rectangle straddling the wall (X/Y is its
// center, Width/Height its extent, same convention as Room). A point inside it counts as a
// legal crossing between RoomAId and RoomBId; everywhere else on a shared wall is solid.
// Vertical (humble-soaring-cat.md "Дверь как устройство со своим footprint'ом") - true when the
// shared wall this door sits on is a vertical LINE (rooms side by side along X, Width==1 along
// that thickness axis), same meaning ShipLayoutGeometry.RoomPairOverlap.Vertical/CustomDoorDef.
// Vertical already use. Optional and defaulted so every existing hand-authored `new Door(...)`
// call site (every fixed hull, EnemyShipLayout, Station) keeps compiling unchanged - IsVertical's
// own Width<=Height fallback is always correct for THOSE (never both 1: a door there is always
// StandardSpanUnits-wide on its span axis), so only Ship.Custom.cs's BuildDoors (the one place a
// genuinely 1x1 narrow custom door can exist, where Width==Height==1 is truly ambiguous) needs to
// pass it explicitly.
public sealed record Door(string Id, string RoomAId, string RoomBId, float X, float Y, float Width, float Height, bool? Vertical = null)
{
    // Every door's along-the-wall span, in world units - exactly 2 of GenerateOuterWallBlocks'/
    // BuildWallBlocks' own 1-unit tiles, so a door replaces precisely the 2 wall blocks its
    // footprint would otherwise land on (Ship's own constructor drops any block a door Contains)
    // with no sliver of untextured gap or leftover block on either side. Also used by
    // AirlockOuterDoor and by Ship.Custom.cs's dynamically-sized doors/airlocks.
    public const float StandardSpanUnits = 2f;

    public float Left => X - Width / 2;
    public float Right => X + Width / 2;
    public float Top => Y - Height / 2;
    public float Bottom => Y + Height / 2;
    public Vec2 Position => new(X, Y);

    public bool Contains(Vec2 p) => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

    public string OtherRoom(string roomId) => roomId == RoomAId ? RoomBId : RoomAId;
    public bool Connects(string roomId) => roomId == RoomAId || roomId == RoomBId;

    public bool IsVertical => Vertical ?? Width <= Height;

    // The door's own footprint, as a genuine multi-tile object (direct user request - "устройство
    // 1 на 2 тайла"/"устройство 2 на 2 тайла", the strip being ONLY the thin barrier - this
    // record's own Left/Top/Width/Height above, unchanged, still the sole thing collision/
    // atmosphere ever reads) - same center, but the THICKNESS (perpendicular) axis widened from 1
    // tile to 2 (one tile into each room), the SPAN axis (1 for narrow, StandardSpanUnits for
    // wide) left exactly as-is. Purely a rendering/interaction concept layered on top - never
    // consulted by TileGrid.IsWalkable/IsBlockingForRegion or any server mechanic.
    public (float Left, float Top, float Width, float Height) FootprintRect() => IsVertical
        ? (X - 1f, Y - Height / 2f, 2f, Height)
        : (X - Width / 2f, Y - 1f, Width, 2f);
}

// M-doors-as-edges (humble-soaring-cat.md) - the narrow-door-as-a-barrier-between-2-tiles
// primitive, at the Ship level. Unlike Door above, this carries no width/height/footprint at all -
// its only geometry is the TileGrid edge it sits on (Coord/Side, TileGrid.CanonicalEdgeKey's own
// convention: Side is always East or South). RoomAId/RoomBId are looked up once at construction
// time from the two flanking tiles (Ship.Custom.cs), the same way every other device's RoomId is
// derived from Room.Contains - never recomputed later, since a room's own Rects never change after
// a Ship is built.
public sealed record ShipDoorEdge(string Id, string RoomAId, string RoomBId, TileCoord Coord, TileSide Side);
