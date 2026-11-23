using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The fight's feedback (EffectTracker.Combat.cs, CameraShake): a shell on the plating flashes and shakes the screen, the slow damage of
// fire does neither, a firing gun kicks the view, and the shake dies away on its own. Plus the extra reactors and distribution boxes
// reaching the client (they used to be counted for the bonus but never drawn, like the extra batteries before them).
internal static partial class TestRunner
{
    // The world's own snapshot, with its first wall plate knocked down by `damage` hit points.
    private static (WorldSnapshot Before, WorldSnapshot After) SnapshotsAroundAPlatingHit(float damage)
    {
        var before = new World().CreateSnapshot();
        var first = before.WallBlockStates.First(s => s.Hp > damage);
        var after = before with
        {
            WallBlockStates = before.WallBlockStates.Select(s => s.Id == first.Id ? s with { Hp = s.Hp - damage } : s).ToList(),
        };
        return (before, after);
    }

    private static bool EffectTracker_PlatingHit_FlashesAndShakesTheScreen()
    {
        var (before, after) = SnapshotsAroundAPlatingHit(25f);
        var tracker = new EffectTracker();
        tracker.Detect(before, after);
        return tracker.Effects.Any(e => e.Kind == EffectKind.HullHit) && tracker.Shake.Trauma > 0.1f;
    }

    private static bool EffectTracker_SlowPlatingDamage_StaysQuiet()
    {
        var (before, after) = SnapshotsAroundAPlatingHit(1f);
        var tracker = new EffectTracker();
        tracker.Detect(before, after);
        return tracker.Effects.All(e => e.Kind != EffectKind.HullHit) && tracker.Shake.Trauma == 0f;
    }

    private static bool EffectTracker_FriendlyShellLeavingTheBarrel_KicksTheView()
    {
        var before = new World().CreateSnapshot();
        var fired = before with { Projectiles = new[] { new ProjectileState("shell-1", 10, 10, 0f, FromEnemy: false, IsLaser: false) } };
        var theirs = before with { Projectiles = new[] { new ProjectileState("shell-2", 10, 10, 0f, FromEnemy: true, IsLaser: false) } };

        var ours = new EffectTracker();
        ours.Detect(before, fired);
        var hostile = new EffectTracker();
        hostile.Detect(before, theirs);
        // The same shell seen again is not a new shot.
        var again = new EffectTracker();
        again.Detect(fired, fired);
        return ours.Shake.Trauma > 0f && hostile.Shake.Trauma == 0f && again.Shake.Trauma == 0f;
    }

    private static bool CameraShake_BuildsWithBlowsAndDrainsAway()
    {
        var shake = new CameraShake();
        shake.Add(0.4f);
        shake.Add(0.4f);
        var built = shake.Trauma >= 0.79f && shake.Offset().Length() > 0f;
        shake.Step(3f);
        return built && shake.Trauma == 0f && shake.Offset() == Microsoft.Xna.Framework.Vector2.Zero;
    }

    private static bool CameraShake_CanBeSwitchedOff()
    {
        var shake = new CameraShake { Strength = 0f };
        shake.Add(1f);
        return shake.Offset() == Microsoft.Xna.Framework.Vector2.Zero;
    }

    private static bool Snapshot_ExtraReactorsAndDistributionBoxes_ReachTheClient()
    {
        var def = BuildSimpleCustomShipDefinition();
        var withExtras = def with
        {
            Devices = def.Devices
                .Append(new CustomDeviceDef(CustomDeviceKind.Reactor, 3, 1))
                .Append(new CustomDeviceDef(CustomDeviceKind.Distribution, 3, 2))
                .ToList(),
        };
        var snapshot = new World(ShipKind.Custom, withExtras).CreateSnapshot();
        // (the start station's own reactor and box join these lists while docked - only the ship's count here)
        var ownReactors = (snapshot.ExtraReactors ?? Array.Empty<ReactorBlock>()).Where(r => !r.Id.StartsWith(Station.StartStationPointId)).ToList();
        var ownBoxes = (snapshot.ExtraDistributions ?? Array.Empty<PowerDistributionBlock>()).Where(b => !b.Id.StartsWith(Station.StartStationPointId)).ToList();
        return ownReactors is { Count: 1 } reactors && reactors[0].Id != snapshot.ReactorBlock.Id
            && ownBoxes is { Count: 1 } boxes && boxes[0].Id != snapshot.DistributionBlock.Id;
    }
}
