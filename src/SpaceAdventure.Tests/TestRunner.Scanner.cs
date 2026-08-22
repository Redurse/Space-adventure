using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // MoveCharacterTo's bang-bang drives X and Y toward the final target at the same time, which
    // drifts off a door's own narrow passable band (every door on this hull sits at Y=3, Door.
    // StandardSpanUnits(2) wide - Ship.cs) long before the crossing X is reached, walling the
    // character off between rooms instead of ever reaching the console. Detouring through a
    // waypoint held at that same Y=3 first crosses every door dead-on, THEN the final short hop is
    // wholly inside the cockpit with nothing left in the way.
    private static void MoveToNavigationConsole(World world, int playerId = 1)
    {
        var console = world.Ship.NavigationConsole.Position;
        MoveCharacterTo(world, playerId, 3f, 3f);
        MoveCharacterTo(world, playerId, console.X, console.Y);
    }

    // Placed a fixed, known distance/bearing from a real ambient hull (World.NpcShips.cs) instead
    // of trusting wherever it happened to spawn - the cone/range math should be exercised the same
    // way every run, not left to whether this test's own setup happened to land within range.
    private static bool World_Scanner_SweepFindsShipInsideConeNotOutside()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.Step(RealtimeStep); // populates this system's ambient fleet

        // Walked to the console BEFORE reading the target's position, not after - the walk itself
        // takes many real ticks (MoveToNavigationConsole's own bang-bang), and NpcShipRuntime.Cargo
        // keeps moving along its route the whole time regardless of what the character is doing;
        // capturing a position first and only using it once the walk was already done left it
        // stale by however far the hull had drifted since, which used to be forgivable by luck but
        // stopped being once the walk itself got longer (M47 moving the console further from the
        // spawn point/door waypoint).
        MoveToNavigationConsole(world);

        var npc = world.CreateSnapshot().NpcShips.First();
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        world.Step(RealtimeStep);
        // Due south of it (bearing 90 degrees), not due west (bearing 0) - 0 is ClientCommand's own
        // default ScannerSweepDegrees, which every plain movement command below implicitly carries
        // while walking to the console, so a target actually AT bearing 0 would get "found" as a
        // side effect of just approaching, before this test ever sends its own aim.
        world.DebugPlaceShip(new Vec2(npc.X, npc.Y - 100f));

        // Aimed 90 degrees off (the movement-command default bearing, dead west) must not find it.
        world.ApplyCommand(1, new ClientCommand(1, ScannerSweepDegrees: 0f));
        world.Step(RealtimeStep);
        var missed = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).ScannerContacts;
        if (missed is not null && missed.Any(c => c.Id == npc.Id))
            return false; // found it while aimed well outside the sweep cone

        // Swept onto its actual bearing (dead south, 90 degrees) must find it.
        world.ApplyCommand(1, new ClientCommand(1, ScannerSweepDegrees: 90f));
        world.Step(RealtimeStep);
        var found = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).ScannerContacts;
        return found is not null && found.Any(c => c.Id == npc.Id);
    }

    // A hull that wanders back out of the cone stays on the operator's own screen at wherever it
    // was last actually seen (game_design.md/M44 - "в последней известной точке"), not vanishing
    // the instant the sweep moves past it again.
    private static bool World_Scanner_ContactStaysAtLastKnownPositionAfterLeavingTheCone()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);

        // Walked to the console before reading the target's position - see the sibling test's own
        // doc comment above for why that order actually matters here.
        MoveToNavigationConsole(world);

        var npc = world.CreateSnapshot().NpcShips.First();
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        world.DebugPlaceShip(new Vec2(npc.X, npc.Y - 100f)); // bearing 90, not the movement-default 0

        world.ApplyCommand(1, new ClientCommand(1, ScannerSweepDegrees: 90f));
        world.Step(RealtimeStep);
        var firstSeen = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).ScannerContacts?
            .FirstOrDefault(c => c.Id == npc.Id);
        if (firstSeen is null)
            return false; // setup problem - didn't even find it the first time

        // Swing well away and let a few seconds pass - a real hull might drift, but the contact
        // record must not, since nothing has re-swept it since.
        world.ApplyCommand(1, new ClientCommand(1, ScannerSweepDegrees: 180f));
        for (var i = 0; i < 5 * 30; i++)
            world.Step(RealtimeStep);

        var stillThere = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).ScannerContacts?
            .FirstOrDefault(c => c.Id == npc.Id);
        return stillThere is not null && stillThere.X == firstSeen.X && stillThere.Y == firstSeen.Y;
    }

    // Far from the console entirely (still holding whatever sweep angle it last had), the operator
    // stops finding anything new - a scanner needs someone physically standing at it.
    private static bool World_Scanner_DoesNothingAwayFromTheConsole()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);

        var npc = world.CreateSnapshot().NpcShips.First();
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        world.DebugPlaceShip(new Vec2(npc.X - 100f, npc.Y));

        // Nowhere near the console (still at the ship's spawn point) - sending the exact right
        // bearing must still find nothing.
        world.ApplyCommand(1, new ClientCommand(1, ScannerSweepDegrees: 0f));
        world.Step(RealtimeStep);

        var contacts = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).ScannerContacts;
        return contacts is null || contacts.Count == 0;
    }

    // A Scientist's own private find only reaches the shared map once they deliberately put it
    // there (game_design.md/M44) - the marker itself carries no identity beyond the point, so any
    // crew member sees the same pin regardless of who placed it or who's currently scanning.
    private static bool World_Scanner_PlacingMarkerAddsItToTheSharedMap()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveToNavigationConsole(world);

        var before = world.CreateSnapshot().ManualScannerMarkers.Count;
        world.ApplyCommand(1, new ClientCommand(1, PlaceScannerMarkerAtX: 42f, PlaceScannerMarkerAtY: 99f));
        world.Step(RealtimeStep);

        var markers = world.CreateSnapshot().ManualScannerMarkers;
        return markers.Count == before + 1 && markers.Any(m => m.X == 42f && m.Y == 99f);
    }

    // Save/load (game_design.md section 5) must not quietly drop a marker the crew already placed.
    private static bool World_Scanner_ManualMarkerSurvivesSaveAndLoad()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveToNavigationConsole(world);
        world.ApplyCommand(1, new ClientCommand(1, PlaceScannerMarkerAtX: 12f, PlaceScannerMarkerAtY: 34f));
        world.Step(RealtimeStep);

        var save = world.CreateSave();
        var reloaded = new World();
        reloaded.SpawnCharacter(1);
        reloaded.ApplySave(save);

        return reloaded.CreateSnapshot().ManualScannerMarkers.Any(m => m.X == 12f && m.Y == 34f);
    }
}
