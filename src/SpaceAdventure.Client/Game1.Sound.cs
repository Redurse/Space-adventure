using System.Linq;
using SpaceAdventure.Client.Audio;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client;

// Which sounds fire when. Everything the server drives is detected the same way the door-break
// sound and EffectTracker already do it - by diffing the previous snapshot against the new one -
// because the client is never told "this happened", only what the world looks like now.
public partial class Game1
{
    private GameSounds? _sounds;

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

        // Doors. Only the ones that actually changed state, and the throttle inside GameSounds keeps
        // a whole bank of them cycling at once from stacking into distortion.
        if (previous is not null)
        {
            foreach (var state in current.DoorStates)
            {
                var before = previous.DoorStates.FirstOrDefault(s => s.DoorId == state.DoorId);
                if (before is null || before.IsOpen == state.IsOpen)
                    continue;
                _sounds.Play(state.IsOpen ? GameSounds.DoorOpen : GameSounds.DoorClose, nowSeconds, volume: 0.75f);
            }

            // A new hole in the hull. Counting rather than matching ids: a breach is loud enough that
            // one report per event is what is wanted, not one per broken block.
            var breaches = current.WallBlockStates.Count(b => b.Breached);
            if (_soundLastBreachCount >= 0 && breaches > _soundLastBreachCount)
                _sounds.Play(GameSounds.HullBreach, nowSeconds, pitchSpread: 0.03f);
            _soundLastBreachCount = breaches;
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
}
