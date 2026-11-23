using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// Direct user request ("ближе к качеству Баротравмы", item 2) - machines are light sources too, not just
// the room lamps: the reactor throws its own teal (red when the plasma runs hot) across the compartment,
// consoles and terminals wash the deck in front of them with screen light, and the power boxes leave a
// small amber pool. These go into the same RoomLighting mask as the lamps, so they are shadowed by the
// walls and, being multiplied over the scene before the post chain's bright pass, are what makes a lit
// screen or the reactor window actually bloom. They follow the power, not the lights lever - a screen
// glows with the room lamps switched off, which is exactly when it matters.
public partial class Game1
{
    private static readonly Color ReactorLightTeal = new(70, 215, 200);
    private static readonly Color ReactorLightHot = new(255, 70, 50);
    private static readonly Color ScreenLight = new(120, 196, 236);
    private static readonly Color TerminalLight = new(96, 214, 170);
    private static readonly Color PanelLight = new(255, 176, 84);

    private static IEnumerable<PointLight> BuildDeviceLights(WorldSnapshot snapshot, float totalSeconds)
    {
        var powered = snapshot.Power.ReactorOutput > 0.01f || snapshot.Power.BatteryCharge > 0.01f;
        if (!powered)
            yield break;

        // Same load/heat curve as the reactor's own glow mask (ShipRenderer.DrawReactorBlock).
        if (snapshot.Reactor.CurrentOutput > 0f)
        {
            var load = MathHelper.Clamp(snapshot.Reactor.CurrentOutput / Math.Max(1f, snapshot.Reactor.MaxOutput), 0f, 1f);
            var heat = MathHelper.Clamp((snapshot.Power.PlasmaTemperature - 125f) / 25f, 0f, 1f);
            var flicker = 0.95f + 0.05f * MathF.Sin(totalSeconds * 13f) * MathF.Sin(totalSeconds * 3.1f);
            var pulse = 1f - heat * 0.25f * (0.5f + 0.5f * MathF.Sin(totalSeconds * 5f));
            var tone = Color.Lerp(ReactorLightTeal, ReactorLightHot, heat);
            foreach (var block in new[] { snapshot.ReactorBlock }.Concat(snapshot.ExtraReactors ?? Array.Empty<Anabiosis.Shared.Model.ReactorBlock>()))
                yield return new PointLight(new Vector2(block.X, block.Y), 6.5f * block.SizeScale, tone * ((0.35f + 0.5f * load) * flicker * pulse));
        }

        foreach (var position in Consoles(snapshot))
            yield return new PointLight(position, 2.6f, ScreenLight * 0.42f);
        foreach (var terminal in snapshot.Terminals ?? Array.Empty<TerminalState>())
            if (terminal.On)
                yield return new PointLight(new Vector2(terminal.Block.X, terminal.Block.Y), 2.1f, TerminalLight * 0.3f);
        yield return new PointLight(new Vector2(snapshot.DistributionBlock.X, snapshot.DistributionBlock.Y), 1.7f, PanelLight * 0.22f);
        yield return new PointLight(new Vector2(snapshot.BatteryBlock.X, snapshot.BatteryBlock.Y), 1.7f, PanelLight * 0.18f);
        foreach (var extraDistribution in snapshot.ExtraDistributions ?? Array.Empty<Anabiosis.Shared.Model.PowerDistributionBlock>())
            yield return new PointLight(new Vector2(extraDistribution.X, extraDistribution.Y), 1.7f, PanelLight * 0.22f);
        foreach (var extraBattery in snapshot.ExtraBatteries ?? Array.Empty<Anabiosis.Shared.Model.BatteryBlock>())
            yield return new PointLight(new Vector2(extraBattery.X, extraBattery.Y), 1.7f, PanelLight * 0.18f);
        // The workshop machines (MachineArt.Workshop.cs): the fabricator's job screen, the deconstructor's hot intake.
        foreach (var device in snapshot.DecorativeDevices ?? Array.Empty<Anabiosis.Shared.Model.DecorativeDevice>())
        {
            if (device.Kind == Anabiosis.Shared.Model.CustomDeviceKind.Fabricator)
                yield return new PointLight(new Vector2(device.X, device.Y), 2.4f, ScreenLight * 0.34f);
            else if (device.Kind == Anabiosis.Shared.Model.CustomDeviceKind.Deconstructor)
                yield return new PointLight(new Vector2(device.X, device.Y), 2.6f, new Color(255, 130, 60) * 0.42f);
        }
    }

    private static IEnumerable<Vector2> Consoles(WorldSnapshot snapshot)
    {
        yield return new Vector2(snapshot.HelmConsole.X, snapshot.HelmConsole.Y);
        yield return new Vector2(snapshot.NavigationConsole.X, snapshot.NavigationConsole.Y);
        foreach (var c in snapshot.ExtraHelmConsoles ?? Enumerable.Empty<Anabiosis.Shared.Model.HelmConsole>())
            yield return new Vector2(c.X, c.Y);
        foreach (var c in snapshot.ExtraNavigationConsoles ?? Enumerable.Empty<Anabiosis.Shared.Model.NavigationConsole>())
            yield return new Vector2(c.X, c.Y);
        foreach (var c in snapshot.ShipStatusMonitors ?? Enumerable.Empty<Anabiosis.Shared.Model.ShipStatusMonitor>())
            yield return new Vector2(c.X, c.Y);
        foreach (var c in snapshot.CommsConsoles ?? Enumerable.Empty<Anabiosis.Shared.Model.CommsConsole>())
            yield return new Vector2(c.X, c.Y);
    }
}
