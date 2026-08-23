using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Audio;

// Plays whichever single track the ship's jukebox has selected (World.cs's JukeboxOn/TrackIndex/
// Volume) - Song/MediaPlayer like GameMusic, not SoundEffect: full-length tracks decoded whole
// into memory as SoundEffect would be tens of megabytes each, times 18 of them, for no benefit -
// a jukebox only ever plays one track at a time anyway. MediaPlayer is a single global channel
// though, so this and GameMusic's ambient bag take turns owning it rather than fighting over it:
// Game1.Music.cs simply stops the ambient bag outright whenever the jukebox is on.
//
// Tracks load lazily, one at a time, the first time each is actually selected - not all 18 up
// front in the constructor the way GameMusic preloads its own 5-track ambient bag. Eagerly reading
// ~110MB of mp3 off disk during LoadContent stretched the startup window in which MonoGame's own
// WinFormsGameWindow.OnDeactivate bug can null-ref if the window loses focus mid-load; loading only
// what's actually playing keeps LoadContent back down near its old duration.
public sealed class JukeboxAudio
{
    private readonly ContentManager _content;
    private readonly Dictionary<int, Song?> _loaded = new();
    private bool _active;
    private int _loadedIndex = -1;

    public JukeboxAudio(ContentManager content)
    {
        _content = content;
    }

    public bool IsActive => _active;

    /// <summary>Called every frame. Starts/switches the selected track, or stops if the jukebox is
    /// off or the ship has none at all.</summary>
    public void Update(JukeboxState? jukebox)
    {
        if (jukebox is null || !jukebox.On)
        {
            Stop();
            return;
        }

        var index = Wrap(jukebox.TrackIndex, JukeboxTracks.All.Length);
        if ((!_active || index != _loadedIndex) && LoadTrack(index) is { } song)
        {
            try
            {
                MediaPlayer.IsRepeating = true;
                MediaPlayer.Play(song);
                _active = true;
                _loadedIndex = index;
            }
            catch (Exception)
            {
                // Some machines have no media stack at all - fall back to silence.
                _active = false;
                _loadedIndex = -1;
            }
        }

        if (_active)
            MediaPlayer.Volume = MathHelper.Clamp(jukebox.Volume / 100f, 0f, 1f);
    }

    private Song? LoadTrack(int index)
    {
        if (_loaded.TryGetValue(index, out var cached))
            return cached;

        Song? song;
        try
        {
            song = _content.Load<Song>(JukeboxTracks.All[index].AssetName);
        }
        catch (Exception)
        {
            // A missing track costs that track, never the game - same contract GameMusic's own
            // load loop keeps.
            song = null;
        }
        _loaded[index] = song;
        return song;
    }

    public void Stop()
    {
        if (!_active)
            return;
        _active = false;
        _loadedIndex = -1;
        try
        {
            MediaPlayer.Stop();
        }
        catch (Exception)
        {
            // Nothing to do about it, and nothing that depends on it.
        }
    }

    private static int Wrap(int value, int count) => count <= 0 ? 0 : ((value % count) + count) % count;
}
