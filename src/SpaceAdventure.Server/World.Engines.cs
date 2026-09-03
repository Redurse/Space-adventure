using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Cosmoteer-style marching engine (direct user request - "давай вначале проработаем средний
// двигатель, а потом по его образу сделаем все остальные", ShipEngine.cs's own doc comment). Each
// of the engine's 3 tiles (Control/Bulkhead/Nozzle) has its own hit points, tracked here the same
// "quiet dictionary keyed by id" way World.WallBlocks.cs already tracks wall HP - purely additive,
// so a hull with no Ship.Engines (every hand-authored hull, every custom ship built before this
// existed) is entirely unaffected.
//
// Not yet wired up: no weapon/collision damages these tiles yet, and no repair tool (welder/wrench)
// finds them yet - only the Debug* setters below can move their HP right now. That's the deliberate
// scope of this first pass (the mechanic itself, proven and tested); hooking real damage sources and
// the repair minigame into it is separate follow-up work, same as the Ship Editor placement UI that
// would let a player actually build one of these compartments in the first place.
public sealed partial class World
{
    public const float EnginePartMaxHp = 100f;

    private readonly Dictionary<string, float> _engineControlHp = new();
    private readonly Dictionary<string, float> _engineBulkheadHp = new();
    private readonly Dictionary<string, float> _engineNozzleHp = new();
    // null = follows the live control input (helm throttle for a Marching engine, helm turn for an
    // Rcs one - EffectiveControl below); set the instant Control breaks, to whatever that input was
    // at that exact moment - direct user request ("если на полном ходу сломается 1 часть, то
    // двигатель будет работать в полную мощность, пока не починить 1 тайл"). Cleared back to null
    // the moment Control is repaired back above 0.
    private readonly Dictionary<string, float> _engineFrozenThrottle = new();

    // Called from InitializeShipState (constructor + every hull swap), same convention as
    // InitializeWallBlocks - a bought/starting hull's engines all start at full health.
    private void InitializeEngines()
    {
        _engineControlHp.Clear();
        _engineBulkheadHp.Clear();
        _engineNozzleHp.Clear();
        _engineFrozenThrottle.Clear();
        foreach (var engine in Ship.Engines)
        {
            _engineControlHp[engine.Id] = EnginePartMaxHp;
            _engineBulkheadHp[engine.Id] = EnginePartMaxHp;
            _engineNozzleHp[engine.Id] = EnginePartMaxHp;
        }
    }

    private float EngineControlHp(string id) => _engineControlHp.GetValueOrDefault(id, EnginePartMaxHp);
    private float EngineBulkheadHp(string id) => _engineBulkheadHp.GetValueOrDefault(id, EnginePartMaxHp);
    private float EngineNozzleHp(string id) => _engineNozzleHp.GetValueOrDefault(id, EnginePartMaxHp);

    public bool IsEngineControlBroken(string id) => EngineControlHp(id) <= 0f;
    public bool IsEngineBulkheadBroken(string id) => EngineBulkheadHp(id) <= 0f;
    public bool IsEngineNozzleBroken(string id) => EngineNozzleHp(id) <= 0f;

    // Control breaking seizes the control input at its current value ("двигатель будет работать в
    // полную мощность [или какую он держал], пока не починить") - captured only on the
    // wasBroken=false -> true edge, so repeated damage after it's already broken doesn't keep
    // re-capturing. Looks the engine up by id since DamageEngineControl's own callers (combat,
    // Debug* setters) only ever have the id on hand - same scan-by-id shape World.WallBlocks.cs's
    // own MaxHpForBlockId already uses.
    private void DamageEngineControl(string id, float amount)
    {
        var wasBroken = IsEngineControlBroken(id);
        _engineControlHp[id] = Math.Max(0f, EngineControlHp(id) - amount);
        if (!wasBroken && IsEngineControlBroken(id) && Ship.Engines.FirstOrDefault(e => e.Id == id) is { } engine)
            _engineFrozenThrottle[id] = engine.Role == EngineRole.Rcs ? _helmTurn : _helmThrottle;
    }

    private void RepairEngineControl(string id, float amount)
    {
        _engineControlHp[id] = Math.Min(EnginePartMaxHp, EngineControlHp(id) + amount);
        if (!IsEngineControlBroken(id))
            _engineFrozenThrottle.Remove(id);
    }

    // Holds pressure exactly like a WallBlock while intact ("держит воздух") - World.Atmosphere.cs's
    // own leak sum reads this the same way it reads Ship.WallBlocks.
    private void DamageEngineBulkhead(string id, float amount) =>
        _engineBulkheadHp[id] = Math.Max(0f, EngineBulkheadHp(id) - amount);

    private void RepairEngineBulkhead(string id, float amount) =>
        _engineBulkheadHp[id] = Math.Min(EnginePartMaxHp, EngineBulkheadHp(id) + amount);

    // Kills this engine's own thrust outright ("больше не генерирует тягу"), independent of
    // Control/throttle - checked directly by TotalEngineThrust below.
    private void DamageEngineNozzle(string id, float amount) =>
        _engineNozzleHp[id] = Math.Max(0f, EngineNozzleHp(id) - amount);

    private void RepairEngineNozzle(string id, float amount) =>
        _engineNozzleHp[id] = Math.Min(EnginePartMaxHp, EngineNozzleHp(id) + amount);

    // Test-only precondition setters, same convention as World.WallBlocks.cs's DebugBreachWallBlock -
    // a test that just needs "this specific tile is already broken/repaired" doesn't need to actually
    // simulate combat or a welder to get there.
    public void DebugBreakEngineControl(string engineId) => DamageEngineControl(engineId, EnginePartMaxHp);
    public void DebugRepairEngineControl(string engineId) => RepairEngineControl(engineId, EnginePartMaxHp);
    public void DebugBreachEngineBulkhead(string engineId) => DamageEngineBulkhead(engineId, EnginePartMaxHp);
    public void DebugBreakEngineNozzle(string engineId) => DamageEngineNozzle(engineId, EnginePartMaxHp);

    // This engine's own effective control input right now - frozen at whatever it was the instant
    // Control broke, or the live input otherwise (helm throttle for a Marching engine, helm turn for
    // an Rcs one - direct user request "по его образу сделаем все остальные"). Signed (-1..1, same
    // convention _helmThrottle/_helmTurn themselves already use) so a frozen-while-reversing/turning
    // engine keeps doing that rather than snapping to some default the moment it seizes.
    private float EffectiveControl(ShipEngine engine) =>
        _engineFrozenThrottle.TryGetValue(engine.Id, out var frozen) ? frozen
        : engine.Role == EngineRole.Rcs ? _helmTurn : _helmThrottle;

    // World.ShipField.cs's own thrustBonus - purely additive to the existing flat SystemDevices sum,
    // so a hull with no Marching Ship.Engines fixtures behaves exactly as before. Magnitude only
    // (Math.Abs) - the ship's overall thrust DIRECTION still comes from the single shared
    // ShipNoseDirection*throttle vector (World.ShipField.cs), not modeled per-engine yet; only how
    // HARD each engine is currently allowed to push is individual.
    private float TotalEngineThrust() =>
        Ship.Engines.Where(e => e.Role == EngineRole.Marching && !IsEngineNozzleBroken(e.Id))
            .Sum(e => e.MaxThrust * Math.Abs(EffectiveControl(e)));

    // World.ShipField.cs's own turnBonus - the Rcs mirror of TotalEngineThrust above. Magnitude only,
    // same reasoning: it flat-adds to the yaw-rate constant that _helmTurn's own sign already
    // multiplies, exactly like the old flat TurnBonus device field always did - only WHICH rooms
    // currently contribute is now damage/freeze-aware instead of a constant per hull.
    private float TotalEngineTurn() =>
        Ship.Engines.Where(e => e.Role == EngineRole.Rcs && !IsEngineNozzleBroken(e.Id))
            .Sum(e => e.MaxThrust * Math.Abs(EffectiveControl(e)));

    // A breached Bulkhead leaks exactly like a breached WallBlock - same OxygenLeakPerBreachPerSecond
    // rate, scaled by how damaged it is rather than a flat on/off, read by World.Atmosphere.cs's own
    // room-oxygen step.
    private float TotalEngineLeakInRoom(string roomId) =>
        Ship.Engines.Where(e => e.RoomId == roomId)
            .Sum(e => OxygenLeakPerBreachPerSecond * (1f - EngineBulkheadHp(e.Id) / EnginePartMaxHp));

    // Same ray-sampling shape as World.WallBlocks.cs's own FindAimedWallBlock, generalized over
    // which of an engine's own tile positions is being aimed at (Bulkhead for welding, Nozzle for an
    // EVA repair from outside) - direct user request (Cosmoteer-style marching engines can be
    // welded/cut like any other hull fixture).
    private ShipEngine? FindAimedEngine(Character character, float reachUnits, int samples, float pointRadius, Func<ShipEngine, Vec2> position)
    {
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return null;

        var origin = character.IsOutside ? GetEvaWorldPosition(character) : character.Position;
        var (hullCenter, _) = GetHullLocalBounds();

        for (var i = 1; i <= samples; i++)
        {
            var point = origin + aim * (reachUnits * i / samples);
            var engine = Ship.Engines.FirstOrDefault(e => character.IsOutside
                ? (_shipFieldPosition + RotateLocalToWorld(position(e) - hullCenter, _shipRotationDegrees) - point).Length() <= pointRadius
                : e.RoomId == character.RoomId && (position(e) - point).Length() <= pointRadius);
            if (engine is not null)
                return engine;
        }
        return null;
    }

    private IReadOnlyList<EngineState> CreateEngineStates() =>
        Ship.Engines.Select(e => new EngineState(e.Id, e.X, e.Y, e.Facing,
            EngineControlHp(e.Id), EngineBulkheadHp(e.Id), EngineNozzleHp(e.Id), EnginePartMaxHp,
            IsThrusting: !IsEngineNozzleBroken(e.Id) && Math.Abs(EffectiveControl(e)) > 0.01f)).ToArray();
}
