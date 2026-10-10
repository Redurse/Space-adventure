using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// One person aboard a hostile ship: who they are (Spawn - role, weapon, starting post), how healthy they are, and where
// they are right now. They walk the hull on tiles (World.EnemyCrew.cs) rather than standing where they were placed.
internal sealed class EnemyCrewRuntime
{
    public const float MaxHealth = 60f;

    public EnemyCrewSpawn Spawn { get; }
    // The gun this person mans (turret id on the CURRENT hull - it is re-pointed when the hull is rebuilt).
    public string? PostId { get; set; }
    public float Health { get; set; } = MaxHealth;
    public bool Alive => Health > 0;
    public Vec2 Position { get; set; }
    public string RoomId { get; set; }
    // Tiles still to walk through to reach GoalTile (centre to centre), nearest first.
    public List<TileCoord> Path { get; } = new();
    public TileCoord? GoalTile { get; set; }
    // Counts down between re-deciding what to do, so the A* isn't rerun every tick.
    public float DecisionCooldown { get; set; }
    // What the person is busy doing right now, read by the turret gate (manned or not) and by tests.
    public EnemyCrewActivity Activity { get; set; } = EnemyCrewActivity.AtPost;
    // The repair an engineer is on right now.
    internal World.EnemyRepairJob? Job { get; set; }

    public EnemyCrewRuntime(EnemyCrewSpawn spawn)
    {
        Spawn = spawn;
        PostId = spawn.PostId;
        Position = spawn.Position;
        RoomId = spawn.RoomId;
    }
}

internal enum EnemyCrewActivity
{
    AtPost,
    Walking,
    Fighting,
    Repairing,
    Healing,
    Fleeing,
}
