using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Each hull wall block's own hit points - a quiet number invisible to the player until they weld
// or cut one, the same "revealed on demand" shape OreDeposit's Hp already has (World.Cutting.cs).
// Replaces the old binary breached/not-breached flag: a wall now takes graduated damage (enemy
// fire and asteroid strikes still knock a block straight to 0, same as before; a cutter works one
// down over a few seconds instead) and leaks air in proportion to how hurt it actually is, and
// welding repairs it the same gradual way rather than un-breaching it in one instant touch.
public sealed partial class World
{
    public const float WallBlockMaxHp = 100f;
    private const float WelderRepairPerSecond = 30f; // a fully broken block takes a bit over 3s to patch
    private const float WallCutDamagePerSecond = 34f; // matches the ore cutter's own rate
    private const float WallCutReachUnits = 1.7f;
    private const int WallCutSamples = 6;
    private const float WallCutPointRadius = 0.85f;
    // Two adjacent fully-broken blocks side by side (WallBlock's own generation tiles each wall in
    // exact 1-unit steps - Ship.cs's GenerateOuterWallBlocks) - wide enough a suited crewman can
    // actually fit through, unlike a single 1-unit hole that's a leak to see through, not a way out.
    private const float PassableBreachAdjacency = 1.05f;

    private readonly Dictionary<string, float> _wallBlockHp = new();

    // Called from InitializeShipState (constructor + every hull swap) - a bought hull starts every
    // block at full health, same as a starting one.
    private void InitializeWallBlocks()
    {
        _wallBlockHp.Clear();
        foreach (var block in Ship.WallBlocks)
            _wallBlockHp[block.Id] = WallBlockMaxHp;
    }

    private float WallBlockHp(string blockId) => _wallBlockHp.GetValueOrDefault(blockId, WallBlockMaxHp);

    public bool IsWallBlockBreached(string blockId) => WallBlockHp(blockId) <= 0f;

    private void DamageWallBlock(string blockId, float amount) =>
        _wallBlockHp[blockId] = Math.Max(0f, WallBlockHp(blockId) - amount);

    private void RepairWallBlock(string blockId, float amount) =>
        _wallBlockHp[blockId] = Math.Min(WallBlockMaxHp, WallBlockHp(blockId) + amount);

    // Test-only direct breach, same convention as World.ShipField.cs's DebugPlaceShip - a
    // precondition setter, not a gameplay action. Enemy fire aims at fixed priority targets now
    // (World.EnemyFleet.cs's EnemyTargetPriority) rather than a uniform random hit anywhere on the
    // hull, so waiting for combat to eventually breach some arbitrary room by chance is no longer a
    // reliable way for a test to set one up - this lets a test that just needs "there's a breach
    // here" say so directly instead.
    public void DebugBreachWallBlock(string roomId)
    {
        var block = Ship.WallBlocks.FirstOrDefault(b => b.RoomId == roomId);
        if (block is not null)
            DamageWallBlock(block.Id, WallBlockMaxHp);
    }

    // A hole wide enough to fit through, not just a pinhole to see space through: this block AND
    // at least one other fully-broken block right beside it on the same wall.
    private bool IsPassableBreach(WallBlock block) =>
        IsWallBlockBreached(block.Id) &&
        Ship.WallBlocks.Any(other => other.Id != block.Id && other.RoomId == block.RoomId &&
            IsWallBlockBreached(other.Id) && (other.Position - block.Position).Length() <= PassableBreachAdjacency);

    private IReadOnlyList<WallBlockState> CreateWallBlockStates() =>
        Ship.WallBlocks.Select(b => new WallBlockState(b.Id, WallBlockHp(b.Id), WallBlockMaxHp)).ToArray();

    // A station is never actually breachable (FindAimedStationWallBlock below is used only for the
    // target-id the client shows a bar over, never by StepWelding/StepCutting/DamageWallBlock/
    // RepairWallBlock) - so unlike _wallBlockHp there's no dictionary to look up, every block is
    // simply always reported at full health.
    private IReadOnlyList<WallBlockState> CreateStationWallBlockStates() =>
        Station.WallBlocks.Select(b => new WallBlockState(b.Id, WallBlockMaxHp, WallBlockMaxHp)).ToArray();

