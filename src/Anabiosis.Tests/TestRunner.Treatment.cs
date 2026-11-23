using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Treating people (World.Treatment.cs / MedicalTreatment): items used on body parts, tourniquets, splints and casts, painkillers and
// their overdose, infection and antibiotics, chest compressions and the defibrillator, surgery on organs.
internal static partial class TestRunner
{
    private static World MedicWorld()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);
        // Side by side, so each is within reach of the other.
        MoveCharacterTo(world, 1, 7f, 3f);
        MoveCharacterTo(world, 2, 8f, 3f);
        return world;
    }

    private static void Treat(World world, int doctor, int patient, BodyPart part, ItemType item, float seconds = 12f)
    {
        world.ApplyCommand(doctor, new ClientCommand(doctor, Treat: new TreatRequest(patient, part, item)));
        for (var i = 0; i < (int)(seconds * 30); i++)
            world.Step(RealtimeStep);
    }

    private static bool HasItem(World world, int playerId, ItemType item) =>
        world.CreateSnapshot().Characters.Single(c => c.PlayerId == playerId).Inventory!.MainSlots.Contains(item);

    // A bandage dresses a bleeding wound: it takes a moment, uses the bandage up, and the wound bleeds much less.
    private static bool Treatment_Bandage_DressesTheWoundAndIsUsedUp()
    {
        var world = MedicWorld();
        world.DebugGiveItem(1, ItemType.Bandage);
        world.DebugDamageCharacter(2, DamageKind.Bullet, 20f, BodyPart.Torso);
        var before = world.DebugBody(2).TotalBleeding;

        world.ApplyCommand(1, new ClientCommand(1, Treat: new TreatRequest(2, BodyPart.Torso, ItemType.Bandage)));
        world.Step(RealtimeStep);
        if (!world.DebugIsTreating(1) || world.DebugBody(2)[BodyPart.Torso].Bandaged)
            return false; // it takes time

        for (var i = 0; i < 4 * 30; i++)
            world.Step(RealtimeStep);
        var body = world.DebugBody(2);
        return body[BodyPart.Torso].Bandaged && body.TotalBleeding < before * 0.5f && !HasItem(world, 1, ItemType.Bandage);
    }

    // Nothing is started for someone who does not need it, so nothing is wasted.
    private static bool Treatment_ItemNotWastedOnAHealthyBodyPart()
    {
        var world = MedicWorld();
        world.DebugGiveItem(1, ItemType.Bandage);
        Treat(world, 1, 2, BodyPart.LeftArm, ItemType.Bandage, 5f);
        return !world.DebugIsTreating(1) && HasItem(world, 1, ItemType.Bandage);
    }

    // Out of reach, no treatment; and you cannot treat with an item you do not have.
    private static bool Treatment_NeedsReachAndTheItem()
    {
        var world = MedicWorld();
        world.DebugDamageCharacter(2, DamageKind.Bullet, 20f, BodyPart.Torso);
        world.ApplyCommand(1, new ClientCommand(1, Treat: new TreatRequest(2, BodyPart.Torso, ItemType.Bandage)));
        if (world.DebugIsTreating(1))
            return false; // no bandage

        world.DebugGiveItem(1, ItemType.Bandage);
        MoveCharacterTo(world, 2, 14f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, Treat: new TreatRequest(2, BodyPart.Torso, ItemType.Bandage)));
        return !world.DebugIsTreating(1);
    }

    // Walking away drops the treatment.
    private static bool Treatment_IsInterruptedWhenTheDoctorWalksOff()
    {
        var world = MedicWorld();
        world.DebugGiveItem(1, ItemType.Splint);
        world.DebugBody(2).Fracture(BodyPart.LeftLeg);
        world.ApplyCommand(1, new ClientCommand(1, Treat: new TreatRequest(2, BodyPart.LeftLeg, ItemType.Splint)));
        world.Step(RealtimeStep);
        if (!world.DebugIsTreating(1))
            return false;
        MoveCharacterTo(world, 1, 12f, 3f);
        world.Step(RealtimeStep);
        return !world.DebugIsTreating(1) && !world.DebugBody(2)[BodyPart.LeftLeg].Splinted;
    }

    // A splint eases a break; a plaster cast immobilises it and the bone heals after a while.
    private static bool Treatment_SplintEasesAndCastHealsTheFracture()
    {
        var world = MedicWorld();
        var body = world.DebugBody(2);
        body.Fracture(BodyPart.LeftLeg);
        var broken = body.MobilityFactor;
        world.DebugGiveItem(1, ItemType.Splint);
        world.DebugGiveItem(1, ItemType.PlasterCast);
        Treat(world, 1, 2, BodyPart.LeftLeg, ItemType.Splint, 8f);
        var splinted = body.MobilityFactor;
        Treat(world, 1, 2, BodyPart.LeftLeg, ItemType.PlasterCast, 10f);
        var casted = body.MobilityFactor;
        if (!(broken < splinted && splinted < casted) || !body[BodyPart.LeftLeg].Cast)
            return false;

        for (var i = 0; i < 190 * 30; i++)
            world.Step(RealtimeStep);
        return !body[BodyPart.LeftLeg].Fractured && body.MobilityFactor >= 1f;
    }

    // A tourniquet stops a limb bleeding altogether, and can go only on a limb.
    private static bool Treatment_TourniquetStopsALimbBleedingButNotTheTorso()
    {
        var world = MedicWorld();
        world.DebugGiveItem(1, ItemType.Tourniquet);
        world.DebugDamageCharacter(2, DamageKind.Bullet, 30f, BodyPart.RightArm);
        world.DebugDamageCharacter(2, DamageKind.Bullet, 30f, BodyPart.Torso);
        world.ApplyCommand(1, new ClientCommand(1, Treat: new TreatRequest(2, BodyPart.Torso, ItemType.Tourniquet)));
        if (world.DebugIsTreating(1))
            return false;
        Treat(world, 1, 2, BodyPart.RightArm, ItemType.Tourniquet, 4f);
        var body = world.DebugBody(2);
        // With the arm tourniqueted only the torso still counts.
        return body[BodyPart.RightArm].Tourniquet && body.TotalBleeding < body[BodyPart.RightArm].Bleeding + body[BodyPart.Torso].Bleeding;
    }

    // A painkiller numbs pain, but too many in a row stops the breathing.
    private static bool Treatment_PainkillersNumPainAndOverdoseStopsBreathing()
    {
        var world = MedicWorld();
        var body = world.DebugBody(1);
        body.AddTrauma(BodyPart.Torso, 40f);
        var pain = body.Pain;
        for (var i = 0; i < 2; i++)
            body.GivePainkiller(120f);
        if (!(body.Pain < pain * 0.5f) || body.Overdosing)
            return false;
        body.GivePainkiller(120f);
        if (!body.Overdosing)
            return false;
        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);
        return body.Hypoxia > 10f || body.IsDowned;
    }

    // An open wound left alone festers into an infection; an antiseptic keeps it clean; an antibiotic clears it.
    private static bool Treatment_UntreatedWoundInfects_AntisepticPrevents_AntibioticCures()
    {
        var dirty = MedicWorld();
        dirty.DebugDamageCharacter(2, DamageKind.Burn, 40f, BodyPart.LeftArm);
        for (var i = 0; i < 200 * 30; i++)
            dirty.Step(RealtimeStep);
        var infection = dirty.DebugBody(2)[BodyPart.LeftArm].Infection;
        if (infection < 20f)
            return false;

        var clean = MedicWorld();
        clean.DebugGiveItem(1, ItemType.Antiseptic);
        clean.DebugDamageCharacter(2, DamageKind.Burn, 40f, BodyPart.LeftArm);
        Treat(clean, 1, 2, BodyPart.LeftArm, ItemType.Antiseptic, 5f);
        for (var i = 0; i < 200 * 30; i++)
            clean.Step(RealtimeStep);
        if (clean.DebugBody(2)[BodyPart.LeftArm].Infection >= infection * 0.5f)
            return false;

        dirty.DebugGiveItem(1, ItemType.Antibiotic);
        Treat(dirty, 1, 2, BodyPart.Torso, ItemType.Antibiotic, 4f);
        for (var i = 0; i < 120 * 30; i++)
            dirty.Step(RealtimeStep);
        return dirty.DebugBody(2)[BodyPart.LeftArm].Infection < infection * 0.5f;
    }

    // Chest compressions stop the heart's clock running down.
    private static bool Treatment_ChestCompressionsHoldBackTheClock()
    {
        var world = MedicWorld();
        var body = world.DebugBody(2);
        body.AddTrauma(BodyPart.Torso, 100f);
        body.AddTrauma(BodyPart.Head, 20f);
        body.AddOrganDamage(Organ.Heart, 90f); // too damaged for compressions alone to start it
        world.Step(RealtimeStep);
        if (!body.InArrest)
            return false;
        var left = body.ArrestSecondsLeft;

        for (var i = 0; i < 20 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, CprTargetId: 2));
            world.Step(RealtimeStep);
        }
        if (body.ArrestSecondsLeft < left - 1f)
            return false;

        world.ApplyCommand(1, new ClientCommand(1)); // stop
        for (var i = 0; i < 10 * 30; i++)
            world.Step(RealtimeStep);
        return body.ArrestSecondsLeft < left - 5f;
    }

    // A defibrillator brings a stopped heart back - a good roll restarts it, a bad one does not.
    private static bool Treatment_DefibrillatorRestartsTheHeart()
    {
        var good = new BodyHealth();
        good.AddTrauma(BodyPart.Torso, 100f);
        good.AddTrauma(BodyPart.Head, 20f);
        good.Normalize();
        good.Compress();
        if (!good.InArrest || !good.Defibrillate(0.01f) || good.InArrest)
            return false;

        var bad = new BodyHealth();
        bad.AddTrauma(BodyPart.Torso, 100f);
        bad.AddTrauma(BodyPart.Head, 20f);
        bad.Normalize();
        return bad.Defibrillate(0.99f) && bad.InArrest;
    }

    // The doctor's defibrillator through the real command: shocks, revives with a good roll, and is not used up.
    private static bool Treatment_DefibrillatorCommandUsesTheItemButKeepsIt()
    {
        // A shock only takes some of the time and the clock is 30 s, so a patient can be lost before one does: try fresh patients until one is brought
        // back (a dozen is certain enough that this never depends on which seed the run happened to roll).
        for (var patient = 0; patient < 12; patient++)
        {
            var world = MedicWorld();
            world.DebugGiveItem(1, ItemType.Defibrillator);
            var body = world.DebugBody(2);
            body.AddTrauma(BodyPart.Torso, 100f);
            body.AddTrauma(BodyPart.Head, 20f);
            body.Normalize();
            if (!body.InArrest)
                return false;
            for (var tries = 0; body.InArrest && !body.Dead && tries < 4; tries++)
                Treat(world, 1, 2, BodyPart.Torso, ItemType.Defibrillator, 9f); // 8 s between shocks
            if (!body.InArrest && !body.Dead)
                return HasItem(world, 1, ItemType.Defibrillator);
        }
        return false;
    }

    // Surgery: only on someone out cold, and it mends the organ.
    private static bool Treatment_SurgeryNeedsAnUnconsciousPatientAndMendsTheOrgan()
    {
        var world = MedicWorld();
        world.DebugGiveItem(1, ItemType.SurgeryKit);
        var body = world.DebugBody(2);
        body.AddOrganDamage(Organ.Lungs, 60f);
        world.ApplyCommand(1, new ClientCommand(1, Surgery: new SurgeryRequest(2, Organ.Lungs)));
        if (world.DebugIsTreating(1))
            return false; // awake - refused

        body.AddTrauma(BodyPart.Torso, 75f); // knocked out
        world.Step(RealtimeStep);
        if (!body.Unconscious)
            return false;
        world.ApplyCommand(1, new ClientCommand(1, Surgery: new SurgeryRequest(2, Organ.Lungs)));
        if (!world.DebugIsTreating(1))
            return false;
        for (var i = 0; i < 16 * 30; i++)
            world.Step(RealtimeStep);
        return body.LungDamage < 30f;
    }

    // What the health window recommends matches the injury.
    private static bool Treatment_RecommendationsMatchTheInjuries()
    {
        var body = new BodyHealth();
        body.AddBleeding(BodyPart.LeftLeg, 20f);
        body.Fracture(BodyPart.LeftLeg);
        body.AddBurn(BodyPart.Torso, 30f);
        var leg = MedicalTreatment.Recommended(body, BodyPart.LeftLeg);
        var torso = MedicalTreatment.Recommended(body, BodyPart.Torso);
        return leg.Contains(ItemType.Bandage) && leg.Contains(ItemType.Tourniquet) && leg.Contains(ItemType.Splint) && leg.Contains(ItemType.PlasterCast)
            && torso.Contains(ItemType.BurnGel) && !torso.Contains(ItemType.Splint);
    }

    // Heavy damage to the head harms the brain, to the chest the heart and lungs.
    private static bool Treatment_BulletsAndBlastsHurtOrgans()
    {
        // Organ hits are a roll, and two 30-damage bullets a round can kill before both rolls have
        // come up - whether they do used to depend on the seed, i.e. on this test's position in the
        // list. So each organ counts the moment it's hurt, and a dead character is replaced.
        var world = WorldWithCharacter();
        var brainHurt = false;
        var chestOrganHurt = false;
        for (var i = 0; i < 25 && !(brainHurt && chestOrganHurt); i++)
        {
            world.DebugDamageCharacter(1, DamageKind.Bullet, 30f, BodyPart.Head);
            world.DebugDamageCharacter(1, DamageKind.Bullet, 30f, BodyPart.Torso);
            var body = world.DebugBody(1);
            brainHurt |= body.BrainDamage > 0f;
            chestOrganHurt |= body.HeartDamage > 0f || body.LungDamage > 0f;
            if (body.Dead)
                world = WorldWithCharacter();
        }
        var hurt = brainHurt && chestOrganHurt;
        var blast = WorldWithCharacter();
        blast.DebugDamageCharacter(1, DamageKind.Blast, 70f);
        return hurt && blast.DebugBody(1).LungDamage > 0f;
    }
}
