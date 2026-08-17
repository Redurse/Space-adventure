using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Draws the component/pin/wire graph (World.Wiring.cs/World.ComponentLogic.cs, M19-M23) directly in
// the ship interior - static methods with an explicit pixel texture, mirroring
// FieldRenderer.DrawCuttingFlame's shape, since this only ever renders inside ShipRenderer's own
// scene (components only exist aboard the player's own ship). Replaces WiringPanel's abstract
// schematic entirely: the physical scene *is* the schematic now, so there's no second, contradicting
// way to show the same data.
public static class ComponentRenderer
{
    private const int MountSize = 16;
    private const int PinSize = 6;

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, WorldSnapshot snapshot, Vector2 origin, float totalSeconds)
    {
        DrawWires(spriteBatch, pixel, snapshot, origin);
        DrawJunctions(spriteBatch, pixel, font, snapshot, origin);
        DrawPowerPins(spriteBatch, pixel, snapshot, origin);
        DrawMounts(spriteBatch, pixel, font, snapshot, origin);
        DrawWiresInProgress(spriteBatch, pixel, snapshot, origin, totalSeconds);
    }

    // Junction boxes never had a visual before now (WireNode's old abstract schematic position
    // didn't correspond to anything physical) - a small panel, one per system this hull actually
    // has a device for, positioned by WireGraphFactory right next to Distribution.
    private static void DrawJunctions(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, WorldSnapshot snapshot, Vector2 origin)
    {
        foreach (var junction in snapshot.Components.Where(c => c.Kind == ComponentKind.Junction))
        {
            var rect = ShipRenderer.GetBlockRect(junction.Position, ShipRenderer.NormalBlockSize, origin);
            ShipRenderer.DrawPanel(spriteBatch, pixel, rect, Color.SlateGray * 0.7f, Color.LightSteelBlue, 1);
            spriteBatch.DrawString(font, "Кор", new Vector2(rect.X + 1, rect.Y + 4), Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }
    }

    // Pin stubs on the built-in power backbone (Distribution/Junction/Device) - these already have
    // their own body rendering (or, for Junction, the one just above), this only adds the little
    // connector squares wire-laying actually targets.
    private static void DrawPowerPins(SpriteBatch spriteBatch, Texture2D pixel, WorldSnapshot snapshot, Vector2 origin)
    {
        foreach (var component in snapshot.Components.Where(c => c.Kind is ComponentKind.Distribution or ComponentKind.Junction or ComponentKind.Device))
        {
            var size = FootprintSize(component, snapshot);
            var rect = ShipRenderer.GetBlockRect(component.Position, size, origin);
            foreach (var (pinId, kind, index, total) in PowerPinLayout(component, snapshot))
                DrawPinStub(spriteBatch, pixel, PinPosition(rect, kind, index, total), kind);
        }
    }

    private static int FootprintSize(Component component, WorldSnapshot snapshot) => component.Kind switch
    {
        ComponentKind.Distribution => ShipRenderer.MediumBlockSize,
        ComponentKind.Device => snapshot.SystemDevices.FirstOrDefault(d => d.Id == component.Id)?.System == PowerSystemId.Engine
            ? ShipRenderer.BigBlockSize
            : ShipRenderer.NormalBlockSize,
        _ => ShipRenderer.NormalBlockSize, // Junction
    };

    // Which pins a built-in power component actually has right now - Distribution/Junction are
    // hull-dependent (ComponentDefinitions.PinsFor deliberately doesn't cover them), so their pins
    // are derived here the same way WireGraphFactory derived them when building the graph.
    private static IEnumerable<(string PinId, PinKind Kind, int Index, int Total)> PowerPinLayout(Component component, WorldSnapshot snapshot)
    {
        if (component.Kind == ComponentKind.Device)
        {
            yield return ("in", PinKind.PowerIn, 0, 1);
            yield break;
        }

        if (component.Kind == ComponentKind.Distribution)
        {
            var systems = Enum.GetValues<PowerSystemId>();
            for (var i = 0; i < systems.Length; i++)
                yield return ($"out-{systems[i]}".ToLowerInvariant(), PinKind.PowerOut, i, systems.Length);
            yield break;
        }

        // Junction: one input, one output per device on its system - id encodes which system.
        var system = Enum.GetValues<PowerSystemId>().FirstOrDefault(s => component.Id == $"junction-{s}".ToLowerInvariant());
        yield return ("in", PinKind.PowerIn, 0, 1);
        var deviceCount = snapshot.SystemDevices.Count(d => d.System == system);
        for (var i = 0; i < deviceCount; i++)
            yield return ($"out-{i}", PinKind.PowerOut, i, deviceCount);
    }

    // Empty gnездо - a thin outline, nothing installed yet; occupied - the same beveled panel every
    // other block uses, colored by category, with a short Latin code (already mixes with Cyrillic
    // elsewhere in this HUD, e.g. "O2" on the oxygen tank).
    private static void DrawMounts(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, WorldSnapshot snapshot, Vector2 origin)
    {
        foreach (var mount in snapshot.ComponentMounts)
        {
            var rect = ShipRenderer.GetBlockRect(mount.Position, MountSize, origin);
            var installedId = snapshot.ComponentMountStates.FirstOrDefault(s => s.MountId == mount.Id)?.InstalledComponentId;
            var installed = installedId is null ? null : snapshot.Components.FirstOrDefault(c => c.Id == installedId);

            if (installed is null)
            {
                ShipRenderer.DrawRectOutline(spriteBatch, pixel, rect, Color.Gray * 0.6f, 1);
                continue;
            }

            var (fill, border) = CategoryColors(installed.Kind);
            ShipRenderer.DrawPanel(spriteBatch, pixel, rect, fill, border, 1);
            var label = ComponentDefinitions.ShortLabel(installed.Kind);
            var size = font.MeasureString(label) * 0.4f;
            spriteBatch.DrawString(font, label, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f),
                Color.White, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);

            foreach (var (_, kind, index, total) in SignalPinLayout(installed.Kind))
                DrawPinStub(spriteBatch, pixel, PinPosition(rect, kind, index, total), kind);
        }
    }

    private static IEnumerable<(string PinId, PinKind Kind, int Index, int Total)> SignalPinLayout(ComponentKind kind)
    {
        var pins = ComponentDefinitions.PinsFor(kind);
        var inputs = pins.Where(p => p.Kind is PinKind.SignalIn or PinKind.PowerIn).ToList();
        var outputs = pins.Where(p => p.Kind is PinKind.SignalOut or PinKind.PowerOut).ToList();
        for (var i = 0; i < inputs.Count; i++)
            yield return (inputs[i].Id, inputs[i].Kind, i, inputs.Count);
        for (var i = 0; i < outputs.Count; i++)
            yield return (outputs[i].Id, outputs[i].Kind, i, outputs.Count);
    }

    private static Vector2 PinPosition(Rectangle rect, PinKind kind, int index, int total)
    {
        var isInput = kind is PinKind.PowerIn or PinKind.SignalIn;
        var y = rect.Y + rect.Height * (index + 1) / (total + 1);
        return new Vector2(isInput ? rect.X : rect.Right, y);
    }

    private static void DrawPinStub(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, PinKind kind)
    {
        var color = kind is PinKind.PowerIn or PinKind.PowerOut ? Color.Gold : Color.CadetBlue;
        spriteBatch.Draw(pixel, new Rectangle((int)position.X - PinSize / 2, (int)position.Y - PinSize / 2, PinSize, PinSize), color);
    }

    private static (Color Fill, Color Border) CategoryColors(ComponentKind kind) => kind switch
    {
        ComponentKind.GateAnd or ComponentKind.GateOr or ComponentKind.GateNot or ComponentKind.GateXor => (Color.SlateBlue * 0.7f, Color.SlateBlue),
        ComponentKind.Timer or ComponentKind.Memory => (Color.Purple * 0.6f, Color.MediumPurple),
        ComponentKind.Relay => (Color.DarkGoldenrod * 0.6f, Color.Gold),
        ComponentKind.OxygenSensor or ComponentKind.BreachSensor or ComponentKind.PowerLossSensor or ComponentKind.MotionSensor => (Color.SeaGreen * 0.6f, Color.LightSeaGreen),
        ComponentKind.AutoDoorController or ComponentKind.AlarmKlaxon or ComponentKind.LightToggle => (Color.DarkOrange * 0.6f, Color.Goldenrod),
        _ => (Color.DimGray * 0.6f, Color.LightGray),
    };

    // Shared by drawing and by Game1's hit-testing, so a click always lands on exactly the pin (or
    // mount body) it looks like it should - the same "one function serves both" convention
    // ShipRenderer.GetBlockRect already establishes.
    public static Rectangle GetMountBodyRect(ComponentMount mount, Vector2 origin) => ShipRenderer.GetBlockRect(mount.Position, MountSize, origin);

    private const int PinHitSize = 10;

    public static Rectangle? GetPinRect(WorldSnapshot snapshot, PinRef pin, Vector2 origin)
    {
        var position = ResolvePinScreenPosition(snapshot, pin, origin);
        if (position is null)
            return null;
        return new Rectangle((int)position.Value.X - PinHitSize / 2, (int)position.Value.Y - PinHitSize / 2, PinHitSize, PinHitSize);
    }

    // Every pin that currently exists, with its hit rect - Game1 just looks for the first one
    // containing the cursor rather than re-deriving pin layout itself.
    public static IEnumerable<(PinRef Pin, Rectangle Rect)> AllPinHitRects(WorldSnapshot snapshot, Vector2 origin)
    {
        foreach (var component in snapshot.Components)
        {
            foreach (var (pinId, _) in PinsFor(component, snapshot))
            {
                var pinRef = new PinRef(component.Id, pinId);
                if (GetPinRect(snapshot, pinRef, origin) is { } rect)
                    yield return (pinRef, rect);
            }
        }
    }

    // Shared by AllPinHitRects and ConnectionsPanel (screwdriver "open the panel" view) - the same
    // built-in-vs-purchased split PowerPinLayout/SignalPinLayout already encode.
    internal static IEnumerable<(string PinId, PinKind Kind)> PinsFor(Component component, WorldSnapshot snapshot) =>
        component.Kind is ComponentKind.Distribution or ComponentKind.Junction or ComponentKind.Device
            ? PowerPinLayout(component, snapshot).Select(p => (p.PinId, p.Kind))
            : SignalPinLayout(component.Kind).Select(p => (p.PinId, p.Kind));

    // A short, readable name for a pin's owner - built-in power components don't have a purchasable
    // ComponentKind label, so they're special-cased the same way DrawJunctions'/DrawPowerPins' own
    // labels are. Shared by Game1's WiringHint and ConnectionsPanel so a pin reads the same way
    // everywhere it's named.
    internal static string PinLabel(WorldSnapshot snapshot, PinRef pin)
    {
        var owner = snapshot.Components.FirstOrDefault(c => c.Id == pin.ComponentId);
        if (owner is null)
            return pin.PinId;
        var name = owner.Kind switch
        {
            ComponentKind.Distribution => "шина",
            ComponentKind.Junction => "коробка",
            ComponentKind.Device => "устройство",
            _ => ComponentDefinitions.ShortLabel(owner.Kind),
        };
        return $"{name}.{pin.PinId}";
    }

    // Full display name for a component's own header line in ConnectionsPanel - unlike PinLabel
    // (short, inline with a pin id) this names the component itself, and looks up the actual system
    // for a Device rather than just saying "устройство".
    internal static string ComponentLabel(WorldSnapshot snapshot, string componentId)
    {
        var component = snapshot.Components.FirstOrDefault(c => c.Id == componentId);
        if (component is null)
            return componentId;
        return component.Kind switch
        {
            ComponentKind.Distribution => "Распределительный блок",
            ComponentKind.Junction => "Распределительная коробка",
            ComponentKind.Device => snapshot.SystemDevices.FirstOrDefault(d => d.Id == componentId) is { } device
                ? $"Устройство: {SystemLabel(device.System)}"
                : "Устройство",
            _ => ComponentDefinitions.DisplayName(component.Kind),
        };
    }

    private static string SystemLabel(PowerSystemId system) => system switch
    {
        PowerSystemId.Oxygen => "Кислород",
        PowerSystemId.Engine => "Двигатель",
        PowerSystemId.Shields => "Щиты",
        PowerSystemId.WeaponCharger => "Орудия",
        PowerSystemId.Secondary => "Прочее",
        _ => system.ToString(),
    };

    // Endpoint screen position for any component, built-in or purchased - resolves through the mount
    // for a purchased one (its footprint is MountSize) or through the power layout for the backbone.
    private static Vector2? ResolvePinScreenPosition(WorldSnapshot snapshot, PinRef pin, Vector2 origin)
    {
        var component = snapshot.Components.FirstOrDefault(c => c.Id == pin.ComponentId);
        if (component is null)
            return null;

        if (component.Kind is ComponentKind.Distribution or ComponentKind.Junction or ComponentKind.Device)
        {
            var rect = ShipRenderer.GetBlockRect(component.Position, FootprintSize(component, snapshot), origin);
            var match = PowerPinLayout(component, snapshot).FirstOrDefault(p => p.PinId == pin.PinId);
            return match.PinId is null ? null : PinPosition(rect, match.Kind, match.Index, match.Total);
        }

        var mountRect = ShipRenderer.GetBlockRect(component.Position, MountSize, origin);
        var signalMatch = SignalPinLayout(component.Kind).FirstOrDefault(p => p.PinId == pin.PinId);
        return signalMatch.PinId is null ? null : PinPosition(mountRect, signalMatch.Kind, signalMatch.Index, signalMatch.Total);
    }

    private static void DrawWires(SpriteBatch spriteBatch, Texture2D pixel, WorldSnapshot snapshot, Vector2 origin)
    {
        foreach (var wire in snapshot.Wires)
        {
            var from = ResolvePinScreenPosition(snapshot, wire.FromPin, origin);
            var to = ResolvePinScreenPosition(snapshot, wire.ToPin, origin);
            if (from is null || to is null)
                continue;

            var state = snapshot.WireStates.FirstOrDefault(s => s.WireId == wire.Id);
            var live = snapshot.ComponentStates.FirstOrDefault(s => s.ComponentId == wire.FromPin.ComponentId)?.SignalValue ?? true;
            var color = state?.Damaged == true ? Color.OrangeRed : live ? Color.LimeGreen : Color.DimGray;
            DrawLine(spriteBatch, pixel, from.Value, to.Value, color, 2);
        }
    }

    // The wire currently being laid, from its anchor pin to wherever that character stands right
    // now - own player or anyone else in co-op (CharacterState.LayingWireFromPin), the same
    // anchor-to-moving-point technique FieldRenderer.DrawToolFlame uses, just without an aim
    // direction: this one just tracks a position.
    private static void DrawWiresInProgress(SpriteBatch spriteBatch, Texture2D pixel, WorldSnapshot snapshot, Vector2 origin, float totalSeconds)
    {
        foreach (var character in snapshot.Characters.Where(c => c.LayingWireFromPin is not null && !c.IsOutside && !c.OnStation && !c.OnEnemyShip))
        {
            var anchor = ResolvePinScreenPosition(snapshot, character.LayingWireFromPin!.Value, origin);
            if (anchor is null)
                continue;
            var flicker = 0.6f + 0.4f * MathF.Sin(totalSeconds * 8f);
            var characterPoint = origin + new Vector2(character.X, character.Y) * ShipRenderer.PixelsPerUnit;
            DrawLine(spriteBatch, pixel, anchor.Value, characterPoint, Color.CadetBlue * flicker, 2);
        }
    }

    private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, Vector2 to, Color color, int thickness)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length < 0.5f)
            return;
        var rotation = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(pixel, from, null, color, rotation, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }
}
