using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;
using Anabiosis.Client.Audio;
using NAudio.Wave;

// The ship's soundscape (Client/Audio/ShipAmbience.cs): the small pure pieces, and the sound files it relies on.
internal static partial class TestRunner
{
    // The Client project's own Sounds folder, found by walking up from where the tests run (the audio engine itself is never opened here).
    private static string? SoundsFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Anabiosis.Client", "Content", "Sounds");
            if (Directory.Exists(candidate))
                return candidate;
            candidate = Path.Combine(directory.FullName, "Content", "Sounds");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "ui_click.wav")))
                return candidate;
            directory = directory.Parent;
        }
        return null;
    }

    private static bool Ambience_SoundsFadeWithDistance()
    {
        return AmbienceMath.Attenuation(0f) == 1f && AmbienceMath.Attenuation(3f) == 1f && AmbienceMath.Attenuation(26f) == 0f && AmbienceMath.Attenuation(400f) == 0f
            && AmbienceMath.Attenuation(8f) > AmbienceMath.Attenuation(16f) && AmbienceMath.Attenuation(16f) > AmbienceMath.Attenuation(24f)
            && AmbienceMath.Pan(-100f) == -0.85f && AmbienceMath.Pan(100f) == 0.85f && AmbienceMath.Pan(0f) == 0f;
    }

    // A level follows its target smoothly: never past it, never a jump, and a long enough time gets there.
    private static bool Ambience_LevelsFollowTheirTargetWithoutJumps()
    {
        var level = 0f;
        var previous = 0f;
        for (var i = 0; i < 20; i++)
        {
            level = AmbienceMath.Approach(level, 1f, 1f / 60f, 1.4f);
            if (level < previous || level > 1f || level - previous > 0.05f)
                return false;
            previous = level;
        }
        for (var i = 0; i < 600; i++)
            level = AmbienceMath.Approach(level, 1f, 1f / 60f, 1.4f);
        return level > 0.999f && AmbienceMath.Approach(0.5f, 0.5f, 0.1f, 3f) == 0.5f && AmbienceMath.Approach(0f, 1f, 5f, 3f) == 1f;
    }

    // Steps come from distance walked, however it was cut into frames, and none are lost or invented.
    private static bool Ambience_StepsAreCountedFromDistanceWalked()
    {
        var counter = new StrideCounter();
        var steps = 0;
        for (var i = 0; i < 100; i++)
            steps += counter.Advance(0.1f, 1.2f); // 10 units in tenths: 8 steps of 1.2, with 0.4 left over
        if (steps != 8)
            return false;
        steps += new StrideCounter().Advance(0f, 1.2f) + new StrideCounter().Advance(0.5f, 1.2f);
        var fast = new StrideCounter();
        return steps == 8 && fast.Advance(3.7f, 1.2f) == 3;
    }

    // Random events come at random, not on a beat, and come faster when the ship is hurt.
    private static bool Ambience_RandomEventsSpeedUpWithTension()
    {
        int Count(float intensity)
        {
            var timer = new RandomEventTimer(new Random(5), 18, 50);
            var count = 0;
            for (var t = 0.0; t < 6000.0; t += 0.5)
                if (timer.Due(t, intensity))
                    count++;
            return count;
        }
        var calm = Count(0f);
        var hurt = Count(1f);
        if (calm < 100 || calm > 400 || hurt < calm * 2.5)
            return false; // 6000 s / ~34 s is about 175; a quarter of the gap should give about four times as many
        // And the gaps are not all the same.
        var gaps = new List<double>();
        var spaced = new RandomEventTimer(new Random(9), 18, 50);
        double last = 0;
        for (var t = 0.0; t < 3000.0; t += 0.5)
            if (spaced.Due(t, 0f))
            {
                gaps.Add(t - last);
                last = t;
            }
        gaps.RemoveAt(0); // the first one is allowed to come early
        return gaps.Count > 30 && gaps.Max() - gaps.Min() > 15 && gaps.Min() >= 17.5;
    }

    // Every sound the soundscape plays is there and can be read; the new loops are long enough to be a bed.
    private static bool Ambience_EveryFileItUsesIsThereAndReadable()
    {
        var folder = SoundsFolder();
        if (folder is null)
            return false;
        var names = new List<string>
        {
            "ship_hum_loop", "vent_loop", "pipe_knock", "power_up", "power_down",
            "reactor_hum_loop", "alarm_loop", "weld_loop", "cutter_loop", "jetpack_loop", "steam_vent",
        };
        names.AddRange(Enumerable.Range(1, 16).Select(i => $"footstep_metal_{i}"));
        names.AddRange(new[] { "pick_item", "drop_item", "pick_item_fail", "chat_msg", "repair_loop", "door_1", "door_2", "door_3", "door_4", "door_break_1", "door_break_2" });
        names.AddRange(Enumerable.Range(1, 4).Select(i => $"creak_{i}"));
        names.AddRange(Enumerable.Range(1, 3).Select(i => $"drip_{i}"));
        names.AddRange(Enumerable.Range(1, 2).Select(i => $"thud_far_{i}"));
        names.AddRange(Enumerable.Range(1, 2).Select(i => $"spark_crackle_{i}"));
        foreach (var name in names)
        {
            var path = Path.Combine(folder, name + ".wav");
            if (!File.Exists(path))
                return false;
            using var reader = new WaveFileReader(path);
            if (reader.TotalTime.TotalSeconds < 0.1)
                return false;
            if (name is "ship_hum_loop" or "vent_loop" && reader.TotalTime.TotalSeconds < 5)
                return false;
        }
        return true;
    }

    // The two new beds loop without a click: the last sample runs into the first as smoothly as any two neighbours.
    private static bool Ambience_BedsLoopWithoutAClick()
    {
        var folder = SoundsFolder();
        if (folder is null)
            return false;
        foreach (var name in new[] { "ship_hum_loop", "vent_loop" })
        {
            using var reader = new WaveFileReader(Path.Combine(folder, name + ".wav"));
            var samples = new List<float>();
            var provider = reader.ToSampleProvider();
            var buffer = new float[4096];
            int read;
            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
                samples.AddRange(buffer.Take(read));
            var seam = Math.Abs(samples[0] - samples[^1]);
            var typical = Enumerable.Range(1, samples.Count - 1).Select(i => Math.Abs(samples[i] - samples[i - 1])).Average();
            if (seam > typical * 6 + 0.01f)
                return false;
        }
        return true;
    }
}

// The snapshot says who is mending something, so the client can play the repair melody for them.
internal static partial class TestRunner
{
    private static bool Ambience_RepairingIsReportedOnlyWhileSomeoneMends()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.CutWire("trunk-system-shields");

        WalkAcrossShipTo(world, 7.2f, 0.7f); // the reactor room's shields device
        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));
        WalkAcrossShipTo(world, 7.2f, 0.7f);
        world.Step(1.0 / 30);
        if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Repairing)
            return false; // a wrench in hand next to a broken device is not yet a repair: nothing was started

        world.ApplyCommand(1, new ClientCommand(1, RepairDeviceId: "system-shields"));
        world.Step(1.0 / 30);
        var working = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Repairing;

        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot)); // puts the wrench away
        world.Step(1.0 / 30);
        var idle = !world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Repairing;
        return working && idle;
    }
}
