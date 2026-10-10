using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// What is inside a hostile hull as a living place: the people walking it and the air in every compartment. Both are
// per ship (a squadron has several hulls, each with its own crew and its own vented rooms), not world-level state.
public sealed partial class EnemyShipRuntime
{
    internal List<EnemyCrewRuntime> Crew { get; } = new();
    // Air in each compartment of this hull, 0..World.FullOxygen.
    internal Dictionary<string, float> RoomOxygen { get; } = new();

    private void InitializeInterior()
    {
        foreach (var spawn in Layout.CrewSpawns)
            Crew.Add(new EnemyCrewRuntime(spawn));
        foreach (var room in Layout.Rooms)
            RoomOxygen[room.Id] = World.FullOxygenLevel;
    }
}
