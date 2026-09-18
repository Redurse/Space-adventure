using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

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
        {
            // Direct user request ("как в Cosmoteer") follow-up: freezes THIS engine's own current
            // activation (RawControl below), not a shared ship-wide throttle/turn number any more -
            // that raw value already accounts for whether this specific engine was actually doing
            // anything useful for the live command, which a bare _helmThrottle/_helmTurn copy no
            // longer means on its own now that engines only fire when their own Facing/position
            // makes them useful for it.
            var (pivot, _) = GetHullLocalBounds();
            _engineFrozenThrottle[id] = RawControl(engine, pivot);
        }
    }

    private void RepairEngineControl(string id, float amount)
    {
        _engineControlHp[id] = Math.Min(EnginePartMaxHp, EngineControlHp(id) + amount);
        if (!IsEngineControlBroken(id))
            _engineFrozenThrottle.Remove(id);
    }

    // Holds pressure exactly like a WallBlock while intact ("держит воздух") - World.Atmosphere.cs's
    // own leak sum reads this the same way it reads Ship.WallBlocks.
    private void DamageEngineBulkhead(string id, float amount)
    {
        _engineBulkheadHp[id] = Math.Max(0f, EngineBulkheadHp(id) - amount);
        // Direct user request ("скрытое число хп" отсека) - same "aggregate of what already breaks
        // today" shape World.WallBlocks.cs's own DamageWallBlock already uses.
        if (Ship.Engines.FirstOrDefault(e => e.Id == id) is { } engine)
            DamageRoom(engine.RoomId, amount);
    }

    private void RepairEngineBulkhead(string id, float amount) =>
        _engineBulkheadHp[id] = Math.Min(EnginePartMaxHp, EngineBulkheadHp(id) + amount);

    // Kills this engine's own thrust outright ("больше не генерирует тягу"), independent of
    // Control/throttle - checked directly by ComputeEngineForces above (IsEngineNozzleBroken skips
    // it entirely) and by TotalEngineLeakInRoom below.
    private void DamageEngineNozzle(string id, float amount)
    {
        _engineNozzleHp[id] = Math.Max(0f, EngineNozzleHp(id) - amount);
        if (Ship.Engines.FirstOrDefault(e => e.Id == id) is { } engine)
            DamageRoom(engine.RoomId, amount);
    }

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
    // Control broke, or the live RawControl otherwise. Signed 0..1 (RawControl's own doc comment -
    // unlike the old shared _helmThrottle/_helmTurn this replaced, a single engine never has a
    // reason to run "in reverse" of its own one-way nozzle) so a frozen engine keeps pushing exactly
    // as hard as it was the moment it seized, rather than snapping to some default.
    private float EffectiveControl(ShipEngine engine, Vec2 pivot) =>
        _engineFrozenThrottle.TryGetValue(engine.Id, out var frozen) ? frozen : RawControl(engine, pivot);

    // Direct user request ("как в Cosmoteer... каждый двигатель включается только когда его
    // направление реально полезно для текущего запрошенного движения, а не всегда от одного общего
    // газа") - replaces the old "every Marching engine fires at the same _helmThrottle, every Rcs one
    // at the same _helmTurn, regardless of which way it actually points" model. A Marching engine
    // only fires when its own push direction (opposite Facing) actually has something in common with
    // the requested ship-local translation (throttle along the nose + strafe along the beam) - a
    // perfectly aligned engine fires at full strength, a diagonal request only partially engages a
    // perpendicular one, and one facing entirely the wrong way doesn't fire at all. An Rcs engine
    // fires only when ITS OWN torque (from its real position and facing, not a shared assumption)
    // would actually spin the hull the requested way - the same "real geometry decides who fires"
    // idea, just for rotation instead of translation.
    private float RawControl(ShipEngine engine, Vec2 pivot) =>
        engine.Role == EngineRole.Rcs ? RcsRawControl(engine, pivot) : MarchingRawControl(engine);

    private float MarchingRawControl(ShipEngine engine)
    {
        var desired = ShipLocalForward * _helmThrottle + ShipLocalRight * _helmStrafe;
        var desiredLength = desired.Length();
        if (desiredLength < 0.0001)
            return 0f;
        var pushDirection = -engine.FacingUnitVector;
        var alignment = (pushDirection.X * desired.X + pushDirection.Y * desired.Y) / desiredLength;
        return alignment > 0f ? (float)(alignment * Math.Min(1.0, desiredLength)) : 0f;
    }

    private float RcsRawControl(ShipEngine engine, Vec2 pivot)
    {
        if (_helmTurn == 0f)
            return 0f;
        var pushDirection = -engine.FacingUnitVector;
        var arm = engine.ControlPosition - pivot;
        var torqueAtFullThrust = arm.X * pushDirection.Y - arm.Y * pushDirection.X;
        return Math.Sign(torqueAtFullThrust) == Math.Sign(_helmTurn) ? Math.Abs(_helmTurn) : 0f;
    }

    // Direct user request ("сделай тягу зависимой от расположения движков") - replaces the old
    // TotalEngineThrust()/TotalEngineTurn() flat-magnitude bonuses, which folded every engine's own
    // push into one shared, nose-aligned scalar regardless of where it actually sat on the hull or
    // which way it actually pointed (World.ShipField.cs's own old doc comment admitted as much: "the
    // ship's overall thrust DIRECTION still comes from the single shared ShipNoseDirection*throttle
    // vector, not modeled per-engine"). Each intact engine (which ones actually fire and how hard is
    // EffectiveControl/RawControl's own call, direction-aware and frozen-throttle-aware) now
    // contributes a real force vector opposite its own Facing (ShipEngine.FacingUnitVector's own doc
    // comment - a real nozzle convention: exhaust goes out Facing, the ship gets pushed the other
    // way), in the same ship-local frame Ship.Rooms/devices already live in. Summed into a net force
    // (added straight onto the ship's existing thrust in World.ShipField.cs) and a net torque around
    // `pivot` (GetHullLocalBounds().Center, the same stand-in "hull centre" turret/camera code
    // already uses - there's no real mass distribution to compute an actual centre of mass from, same
    // honesty ShipCatalog.Mass's own doc comment already admits for the ship's overall mass).
    // A hull with no Ship.Engines fixtures (every hand-authored/pre-existing custom ship) sums an
    // empty sequence - exactly zero force and zero torque, so nothing about its flight changes.
    private (Vec2 NetForce, float NetTorque) ComputeEngineForces(Vec2 pivot)
    {
        var netForce = Vec2.Zero;
        var netTorque = 0.0;
        foreach (var engine in Ship.Engines)
        {
            if (IsEngineNozzleBroken(engine.Id))
                continue;
            var force = -engine.FacingUnitVector * (engine.MaxThrust * EffectiveControl(engine, pivot));
            netForce += force;
            var arm = engine.ControlPosition - pivot;
            netTorque += arm.X * force.Y - arm.Y * force.X;
        }
        return (netForce, (float)netTorque);
    }

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

    private IReadOnlyList<EngineState> CreateEngineStates()
    {
        var (pivot, _) = GetHullLocalBounds();
        return Ship.Engines.Select(e => new EngineState(e.Id, e.X, e.Y, e.Facing,
            EngineControlHp(e.Id), EngineBulkheadHp(e.Id), EngineNozzleHp(e.Id), EnginePartMaxHp,
            IsThrusting: !IsEngineNozzleBroken(e.Id) && Math.Abs(EffectiveControl(e, pivot)) > 0.01f)).ToArray();
    }
}
