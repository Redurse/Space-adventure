using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client;

// The waiting room's list of ships and the little picture of the chosen one. The names and the figures worked out from each design are cached for a few
// seconds: the list is read from disk, and the lobby draws sixty times a second.
public partial class Game1
{
    private const string LobbyDefaultShipLabel = "По умолчанию";
    private const long LobbyShipCacheMilliseconds = 3000;

    private sealed record LobbyShipInfo(CustomShipDefinition Definition, int Rooms, int Devices, int Engines, int Weapons, int Quarter);

    private IReadOnlyList<string>? _lobbyShipNames;
    private long _lobbyShipCacheAt;
    private readonly Dictionary<string, LobbyShipInfo?> _lobbyShipInfo = new();

    private IReadOnlyList<string> LobbyShipNames()
    {
        var now = Environment.TickCount64;
        if (_lobbyShipNames is null || now - _lobbyShipCacheAt > LobbyShipCacheMilliseconds)
        {
            _lobbyShipNames = PlayableCustomShipNames();
            _lobbyShipInfo.Clear();
            _lobbyShipCacheAt = now;
        }
        return _lobbyShipNames;
    }

    // What the list shows, top to bottom; null is the default ship, which is only there while it is the one in use (once the host picks a drawn ship the
    // default cannot be picked back, so it is not offered).
    private List<string?> LobbyShipRows(LobbySnapshot lobby)
    {
        var rows = new List<string?>();
        if (lobby.CustomShipName is null)
            rows.Add(null);
        rows.AddRange(LobbyShipNames());
        return rows;
    }

    private LobbyShipInfo? LobbyShipInfoFor(string name)
    {
        LobbyShipNames(); // refreshes the cache when it is old
        if (_lobbyShipInfo.TryGetValue(name, out var cached))
            return cached;
        LobbyShipInfo? info = null;
        if (LoadPlayableCustomShip(name) is { } definition && definition.Rooms.Count > 0)
        {
            var devices = definition.Devices;
            var engines = (definition.EnginesRaw?.Count ?? 0) + devices.Count(d => d.Kind == CustomDeviceKind.Engine);
            var weapons = devices.Count(d => d.Kind is CustomDeviceKind.TurretBallistic or CustomDeviceKind.TurretLaser or CustomDeviceKind.TurretMachineGun);
            // The ship's nose is drawn pointing right: the design is turned by the quarter-turns that take its forward direction there.
            var quarter = (int)MathF.Round(definition.ForwardDegrees / 90f) % 4;
            info = new LobbyShipInfo(definition, definition.Rooms.Count, devices.Count, engines, weapons, (quarter + 4) % 4);
        }
        _lobbyShipInfo[name] = info;
        return info;
    }

    // ---- drawing ----

