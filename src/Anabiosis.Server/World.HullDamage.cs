namespace Anabiosis.Server;

// Carries the hull's damage across a rebuild that goes through InitializeShipState (which resets everything to
// full health): the structural change that makes it run - a compartment holding a device being lost - must
// not heal the walls, doors, engines and air of every compartment that is still there. Only entries for ids
// that exist in the new ship are restored; the rest simply start fresh, as before.
public sealed partial class World
{
    private sealed record HullDamage(
        Dictionary<string, float> WallBlockHp,
        Dictionary<string, float> DoorHp,
        Dictionary<string, bool> DoorOpen,
        Dictionary<string, float> DoorEdgeHp,
        Dictionary<string, bool> DoorEdgeOpen,
        Dictionary<string, float> RoomOxygen,
        Dictionary<string, float> RoomExtraHp,
        Dictionary<string, float> EngineControlHp,
        Dictionary<string, float> EngineBulkheadHp,
        Dictionary<string, float> EngineNozzleHp);

    private HullDamage CaptureHullDamage() => new(
        new(_wallBlockHp), new(_doorHp), new(_doorOpen), new(_doorEdgeHp), new(_doorEdgeOpen),
        new(_roomOxygen), new(_roomHp), new(_engineControlHp), new(_engineBulkheadHp), new(_engineNozzleHp));

    private void RestoreHullDamage(HullDamage carried)
    {
        foreach (var block in Ship.WallBlocks)
            if (carried.WallBlockHp.TryGetValue(block.Id, out var hp))
                _wallBlockHp[block.Id] = hp;
        foreach (var door in Ship.Doors)
        {
            if (carried.DoorHp.TryGetValue(door.Id, out var hp))
                _doorHp[door.Id] = hp;
            if (carried.DoorOpen.TryGetValue(door.Id, out var open))
                _doorOpen[door.Id] = open;
        }
        foreach (var edge in Ship.DoorEdges)
        {
            if (carried.DoorEdgeHp.TryGetValue(edge.Id, out var hp))
                _doorEdgeHp[edge.Id] = hp;
            if (carried.DoorEdgeOpen.TryGetValue(edge.Id, out var open))
                _doorEdgeOpen[edge.Id] = open;
        }
        foreach (var room in Ship.Rooms)
        {
            if (carried.RoomOxygen.TryGetValue(room.Id, out var oxygen))
                _roomOxygen[room.Id] = oxygen;
            if (carried.RoomExtraHp.TryGetValue(room.Id, out var extra))
                _roomHp[room.Id] = extra;
        }
        foreach (var engine in Ship.Engines)
        {
            if (carried.EngineControlHp.TryGetValue(engine.Id, out var control))
                _engineControlHp[engine.Id] = control;
            if (carried.EngineBulkheadHp.TryGetValue(engine.Id, out var bulkhead))
                _engineBulkheadHp[engine.Id] = bulkhead;
            if (carried.EngineNozzleHp.TryGetValue(engine.Id, out var nozzle))
                _engineNozzleHp[engine.Id] = nozzle;
        }
    }
}
