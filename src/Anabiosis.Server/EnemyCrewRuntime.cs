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
    // The same body model as a player (Shared/Model/BodyHealth.cs); Health is that body on the old 0..MaxHealth scale, so the code that
    // just wants to hurt, heal or kill keeps working, while shots, blasts and bleeding go through real parts.
    public BodyHealth Body { get; } = new();
    public float Health
    {
        get => Body.Health * MaxHealth / BodyHealth.MaxVitality;
        set => Body.SetHealth(value * BodyHealth.MaxVitality / MaxHealth);
    }
    public bool Alive => !Body.Dead;
    // Unconscious or with a stopped heart: still there, but not walking, working or manning a gun.
    public bool Downed => Body.IsDowned;
    // Gunfire (World.Boarding.cs): fighters wear a vest and a helmet; each defender keeps its own pace of fire,
    // counts the rounds left in its gun, and takes a moment to bring the gun up on a fresh target.
    public ArmorWorn Armor => Spawn.Role == EnemyCrewRole.Fighter ? new ArmorWorn(true, true) : ArmorWorn.None;
    public float WeaponsSkill => WeaponSkills.Starting(Spawn.Role);
    public float FireCooldown { get; set; }
    public float AimSeconds { get; set; }
    public int RoundsLeft { get; set; } = -1;
    public float FirstAidTimer;
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
