using System;
using System.Linq;
using Anabiosis.Client.Audio;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// Which sounds fire when. Everything the server drives is detected the same way the door-break
// sound and EffectTracker already do it - by diffing the previous snapshot against the new one -
// because the client is never told "this happened", only what the world looks like now.
public partial class Game1
{
    private GameSounds? _sounds;
    // The ship's own soundscape: beds, alarms, footsteps, work loops, creaks (Audio/ShipAmbience.cs).
    private ShipAmbience? _ambience;

    // Panels are pure client state, so this is remembered here rather than diffed off a snapshot.
    private BlockKind _soundLastOpenBlock = BlockKind.None;
    private int _soundLastBreachCount = -1;
    private double _soundLastOxygenWarning = double.NegativeInfinity;

    // How low the player's own oxygen has to get before the suit starts complaining, and how long
    // it waits before saying it again.
    private const float LowOxygenThreshold = 25f;
    private const double LowOxygenRepeatSeconds = 6.0;

    private void UpdateWorldSounds(WorldSnapshot? previous, WorldSnapshot current, double nowSeconds)
    {
        if (_sounds is null)
            return;

        // The ship's own soundscape follows the newest snapshot every frame (it works out for itself what is new).
        _ambience?.Update(current, _client.PlayerId, nowSeconds);

        // Doors. Only the ones that actually changed state, and the throttle inside GameSounds keeps
        // a whole bank of them cycling at once from stacking into distortion.
        if (previous is not null)
        {
            foreach (var state in current.DoorStates)
            {
                var before = previous.DoorStates.FirstOrDefault(s => s.DoorId == state.DoorId);
                if (before is not null)
                    PlayDoorChange(before.IsOpen, before.Destroyed, state.IsOpen, state.Destroyed, nowSeconds);
            }

            // Direct user report ("добавь звук при открытии двери как это было раньше") - M-doors-
            // as-edges (the Ship Editor's Door/"Шлюз" tool, now the only way to place a door there)
            // carries its own state list (DoorEdgeStates), completely separate from DoorStates
            // above - this loop was never added when that model shipped, so every door on a
            // Ship-Editor-built hull has been silent regardless of open/closed state.
            foreach (var state in current.DoorEdgeStates ?? Array.Empty<DoorEdgeState>())
            {
                var before = (previous.DoorEdgeStates ?? Array.Empty<DoorEdgeState>()).FirstOrDefault(s => s.Id == state.Id);
                if (before is not null)
                    PlayDoorChange(before.IsOpen, before.Destroyed, state.IsOpen, state.Destroyed, nowSeconds);
            }

            // A new hole in the hull. Counting rather than matching ids: a breach is loud enough that
            // one report per event is what is wanted, not one per broken block.
            var breaches = current.WallBlockStates.Count(b => b.Breached);
            if (_soundLastBreachCount >= 0 && breaches > _soundLastBreachCount)
                _sounds.Play(GameSounds.HullBreach, nowSeconds, pitchSpread: 0.03f);
            _soundLastBreachCount = breaches;

            // A compartment (or the reactor) going up - once per new explosion id. Blasts stay in the snapshot for a couple
            // of seconds, so the ids already heard are remembered rather than compared against the previous tick.
            foreach (var blast in current.ShipBlasts ?? Array.Empty<ShipBlastState>())
                if (_soundHeardBlasts.Add(blast.Id))
                    _sounds.Play(blast.Kind == ShipBlastKind.Reactor ? GameSounds.ExplosionBig : GameSounds.Explosion, nowSeconds, pitchSpread: 0.04f);
            if (_soundHeardBlasts.Count > 64)
                _soundHeardBlasts.Clear();

            UpdateCombatSounds(previous, current, nowSeconds);
        }

        // The player's own suit, not the ship's: everyone hears their own air running out.
        var me = current.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me is { IsOutside: true, SuitTank: { } suitAir } && suitAir <= LowOxygenThreshold
            && nowSeconds - _soundLastOxygenWarning >= LowOxygenRepeatSeconds)
        {
            _soundLastOxygenWarning = nowSeconds;
            _sounds.Play(GameSounds.LowOxygen, nowSeconds, volume: 0.6f, pitchSpread: 0f);
        }
    }

    private readonly System.Collections.Generic.HashSet<string> _soundHeardBlasts = new();

    // Opening and closing a block terminal. Client-side state, so it is watched here rather than in
    // the snapshot diff above.
    private void UpdatePanelSounds(double nowSeconds)
    {
        if (_sounds is null || _openBlock.Kind == _soundLastOpenBlock)
            return;

        var opened = _openBlock.Kind != BlockKind.None;
        _sounds.Play(opened ? GameSounds.PanelOpen : GameSounds.PanelClose, nowSeconds, volume: 0.55f);
        _soundLastOpenBlock = _openBlock.Kind;
    }

    // Direct user request ("звук как при нажатии на кнопки в баротравме") - main menu buttons,
    // Settings tabs/buttons (reached from either the main menu or the in-session pause menu) and the
    // pause menu's own 4 buttons all call this at the exact point a click lands on something real,
    // rather than on every mouse-down. Environment.TickCount64 rather than gameTime - every one of
    // these call sites runs from a context (pre-session menu input, the pause-menu click branch
    // inside HandleMouseClick) that doesn't have a GameTime in scope, and GameSounds.PlayInternal's
    // own repeat-throttle only needs a steadily-increasing clock, not the simulation's own one.
    private void PlayUiClick() => _sounds?.PlayUi(GameSounds.UiClick, Environment.TickCount64 / 1000.0);
}

// Item sounds fired from input handling (which has no GameTime in scope).
public partial class Game1
{
    private void PlayItemSound(string name, float volume) =>
        _sounds?.Play(name, Environment.TickCount64 / 1000.0, volume: volume, pitchSpread: 0.04f);

    // Taking something up off the floor: the pick-up sound, or the refusal when there is nowhere to put it.
    private void PlayFloorPickupSound(CharacterState me)
    {
        var inventory = me.Inventory;
        var room = inventory is null || inventory.MainSlots.Any(slot => slot is null) || inventory.BeltBagSlots.Any(slot => slot is null);
        PlayItemSound(room ? GameSounds.ItemPickup : GameSounds.ItemFail, 0.6f);
    }
}

// Doors: opening and closing play one of four recordings (never the same twice in a row), a door that has just been destroyed plays one of two breaking sounds.
public partial class Game1
{
    private const int DoorSoundVariants = 4;
    private const int DoorBreakVariants = 2;
    private int _lastDoorVariant;
    private readonly Random _doorSoundRandom = new();

    private void PlayDoorChange(bool wasOpen, bool wasDestroyed, bool isOpen, bool isDestroyed, double nowSeconds)
    {
        if (_sounds is null)
            return;
        if (isDestroyed)
        {
            if (!wasDestroyed)
                _sounds.Play($"door_break_{_doorSoundRandom.Next(1, DoorBreakVariants + 1)}", nowSeconds, volume: 0.9f, pitchSpread: 0.05f);
            return; // a broken door has no more movement to hear
        }
        if (wasOpen == isOpen)
            return;
        var variant = _doorSoundRandom.Next(1, DoorSoundVariants + 1);
        if (variant == _lastDoorVariant)
            variant = variant % DoorSoundVariants + 1;
        _lastDoorVariant = variant;
        _sounds.Play($"door_{variant}", nowSeconds, volume: isOpen ? 0.75f : 0.65f, pitchSpread: 0.05f);
    }
}
