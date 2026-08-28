using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// M57 - the Engineer tab: "список всех устройств на корабле и их состояние, при поломке он может
// начать чинить его". Every device already rides some list in WorldSnapshot (SystemStates covers
// both ShipSystemDevices and Cameras - World.cs's own CreateSnapshot concatenates them - plus
// JunctionStates, destroyed DoorStates, and the new BlockStates for the reactor/distribution/
// battery/helm/nav "boxes") - this panel just flattens all of them into one clickable list.
// Clicking a row sends World.SystemRepair.cs's remote-focus command (Game1.Input.cs) rather than
// requiring the player to physically walk to the device the way the old single-card
// SystemRepairPanel does.
public sealed class EngineerDevicePanel
{
    public const int Width = 340;
    private const int HeaderHeight = 26;
    private const int RowHeight = 24;
    private const int RowGap = 2;

    public readonly record struct Row(string DeviceId, string Label, bool Damaged, float RepairProgress);

    public static IReadOnlyList<Row> BuildRows(WorldSnapshot snapshot)
    {
        var rows = new List<Row>();
        var deviceSystemById = snapshot.SystemDevices.ToDictionary(d => d.Id, d => d.System);
        var cameraIds = snapshot.Cameras.Select(c => c.Id).ToHashSet();
        var cameraNumber = 0;
        foreach (var state in snapshot.SystemStates)
        {
            if (deviceSystemById.TryGetValue(state.DeviceId, out var system))
                rows.Add(new Row(state.DeviceId, ComponentRenderer.SystemLabel(system), state.Damaged, state.RepairProgress));
            else if (cameraIds.Contains(state.DeviceId))
                rows.Add(new Row(state.DeviceId, $"Камера {++cameraNumber}", state.Damaged, state.RepairProgress));
        }
        foreach (var junction in snapshot.JunctionStates)
            rows.Add(new Row(junction.DeviceId, "Распред. коробка", junction.Damaged, junction.RepairProgress));
        foreach (var door in snapshot.DoorStates.Where(d => d.Destroyed))
            rows.Add(new Row(door.DoorId, "Дверь", true, door.RepairProgress));

        var blockLabels = new[] { "Реактор", "Распределение", "Батарея", "Штурвал", "Навигация" };
        var blocks = snapshot.BlockStates ?? Array.Empty<ShipSystemState>();
        for (var i = 0; i < blocks.Count && i < blockLabels.Length; i++)
            rows.Add(new Row(blocks[i].DeviceId, blockLabels[i], blocks[i].Damaged, blocks[i].RepairProgress));

        return rows;
    }

    public static Rectangle GetRowRect(int index, Vector2 origin) =>
        new((int)origin.X, (int)origin.Y + HeaderHeight + index * (RowHeight + RowGap), Width, RowHeight);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public EngineerDevicePanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, string? focusedDeviceId)
    {
        var rows = BuildRows(snapshot);
        spriteBatch.DrawString(_font, $"Устройства корабля ({rows.Count})", origin, Color.LightGray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rect = GetRowRect(i, origin);
            var focused = row.DeviceId == focusedDeviceId;
            spriteBatch.Draw(_pixel, rect,
                !row.Damaged ? new Color(30, 45, 30) * 0.9f :
                focused ? new Color(200, 120, 30) * 0.95f : new Color(50, 35, 30) * 0.9f);

            spriteBatch.DrawString(_font, row.Label, new Vector2(rect.X + 6, rect.Y + 5), Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

            var status = !row.Damaged ? "ИСПРАВЕН" : focused ? $"РЕМОНТ {row.RepairProgress:0}%" : $"СЛОМАН {row.RepairProgress:0}%";
            var statusColor = !row.Damaged ? Color.LightGreen : focused ? Color.White : Color.OrangeRed;
            var statusSize = _font.MeasureString(status) * 0.4f;
            spriteBatch.DrawString(_font, status, new Vector2(rect.Right - statusSize.X - 6, rect.Y + 5), statusColor, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
        }
    }
}
