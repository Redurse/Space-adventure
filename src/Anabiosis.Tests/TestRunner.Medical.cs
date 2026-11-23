using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Medicine in the manner of Neurotrauma (World.Medical.cs / BodyHealth): body parts with their own injuries, bleeding, breaks,
// consciousness, a stopped heart that can still be restarted for a while, and what each of those does to what a person can do.
internal static partial class TestRunner
{
    private static World WorldWithCharacter()
    {
        var world = new World();
        world.SpawnCharacter(1);
        return world;
    }

    // A bullet tears one part: trauma and a bleeding wound there, nothing elsewhere.
    private static bool Medical_Bullet_InjuresOnlyThatPartAndBleeds()
    {
        var world = WorldWithCharacter();
        world.DebugDamageCharacter(1, DamageKind.Bullet, 20f, BodyPart.RightLeg);
        var body = world.DebugBody(1);
        return body[BodyPart.RightLeg].Trauma > 20f && body[BodyPart.RightLeg].Bleeding > 5f
            && body[BodyPart.Torso].Trauma == 0f && body[BodyPart.Head].Trauma == 0f && body.IsBleeding;
    }

    // A bleeding wound drains health over time until it clots (or is treated).
    private static bool Medical_Bleeding_DrainsHealthOverTime()
    {
        var world = WorldWithCharacter();
        world.DebugDamageCharacter(1, DamageKind.Bullet, 20f, BodyPart.Torso);
        var after = world.DebugBody(1).Health;
        for (var i = 0; i < 10 * 30; i++)
            world.Step(RealtimeStep);
        return world.DebugBody(1).Health < after - 1f && world.DebugBody(1).BloodLoss > 1f;
    }

    // A wound clots by itself after a while: the bleeding stops on its own.
    private static bool Medical_Bleeding_StopsByItselfEventually()
    {
        var world = WorldWithCharacter();
        world.DebugDamageCharacter(1, DamageKind.Bullet, 12f, BodyPart.LeftArm);
        for (var i = 0; i < 60 * 30; i++)
            world.Step(RealtimeStep);
        return !world.DebugBody(1).IsBleeding;
    }

    // Broken legs slow a person down; a broken arm pair means no tools or weapons.
    private static bool Medical_BrokenLegsSlowAndBrokenArmsDropTheTools()
    {
        var world = WorldWithCharacter();
        var body = world.DebugBody(1);
        var healthyMobility = body.MobilityFactor;
        body.Fracture(BodyPart.LeftLeg);
        var oneLeg = body.MobilityFactor;
        body.Fracture(BodyPart.RightLeg);
        var bothLegs = body.MobilityFactor;
        var canSprint = body.CanSprint;
        var handsBefore = body.HandsUsable;
        body.Fracture(BodyPart.LeftArm);
        var oneArm = body.HandsUsable;
        body.Fracture(BodyPart.RightArm);
        return healthyMobility == 1f && oneLeg < healthyMobility && bothLegs < oneLeg && !canSprint
            && handsBefore && oneArm && !body.HandsUsable;
    }

    // A heavy blow takes a person down: unconscious, they ignore every command; they come round when they have recovered enough.
    private static bool Medical_HeavyInjury_KnocksOutAndIgnoresInput_ThenTheyWakeUp()
    {
        var world = WorldWithCharacter();
        var start = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        world.DebugBody(1).AddTrauma(BodyPart.Torso, 80f); // vitality ~20: out
        world.Step(RealtimeStep);
        var body = world.DebugBody(1);
        if (!body.Unconscious || body.InArrest || body.Dead)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var moved = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (Math.Abs(moved.X - start.X) > 0.01f)
            return false;

        body.HealTrauma(BodyPart.Torso, 60f); // treated
        world.Step(RealtimeStep);
        return !body.Unconscious;
    }

    // Running out of vitality stops the heart rather than killing at once; the person is still there, with minutes to be saved.
    private static bool Medical_OutOfVitality_StopsTheHeartAndTheWindowThenEndsInDeath()
    {
        var world = WorldWithCharacter();
        var body = world.DebugBody(1);
        body.AddTrauma(BodyPart.Torso, 100f);
        body.AddTrauma(BodyPart.Head, 20f);
        world.Step(RealtimeStep);
        if (!body.InArrest || body.Dead || body.Health < 1f || world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health < 1f)
            return false;

        for (var i = 0; i < (int)(BodyHealth.ArrestWindowSeconds * 30) - 60; i++)
            world.Step(RealtimeStep);
        if (body.Dead)
            return false; // still inside the window

        for (var i = 0; i < 4 * 30; i++)
            world.Step(RealtimeStep);
        return body.Dead && world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health <= 0f;
    }

