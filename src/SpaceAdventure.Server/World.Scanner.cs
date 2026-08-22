using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// The navigation console's scanner (game_design.md/M44): a directional sweep that finds persistent
// NPC hulls (World.NpcShips.cs, M43) the crew hasn't otherwise seen, and remembers where it found
// them - stations and the asteroid field itself stay always-visible on the map (GalaxyPoints, same
// as before this milestone); only ambient traffic is hidden by default. Each player's own
// discoveries are private (CharacterState.ScannerContacts) until a Scientist deliberately promotes
// one onto the shared map as a ManualScannerMarker.
public sealed partial class World
{
    // A narrow directional sweep, not an omnidirectional radar - reading the bearing off the dial
    // actually matters. Half-width, not full width: the cone spans SweepHalfAngle either side of
    // ScannerSweepDegrees.
    private const float ScannerSweepHalfAngleDegrees = 12f;
    // Comfortably short of the field's own full extent (2400) - a sweep that already covered the
    // whole system from any one spot would make standing at the console strictly better than
    // flying to look, which isn't the point of a director­al sensor.
    private const float ScannerRangeUnits = 900f;

    // Which NPC hulls each player has personally found, and where they were the moment that
    // happened - a Dictionary<string, ...> per player rather than a flat list, so re-detecting the
    // same hull updates its entry in place instead of accumulating duplicates.
    private readonly Dictionary<int, Dictionary<string, ScannerContactState>> _scannerContactsByPlayer = new();
    // Shared with the whole crew, unlike the private contacts above - a Scientist's own deliberate
    // "put this on the map for everyone" action (PlaceScannerMarkerAtX/Y), not a byproduct of
    // sweeping. Persisted (World.Save.cs) the same way everything else campaign-persistent is.
    private readonly List<Vec2> _manualScannerMarkers = new();

    // Applies ScannerSweepDegrees/PlaceScannerMarkerAtX/Y from ApplyCommand - both gated on
    // physically standing at the console, the same InteractionRadius proximity check the reactor's
    // own levers use (World.cs), rather than a separate seated/toggle state.
    private void HandleScannerInput(Character character, ClientCommand command)
    {
        if ((Ship.NavigationConsole.Position - character.Position).Length() >= InteractionRadius)
            return;

        character.ScannerSweepDegrees = command.ScannerSweepDegrees;

        if (command.PlaceScannerMarkerAtX is { } markerX && command.PlaceScannerMarkerAtY is { } markerY)
            _manualScannerMarkers.Add(new Vec2(markerX, markerY));
    }

    // One sweep per character actually at the console right now - walking away freezes the dial
    // (HandleScannerInput's own doc comment) and, just as naturally, stops finding anything new.
    private void StepScanners(double deltaSeconds)
    {
        foreach (var character in _characters.Values)
        {
            if ((Ship.NavigationConsole.Position - character.Position).Length() >= InteractionRadius)
                continue;

            var contacts = _scannerContactsByPlayer.TryGetValue(character.PlayerId, out var existing)
                ? existing
                : _scannerContactsByPlayer[character.PlayerId] = new Dictionary<string, ScannerContactState>();

            foreach (var npc in _npcShips)
            {
                var toNpc = npc.Position - _shipFieldPosition;
                if (toNpc.Length() > ScannerRangeUnits)
                    continue;

                var bearing = MathF.Atan2(toNpc.Y, toNpc.X) * (180f / MathF.PI);
                var offBearing = MathF.Abs(ShortestAngle(bearing - character.ScannerSweepDegrees));
                if (offBearing > ScannerSweepHalfAngleDegrees)
                    continue;

                contacts[npc.Id] = new ScannerContactState(
                    npc.Id, npc.Kind, npc.FactionId, npc.Position.X, npc.Position.Y, npc.RotationDegrees, Tick);
            }
        }
    }

    private IReadOnlyList<ScannerContactState> CreateScannerContacts(int playerId) =>
        _scannerContactsByPlayer.TryGetValue(playerId, out var contacts) ? contacts.Values.ToArray() : Array.Empty<ScannerContactState>();
}
