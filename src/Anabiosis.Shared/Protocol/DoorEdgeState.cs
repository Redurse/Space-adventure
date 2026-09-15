namespace Anabiosis.Shared.Protocol;

// M-doors-as-edges (humble-soaring-cat.md) - mirrors DoorState (own doc comment) for the new narrow-
// door-as-edge-between-2-tiles primitive. A separate list/id space rather than folded into
// DoorState/Doors: an edge door has no width/height/footprint at all (Ship.DoorEdges' own doc
// comment), so it carries no geometry here either - ClientTileGrid.ApplyDoorEdges looks the actual
// Coord/Side up from Ship.DoorEdges (networked separately, WorldSnapshot.cs), keyed by the same Id.
public sealed record DoorEdgeState(string Id, bool IsOpen, float Hp, float MaxHp, float RepairProgress = 0f)
{
    public bool Destroyed => Hp <= 0f;
    public float Fraction => MaxHp > 0f ? Hp / MaxHp : 0f;
}