    // Shared by the welder and the indoor cutter (both burn a short aimed flame against the ship's
    // own wall blocks, just to opposite effect) and by the snapshot query that tells the client
    // which block to show the health bar over - one sampling routine so all three can never
    // disagree about what's actually under the flame. Indoors the flame and the blocks it's aimed
    // at share the ship's own interior coordinate space; outside, the character is tracked in
    // world/field space (GetEvaWorldPosition) while WallBlock.Position is still in that same
    // interior frame, so the block's position has to be rotated and translated out to world space
    // the same way the hull plating itself is drawn out there (World.Eva.cs) before the two can be
    // compared.
    private WallBlock? FindAimedWallBlock(Character character, float reachUnits, int samples, float pointRadius)
    {
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return null;

        var origin = character.IsOutside ? GetEvaWorldPosition(character) : character.Position;
        var (hullCenter, _) = GetHullLocalBounds();

        for (var i = 1; i <= samples; i++)
        {
            var point = origin + aim * (reachUnits * i / samples);
            var block = Ship.WallBlocks.FirstOrDefault(b => character.IsOutside
                ? (_shipFieldPosition + RotateLocalToWorld(b.Position - hullCenter, _shipRotationDegrees) - point).Length() <= pointRadius
                : b.RoomId == character.RoomId && (b.Position - point).Length() <= pointRadius);
            if (block is not null)
                return block;
        }
        return null;
    }

    // A station has nothing of the player's own ship to actually weld/cut (World.Welding.cs/World.
    // Cutting.cs's own comments explain why the torch still lights there but never does anything) -
    // this exists purely so the target-HP-bar UI still shows *something* while aimed at a station
    // wall, instead of silently showing nothing while the ship's equivalent bar would be lit. Never
    // called from StepWelding/StepCutting/DamageWallBlock/RepairWallBlock - a station's own walls
    // stay permanently un-mutatable regardless of what this reports.
    private WallBlock? FindAimedStationWallBlock(Character character, float reachUnits, int samples, float pointRadius)
    {
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return null;

        for (var i = 1; i <= samples; i++)
        {
            var point = character.Position + aim * (reachUnits * i / samples);
            var block = Station.WallBlocks.FirstOrDefault(b => b.RoomId == character.RoomId && (b.Position - point).Length() <= pointRadius);
            if (block is not null)
                return block;
        }
        return null;
    }

    // What the client shows a health bar over - only while a tool is actually lit and pointed at
    // something, the same contexts WeldAlongFlame/CutIndoorAlongFlame (World.Cutting.cs) themselves
    // act in. A wall's Hp is otherwise invisible on purpose (game_design.md's "quiet number"
    // convention, same as an untouched ore deposit's). The cutting branch goes through
    // FindAimedCutTarget rather than calling FindAimedWallBlock directly, so this can never show a
    // block's bar while the flame is actually about to cut a door instead (or the other way round).
    private string? GetWallToolTargetId(Character character)
    {
        if (character.OnEnemyShip)
            return null;
        if (IsWelding(character.PlayerId))
            return FindAimedWallBlock(character, WelderReachUnits, WelderSamples, WeldPointRadius)?.Id
                ?? (character.OnStation ? FindAimedStationWallBlock(character, WelderReachUnits, WelderSamples, WeldPointRadius)?.Id : null);
        if (IsCutting(character.PlayerId))
        {
            // Outside, CutAlongFlame (World.Cutting.cs) only ever reaches the hull once nothing
            // along the flame is ore - same priority order here, so the bar never shows a hull
            // block while the flame is actually about to bite into a vein instead.
            if (character.IsOutside)
                return FindAimedOreDeposit(character) is null
                    ? FindAimedWallBlock(character, WallCutReachUnits, WallCutSamples, WallCutPointRadius)?.Id
                    : null;
            return FindAimedCutTarget(character).WallBlockId
                ?? (character.OnStation ? FindAimedStationWallBlock(character, WallCutReachUnits, WallCutSamples, WallCutPointRadius)?.Id : null);
        }
        return null;
    }
}