    // Restarting the heart inside the window brings them back, weak.
    private static bool Medical_Resuscitation_InsideTheWindowBringsThemBack()
    {
        var world = WorldWithCharacter();
        var body = world.DebugBody(1);
        body.AddTrauma(BodyPart.Torso, 100f);
        body.AddTrauma(BodyPart.Head, 20f);
        world.Step(RealtimeStep);
        for (var i = 0; i < 30 * 30; i++)
            world.Step(RealtimeStep);
        body.Resuscitate(20f);
        world.Step(RealtimeStep);
        return !body.InArrest && !body.Dead && body.Health >= 15f;
    }

    // Thin air starves the brain; breathing again recovers it.
    private static bool Medical_Hypoxia_BuildsInThinAirAndRecoversWithBreath()
    {
        var world = WorldWithCharacter();
        var body = world.DebugBody(1);
        world.DebugDamageCharacter(1, DamageKind.Suffocation, 30f);
        var starved = body.Hypoxia;
        for (var i = 0; i < 15 * 30; i++)
            world.Step(RealtimeStep);
        return starved >= 30f && body.Hypoxia < starved - 20f;
    }

    // A blast batters the whole body at once, with a concussion.
    private static bool Medical_Blast_HurtsEveryPartAndConcusses()
    {
        var world = WorldWithCharacter();
        world.DebugDamageCharacter(1, DamageKind.Blast, 60f);
        var body = world.DebugBody(1);
        return body.AllParts.All(p => body[p].Trauma > 5f) && body.Concussion > 20f && body.Health < Character.MaxHealth - 20f;
    }

    // A painkiller takes the edge off the pain.
    private static bool Medical_Painkiller_ReducesPain()
    {
        var world = WorldWithCharacter();
        var body = world.DebugBody(1);
        body.AddTrauma(BodyPart.Torso, 40f);
        body.Fracture(BodyPart.LeftLeg);
        var before = body.Pain;
        body.GivePainkiller(60f);
        return before > 20f && body.Pain < before * 0.5f;
    }

    // After dying and respawning the body is whole again.
    private static bool Medical_Respawn_ResetsTheBody()
    {
        var world = WorldWithCharacter();
        world.DebugBody(1).AddTrauma(BodyPart.Torso, 50f);
        world.DebugBody(1).Fracture(BodyPart.RightArm);
        world.DebugKillCharacter(1);
        for (var i = 0; i < 12 * 30; i++)
            world.Step(RealtimeStep);
        var body = world.DebugBody(1);
        return !body.Dead && body.Health >= Character.MaxHealth && !body[BodyPart.RightArm].Fractured && body[BodyPart.Torso].Trauma == 0f;
    }

    // The client sees the body: parts, injuries and the state.
    private static bool Medical_SnapshotCarriesTheBodyState()
    {
        var world = WorldWithCharacter();
        world.DebugDamageCharacter(1, DamageKind.Bullet, 20f, BodyPart.Head);
        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return me.Body is { } body && body.Parts.Count == 6
            && body.Parts.Single(p => p.Part == BodyPart.Head).Trauma > 20f && body.Parts.Single(p => p.Part == BodyPart.Head).Bleeding > 5f
            && body.Concussion > 10f;
    }

    // Plain "set Health" callers (a scientist crewmate healing, a compartment killing) still work on the new model.
    private static bool Medical_SettingHealthStillHurtsAndHeals()
    {
        var world = WorldWithCharacter();
        var character = world.DebugBody(1);
        world.DebugDamageCharacter(1, DamageKind.Generic, 40f);
        var hurt = character.Health;
        var snapshotHurt = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        character.SetHealth(hurt + 30f);
        return hurt <= 60.5f && Math.Abs(snapshotHurt - hurt) < 0.01f && Math.Abs(character.Health - (hurt + 30f)) < 0.5f;
    }
}
