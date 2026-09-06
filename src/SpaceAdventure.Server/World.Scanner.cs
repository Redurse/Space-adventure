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
    // A narrow directional pulse, not an omnidirectional radar - reading the bearing off the dial
    // actually matters. Half-width, not full width: the cone spans SweepHalfAngle either side of
    // ScannerSweepDegrees.
    private const float ScannerSweepHalfAngleDegrees = ScannerConstants.SweepHalfAngleDegrees;
    // Comfortably short of the field's own full extent (2400) - a pulse that already covered the
    // whole system from any one spot would make standing at the console strictly better than
    // flying to look, which isn't the point of a director­al sensor. 900 * 1.2 (M48 follow-up -
    // "лучевой сканер сканировал на 20 процентов дальше").
    private const float ScannerRangeUnits = ScannerConstants.RangeUnits;
    // The console's own toggle switch (M48 follow-up - "круговой... просвечивает область в 2 раза
    // меньше, но зато по кругу"): trading range for all-around coverage, not free coverage.
    private const float CircularScannerRangeUnits = ScannerRangeUnits / 2f;
    // M47 follow-up - "с перезарядкой... 15 секунд": aiming the dial is still free and continuous,
    // but the actual detecting pulse is a discrete, cooldown-gated action rather than a permanent
    // sweep - a placeholder value the design brief itself called out as provisional.
    public const float ScannerPingCooldownSeconds = ScannerConstants.PingCooldownSeconds;

    // Combat damage (World.EnemyAi.cs's ApplyEnemyAttack, enemy/weapon overhaul - "сонар можно
    // было сломать") - a wrecked console answers to nobody until repaired (World.SystemRepair.cs);
    // HandleScannerInput below refuses both the dial and the ping while this is true.
    public bool NavigationConsoleBroken { get; set; }

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
    // own levers use (World.cs), rather than a separate seated/toggle state. ScannerPingPressed
    // fires the actual detecting pulse (M47 follow-up) - aiming the dial itself stays free, but the
    // pulse only fires once the cooldown has actually run out.
    private void HandleScannerInput(Character character, ClientCommand command)
    {
        // Content-каталог отсеков - an extra bridge room's own nav seat works the same as the primary
        // console (Ship.cs's own doc comment on ExtraNavigationConsoles), same "just distance, no
        // room check" convention this proximity test already used before.
        var nearAnyNavigationConsole = (!NavigationConsoleBroken && (Ship.NavigationConsole.Position - character.Position).Length() < InteractionRadius)
            || Ship.ExtraNavigationConsoles.Any(c => (c.Position - character.Position).Length() < InteractionRadius);
        if (!nearAnyNavigationConsole)
            return;

        character.ScannerSweepDegrees = command.ScannerSweepDegrees;
        character.ScannerMode = command.RequestedScannerMode;

        if (command.PlaceScannerMarkerAtX is { } markerX && command.PlaceScannerMarkerAtY is { } markerY)
            _manualScannerMarkers.Add(new Vec2(markerX, markerY));

        if (command.ScannerPingPressed && character.ScannerCooldownRemaining <= 0f)
            FireScannerPing(character);
    }

    // A single sonar-style pulse along the dial's current bearing - everything within the cone and
    // in range is revealed at once (their position the instant of this pulse, not tracked live
    // afterward), then the cooldown starts. Replaces the old "detects continuously while aimed"
    // behavior (M47 follow-up - "не постоянным сканером а с перезарядкой").
    private void FireScannerPing(Character character)
    {
        character.ScannerCooldownRemaining = ScannerPingCooldownSeconds;

        var contacts = _scannerContactsByPlayer.TryGetValue(character.PlayerId, out var existing)
            ? existing
            : _scannerContactsByPlayer[character.PlayerId] = new Dictionary<string, ScannerContactState>();

        var isCircular = character.ScannerMode == ScannerMode.Circular;
        var range = isCircular ? CircularScannerRangeUnits : ScannerRangeUnits;

        foreach (var npc in _npcShips)
        {
            var toNpc = npc.Position - _shipFieldPosition;
            if (toNpc.Length() > range)
                continue;

            if (!isCircular)
            {
                var bearing = MathF.Atan2((float)toNpc.Y, (float)toNpc.X) * (180f / MathF.PI);
                var offBearing = MathF.Abs(ShortestAngle(bearing - character.ScannerSweepDegrees));
                if (offBearing > ScannerSweepHalfAngleDegrees)
                    continue;
            }

            contacts[npc.Id] = new ScannerContactState(
                npc.Id, npc.Kind, npc.FactionId, npc.Position.X, npc.Position.Y, npc.RotationDegrees, Tick);
        }
    }

    // Ticks every character's own personal cooldown down, regardless of where they currently are -
    // walking away from the console no longer matters to this timer either way, since a ping can
    // only ever be triggered from right there in the first place (HandleScannerInput's own gate).
    private void StepScanners(double deltaSeconds)
    {
        foreach (var character in _characters.Values)
        {
            if (character.ScannerCooldownRemaining > 0f)
                character.ScannerCooldownRemaining = MathF.Max(0f, character.ScannerCooldownRemaining - (float)deltaSeconds);
        }
    }

    private IReadOnlyList<ScannerContactState> CreateScannerContacts(int playerId) =>
        _scannerContactsByPlayer.TryGetValue(playerId, out var contacts) ? contacts.Values.ToArray() : Array.Empty<ScannerContactState>();
}