    private void DrawLobbyShipList(LobbySnapshot lobby, bool isHost)
    {
        DrawLobbyHeader("КОРАБЛЬ", LobbyShipListBox.X, LobbyShipListBox.Y - 18);
        if (!isHost)
            DrawRightText("выбирает хост", LobbyShipListBox.Right, LobbyShipListBox.Y - 15, 0.45f, SettingsTextDim);
        DrawLobbyInset(LobbyShipListBox);

        var rows = LobbyShipRows(lobby);
        for (var i = 0; i < LobbyShipRowsVisible; i++)
        {
            var index = _lobbyShipScroll + i;
            if (index >= rows.Count)
                break;
            var rect = LobbyShipRow(i);
            var name = rows[index];
            var selected = name is null ? lobby.CustomShipName is null : lobby.CustomShipName == name;
            var hover = isHost && rect.Contains(_designMouse);
            if (selected)
                _spriteBatch.Draw(_pixel, rect, SettingsAccentTeal * 0.22f);
            else if (hover)
                _spriteBatch.Draw(_pixel, rect, Color.White * 0.08f);
            var color = selected ? SettingsAccentTeal : isHost ? SettingsTextPrimary : SettingsTextDim;
            _spriteBatch.DrawString(_font, FitText(name ?? LobbyDefaultShipLabel, 130, 0.6f), new Vector2(rect.X + 8, rect.Y + 6), color, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            if (name is not null && LobbyShipInfoFor(name) is { } info)
            {
                var (width, height) = ShipExtent(info);
                DrawRightText($"{width}x{height}", rect.Right - 8, rect.Y + 8, 0.5f, SettingsTextDim);
            }
        }

        // A thin scroll bar when the list does not fit.
        if (rows.Count > LobbyShipRowsVisible)
        {
            var track = new Rectangle(LobbyShipListBox.Right - 9, LobbyShipListBox.Y + 4, 5, LobbyShipListBox.Height - 8);
            _spriteBatch.Draw(_pixel, track, Color.White * 0.08f);
            var thumbHeight = Math.Max(16, track.Height * LobbyShipRowsVisible / rows.Count);
            var travel = track.Height - thumbHeight;
            var thumbY = track.Y + travel * _lobbyShipScroll / Math.Max(1, rows.Count - LobbyShipRowsVisible);
            _spriteBatch.Draw(_pixel, new Rectangle(track.X, thumbY, track.Width, thumbHeight), SettingsBorderSteel);
        }
    }

    private void DrawLobbyShipPreviewAndDetails(LobbySnapshot lobby)
    {
        DrawLobbyInset(LobbyPreviewBox);
        DrawLobbyInset(LobbyDetailsBox);

        var name = lobby.CustomShipName;
        var info = name is null ? null : LobbyShipInfoFor(name);
        if (info is not null)
            DrawLobbyShipPicture(LobbyPreviewBox, info);
        else
            DrawCenteredText(name is null ? "стандартный корабль" : "нет изображения", LobbyPreviewBox, 0.55f, SettingsTextDim);

        var box = LobbyDetailsBox;
        _spriteBatch.DrawString(_font, FitText(name ?? LobbyDefaultShipLabel, box.Width - 24, 0.9f), new Vector2(box.X + 12, box.Y + 6), SettingsAccentGold, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
        var subtitle = name is null ? ShipCatalog.Name(lobby.ShipKind) : ShipBuiltInFleet.Names.Contains(name) ? "ВШИТЫЙ КОРАБЛЬ" : "ВАШ КОРАБЛЬ";
        _spriteBatch.DrawString(_font, subtitle, new Vector2(box.X + 12, box.Y + 30), SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        if (info is null)
            return;

        var (width, height) = ShipExtent(info);
        var lines = new (string Label, string Value)[]
        {
            ("Габариты", $"{width}x{height} тайлов"),
            ("Отсеков", info.Rooms.ToString()),
            ("Двигателей", info.Engines.ToString()),
            ("Орудий", info.Weapons.ToString()),
        };
        for (var i = 0; i < lines.Length; i++)
        {
            var y = box.Y + 48 + i * 14;
            _spriteBatch.DrawString(_font, lines[i].Label, new Vector2(box.X + 12, y), SettingsTextPrimary, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            DrawRightText(lines[i].Value, box.Right - 14, y, 0.5f, SettingsTextPrimary);
        }
    }

    // Width and height in tiles, as the ship sits in the picture (turned nose-right).
    private static (int Width, int Height) ShipExtent(LobbyShipInfo info)
    {
        var (min, max) = RotatedBounds(info);
        return ((int)MathF.Round(max.X - min.X), (int)MathF.Round(max.Y - min.Y));
    }

    private static Vector2 TurnQuarters(Vector2 point, int quarters)
    {
        // A turn by -quarters * 90 degrees (so a ship whose forward is "quarters" quarter-turns round ends up forward-right).
        var angle = -quarters * MathHelper.PiOver2;
        int c = (int)MathF.Round(MathF.Cos(angle)), s = (int)MathF.Round(MathF.Sin(angle));
        return new Vector2(point.X * c - point.Y * s, point.X * s + point.Y * c);
    }

    private static (Vector2 Min, Vector2 Max) RotatedRect(RectF rect, int quarters)
    {
        var a = TurnQuarters(new Vector2(rect.X, rect.Y), quarters);
        var b = TurnQuarters(new Vector2(rect.Right, rect.Bottom), quarters);
        return (Vector2.Min(a, b), Vector2.Max(a, b));
    }

    private static (Vector2 Min, Vector2 Max) RotatedBounds(LobbyShipInfo info)
    {
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var rect in info.Definition.Rooms.SelectMany(room => room.Rects))
        {
            var (a, b) = RotatedRect(rect, info.Quarter);
            min = Vector2.Min(min, a);
            max = Vector2.Max(max, b);
        }
        return (min, max);
    }

    private static Color LobbyDeviceColor(CustomDeviceKind kind) => kind switch
    {
        CustomDeviceKind.Reactor => new Color(236, 140, 52),
        CustomDeviceKind.Distribution => new Color(232, 208, 90),
        CustomDeviceKind.Helm or CustomDeviceKind.Navigation => new Color(224, 178, 84),
        CustomDeviceKind.Engine => new Color(94, 214, 196),
        CustomDeviceKind.TurretBallistic or CustomDeviceKind.TurretLaser or CustomDeviceKind.TurretMachineGun or CustomDeviceKind.WeaponCharger => new Color(214, 84, 70),
        CustomDeviceKind.Oxygen => new Color(120, 200, 236),
        CustomDeviceKind.Shields => new Color(110, 140, 236),
        _ => new Color(150, 158, 170),
    };

    // The ship as a flat plan: the rooms as steel plates with an edge, the devices as coloured dots, the engines as a bright mark on the stern edge.
    private void DrawLobbyShipPicture(Rectangle box, LobbyShipInfo info)
    {
        var (min, max) = RotatedBounds(info);
        var size = max - min;
        if (size.X < 0.01f || size.Y < 0.01f)
            return;
        var scale = Math.Min((box.Width - 28) / size.X, (box.Height - 28) / size.Y);
        scale = Math.Min(scale, 16f);
        var origin = new Vector2(box.Center.X - size.X * scale / 2f, box.Center.Y - size.Y * scale / 2f);
        Vector2 ToScreen(Vector2 world) => origin + (world - min) * scale;

        var roomIndex = 0;
        foreach (var room in info.Definition.Rooms)
        {
            var fill = roomIndex++ % 2 == 0 ? new Color(58, 68, 84) : new Color(48, 58, 74);
            foreach (var rect in room.Rects)
            {
                var (a, b) = RotatedRect(rect, info.Quarter);
                var s0 = ToScreen(a);
                var s1 = ToScreen(b);
                var r = new Rectangle((int)MathF.Round(s0.X), (int)MathF.Round(s0.Y), Math.Max(1, (int)MathF.Round(s1.X - s0.X)), Math.Max(1, (int)MathF.Round(s1.Y - s0.Y)));
                _spriteBatch.Draw(_pixel, r, fill);
                ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, r, new Color(118, 132, 152), 1);
            }
        }

        var dot = Math.Max(2, (int)(scale * 0.6f));
        foreach (var device in info.Definition.Devices)
        {
            var p = ToScreen(TurnQuarters(new Vector2(device.X + 0.5f, device.Y + 0.5f), info.Quarter));
            _spriteBatch.Draw(_pixel, new Rectangle((int)(p.X - dot / 2f), (int)(p.Y - dot / 2f), dot, dot), LobbyDeviceColor(device.Kind));
        }
        foreach (var engine in info.Definition.EnginesRaw ?? Array.Empty<CustomEngineDef>())
        {
            var p = ToScreen(TurnQuarters(new Vector2(engine.X + 0.5f, engine.Y + 0.5f), info.Quarter));
            _spriteBatch.Draw(_pixel, new Rectangle((int)(p.X - dot / 2f), (int)(p.Y - dot / 2f), dot, dot), LobbyDeviceColor(CustomDeviceKind.Engine));
        }
    }
}
