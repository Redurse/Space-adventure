using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace SpaceAdventure.Client.Audio;

// One place that owns every sound effect, so adding the twentieth does not mean adding a twentieth
// field, a twentieth try/catch and a twentieth null check at the call site.
//
// Three things every game needs from a sound layer and none of them are the loading:
//
//   * Pitch variation. The same file fired twice in a row is instantly recognisable as a file. A
//     few percent of random detune per shot is the difference between a footstep and a machine gun
//     made of one footstep.
//   * Throttling. Several doors closing on the same tick must not play three copies at full volume
//     into each other - that is not louder, it is distorted.
//   * Failing quietly. A missing .xnb costs the sound, never the game, exactly like Shaders.TryLoad
//     does for effects.
public sealed class GameSounds
{
    // Names are the asset paths under Content/Sounds, without extension.
    public const string UiClick = "ui_click";
    public const string UiDeny = "ui_deny";
    public const string PanelOpen = "panel_open";
    public const string PanelClose = "panel_close";
    public const string DoorOpen = "door_open";
    public const string DoorClose = "door_close";
    public const string AirlockCycle = "airlock_cycle";
    public const string ItemPickup = "item_pickup";
    public const string ItemDrop = "item_drop";
    public const string HullBreach = "hull_breach";
    public const string LaserShot = "laser_shot";
    public const string RifleShot = "rifle_shot";
    public const string LowOxygen = "low_oxygen";
    public const string WeldLoop = "weld_loop";
    public const string CutterLoop = "cutter_loop";
    public const string AlarmLoop = "alarm_loop";
    public const string JetpackLoop = "jetpack_loop";
    public const string SteamVent = "steam_vent";
    public const string ReactorHumLoop = "reactor_hum_loop";

    private static readonly string[] AllNames =
    {
        UiClick, UiDeny, PanelOpen, PanelClose, DoorOpen, DoorClose, AirlockCycle,
        ItemPickup, ItemDrop, HullBreach, LaserShot, RifleShot, LowOxygen,
        WeldLoop, CutterLoop, AlarmLoop, JetpackLoop, SteamVent, ReactorHumLoop,
    };

    private readonly Dictionary<string, SoundEffect> _effects = new();
    private readonly Dictionary<string, double> _lastPlayed = new();
    private readonly Random _random = new();

    // Shortest gap between two plays of the same sound. Anything closer is the same event being
    // reported twice, not two events.
    private const double MinRepeatSeconds = 0.06;

    public GameSounds(ContentManager content)
    {
        foreach (var name in AllNames)
        {
            try
            {
                _effects[name] = content.Load<SoundEffect>("Sounds/" + name);
            }
            catch
            {
                // Missing or unbuilt: that one sound is silent, everything else still works.
            }
        }
    }

    // Volume is this sound's own level; SoundEffect.MasterVolume (set from the settings screen)
    // scales all of them on top of it.
    public void Play(string name, double nowSeconds, float volume = 1f, float pitchSpread = 0.06f, float pan = 0f)
    {
        if (!_effects.TryGetValue(name, out var effect))
            return;
        if (_lastPlayed.TryGetValue(name, out var last) && nowSeconds - last < MinRepeatSeconds)
            return;

        _lastPlayed[name] = nowSeconds;
        var pitch = pitchSpread <= 0f ? 0f : (float)(_random.NextDouble() * 2 - 1) * pitchSpread;
        effect.Play(Math.Clamp(volume, 0f, 1f), Math.Clamp(pitch, -1f, 1f), Math.Clamp(pan, -1f, 1f));
    }

    // A looping instance the caller owns and starts/stops itself - for the welder, the reactor bed
    // and anything else that runs for as long as a state holds rather than firing once.
    public SoundEffectInstance? CreateLoop(string name, float volume = 1f)
    {
        if (!_effects.TryGetValue(name, out var effect))
            return null;

        var instance = effect.CreateInstance();
        instance.IsLooped = true;
        instance.Volume = Math.Clamp(volume, 0f, 1f);
        return instance;
    }
}
