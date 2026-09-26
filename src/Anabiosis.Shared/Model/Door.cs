namespace Anabiosis.Shared.Model;

// A gap in the wall shared by two rooms (or, when RoomBId is null, shared by one room and open
// space - see LeadsToVacuum below): a small rectangle straddling the wall (X/Y is its center,
// Width/Height its extent, same convention as Room). A point inside it counts as a legal crossing
// between RoomAId and RoomBId; everywhere else on a shared wall is solid.
//
// RoomBId nullable (humble-soaring-cat.md "убрать AirlockOuterDoor как отдельный тип" - direct
// user request, "это просто дверь блокирующая выход воздуха и все") - a door that used to need a
// wholly separate sibling type (the deleted AirlockOuterDoor) just to represent "leads to
// nowhere" now does it the same way ShipDoorEdge already proved out below: a null room id, not a
// different type. Unlike ShipDoorEdge, only RoomBId (never RoomAId) is nullable here - a rect
// Door has no external constraint on which side is "first" (every producer picks the order
// freely, unlike an edge's Coord/Side-derived A/B), so normalizing "A is always the real room"
// is strictly cheaper and kills a whole class of null-forgiving `!` at call sites that used to
// read AirlockOuterDoor.RoomId.
//
// Vertical (humble-soaring-cat.md "Дверь как устройство со своим footprint'ом") - true when the
// shared wall this door sits on is a vertical LINE (rooms side by side along X, Width==1 along
// that thickness axis), same meaning ShipLayoutGeometry.RoomPairOverlap.Vertical/CustomDoorDef.
// Vertical already use. Optional and defaulted so every existing hand-authored `new Door(...)`
// call site (every fixed hull, EnemyShipLayout, Station) keeps compiling unchanged - IsVertical's
// own Width<=Height fallback is always correct for THOSE (never both 1: a door there is always
// StandardSpanUnits-wide on its span axis), so only Ship.Custom.cs's BuildDoors/BuildHullDoors
// (the one place a genuinely 1x1 narrow custom door can exist, where Width==Height==1 is truly
// ambiguous) needs to pass it explicitly.
public sealed record Door(string Id, string RoomAId, string? RoomBId, float X, float Y, float Width, float Height, bool? Vertical = null)
{
    // Every door's along-the-wall span, in world units - exactly 2 of GenerateOuterWallBlocks'/
    // BuildWallBlocks' own 1-unit tiles, so a door replaces precisely the 2 wall blocks its
    // footprint would otherwise land on (Ship's own constructor drops any block a door Contains)
    // with no sliver of untextured gap or leftover block on either side. Also used by
    // Ship.Custom.cs's dynamically-sized doors/hull doors.
    public const float StandardSpanUnits = 2f;

    public float Left => X - Width / 2;
    public float Right => X + Width / 2;
    public float Top => Y - Height / 2;
    public float Bottom => Y + Height / 2;
    public Vec2 Position => new(X, Y);

    public bool Contains(Vec2 p) => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

    // True for what used to be a separate AirlockOuterDoor - this side has no room, so crossing
    // through here (when open) means stepping into the AsteroidField, not another compartment.
    // World.Eva.cs's crossing/walk logic must only ever consult Ship.VacuumDoors (the doors where
    // this is true), never the full Doors list, or a crew member could step into space through an
    // ordinary interior doorway.
    public bool LeadsToVacuum => RoomBId is null;

    public string? OtherRoom(string roomId) => roomId == RoomAId ? RoomBId : RoomAId;
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
//
// Direct user request ("сделай возможным поставить дверь если 1 клетка это пол а вторая космос...
// и в таком случае дверь будет считаться шлюзом") - either id can be null when that flanking tile
// isn't part of any room at all (TileGrid.CanPlaceDoorEdge now allows one side to be genuinely open
// space, not just another room's floor). This was the first place in the codebase to model "leads
// to vacuum" as a nullable room id rather than a separate type - Door above later adopted the same
// idea for RoomBId, which is what let the old AirlockOuterDoor sibling type go away entirely.
// BOTH ids are nullable here (unlike Door's just-RoomBId) because an edge's A/B assignment is
// externally forced by TileGrid.CanonicalEdgeKey (A is always the tile at Coord, B the neighbour
// across Side) - which of the two happens to be vacuum isn't this type's choice to normalize away.
public sealed record ShipDoorEdge(string Id, string? RoomAId, string? RoomBId, TileCoord Coord, TileSide Side);
