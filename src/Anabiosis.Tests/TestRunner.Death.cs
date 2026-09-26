using Anabiosis.Server;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // Direct user request ("экран смерти... почти точь в точь как в баротравме") -
    // Character.IsDead (Health <= 0) gates World.Movement.cs's StepCharacters and World.cs's
    // ApplyCommand; these are the server-observable half of that gate. The client-side death
    // screen/spectator camera has no automated coverage here (MonoGame doesn't run headless in
    // this project - same as every other client-only UI, manual verification only).
    private static bool World_DeadCharacter_CannotMove()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugKillCharacter(1);

        var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        return Math.Abs(before.X - after.X) < 0.01f && Math.Abs(before.Y - after.Y) < 0.01f;
    }

    // DoorToggleId is client-trusted, no proximity check (World.cs's own comment on why) - the
    // simplest command to prove ApplyCommand's dead-character gate blocks "большинство команд" in
    // general, not just movement specifically.
    private static bool World_DeadCharacter_CannotToggleDoors()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugKillCharacter(1);

        var before = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor"));
        var after = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;

        return before == after; // unchanged - the command never even reached ToggleDoor
    }

    // The gate doesn't force anything to happen either (plan's own explicit scope - "не роняет
    // предметы и не покидает турель/руль автоматически"): dying while manning a turret leaves the
    // MannedByPlayerId exactly as it was, nobody else's business to clear it.
    private static bool World_DeadCharacter_StaysSeatedAtTurretRatherThanBeingEjected()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        var mannedBefore = world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 1);

        world.DebugKillCharacter(1);
        world.Step(RealtimeStep);
        var mannedAfter = world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 1);

        return mannedBefore && mannedAfter;
    }

    // Direct user request ("через 10 секунд... игрок спанился на корабле в кокпите") - the seat
    // this same test file's own StaysSeatedAtTurretRatherThanBeingEjected test proves is left alone
    // IMMEDIATELY after death eventually does get cleared, once the respawn countdown itself (not
    // any other system) reaches zero - RespawnCharacter, World.Respawn.cs.
    private static bool World_DeadCharacter_RespawnsAfterTenSeconds()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        var mannedBeforeDeath = world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 1);

        world.DebugKillCharacter(1);
        // 300 steps at RealtimeStep (1/30s) is exactly 10s - one extra step to land past the
        // countdown's own <= 0 trigger rather than exactly on its edge.
        for (var i = 0; i < 301; i++)
            world.Step(RealtimeStep);

        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var mannedAfterRespawn = world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 1);

        return mannedBeforeDeath
            && after.Health >= Character.MaxHealth
            && Math.Abs(after.X - world.Ship.SpawnPoint.X) < 0.01
            && Math.Abs(after.Y - world.Ship.SpawnPoint.Y) < 0.01
            && !mannedAfterRespawn;
    }
}
