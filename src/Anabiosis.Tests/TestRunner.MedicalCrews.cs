using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Medicine for the people the player does not control: a hired scientist treats the crew by the body rules (compressions, dressings),
// a hostile crew member has a real body (a hit makes him bleed, a downed one stops acting), and the medbay is a catalog compartment.
internal static partial class TestRunner
{
    private static bool Medical_ScientistBotKeepsAStoppedHeartGoing()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FundGenerously(world);
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: RerollUntilRoleOffered(world, CrewRole.Scientist).Id));

        // The player collapses with a stopped heart: nobody but the bot is around to do anything about it.
        var body = world.DebugBody(1);
        body.AddTrauma(BodyPart.Torso, 100f);
        body.AddTrauma(BodyPart.Head, 20f);
        body.AddOrganDamage(Organ.Heart, 90f); // too damaged for compressions alone to start it
        world.Step(RealtimeStep);
        if (!body.InArrest)
            return false;

        var before = body.ArrestSecondsLeft;
        for (var i = 0; i < 5 * 30; i++)
            world.Step(RealtimeStep);

        // The clock was held (compressions) rather than ticking five seconds down - or the heart is already going again.
        return !body.InArrest || body.ArrestSecondsLeft >= before - 1f;
    }

    private static bool Medical_ScientistBotDressesABleedingWound()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FundGenerously(world);
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: RerollUntilRoleOffered(world, CrewRole.Scientist).Id));

        world.DebugDamageCharacter(1, DamageKind.Bullet, 12f, BodyPart.LeftArm);
        world.Step(RealtimeStep);
        var body = world.DebugBody(1);
        if (body[BodyPart.LeftArm].Bleeding <= 0.5f)
            return false;

        for (var i = 0; i < 8 * 30; i++)
            world.Step(RealtimeStep);

        return body[BodyPart.LeftArm].Bandaged || body.TotalBleeding < 0.5f;
    }

    private static bool Medical_EnemyCrewHitBleedsAndHasABody()
    {
        var world = EngagedWorld();
        var crew = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Role == EnemyCrewRole.Fighter);
        world.DebugDamageEnemyCrewBody(crew.Id, DamageKind.Bullet, 14f);
        var body = world.DebugEnemyCrewBody(crew.Id);
        return body.TotalBleeding > 0.5f && world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == crew.Id).Health < 60f;
    }

    private static bool Medical_EnemyCrewDownedStopsActing()
    {
        var world = EngagedWorld();
        var crew = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Role == EnemyCrewRole.Fighter);
        var body = world.DebugEnemyCrewBody(crew.Id);
        body.AddTrauma(BodyPart.Torso, 90f);
        world.Step(RealtimeStep);
        if (!body.IsDowned)
            return false;

        var at = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == crew.Id);
        for (var i = 0; i < 3 * 30; i++)
            world.Step(RealtimeStep);
        var later = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == crew.Id);
        return Math.Abs(later.X - at.X) < 0.01f && Math.Abs(later.Y - at.Y) < 0.01f;
    }

    private static bool Medical_GiveUpDuringArrestKillsTheCharacter()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var body = world.DebugBody(1);
        body.AddTrauma(BodyPart.Torso, 100f);
        body.AddTrauma(BodyPart.Head, 20f);
        body.AddOrganDamage(Organ.Heart, 90f);
        world.Step(RealtimeStep);
        if (!body.InArrest)
            return false;

        // Awake and merely hurt, nothing happens.
        var other = new World();
        other.SpawnCharacter(1);
        other.ApplyCommand(1, new ClientCommand(1, GiveUp: true));
        if (other.DebugBody(1).Dead)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, GiveUp: true));
        return body.Dead && world.CreateSnapshot().Characters.Single().Health <= 0f;
    }

    private static bool Medical_MedbayIsACatalogCompartment()
    {
        var entry = CompartmentCatalog.Find("medbay");
        return entry is { Type: CompartmentType.Medical, DisplayName: "Медотсек" }
            && CompartmentPricing.EntryForRoomName("Медотсек")?.Type == CompartmentType.Medical
            && CompartmentPricing.Price(entry) > 0;
    }
}
