using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// What frames the map: the bar across the top (the date of the war, who holds how much, the speed of time), the panel for
// the selected system, the key to the signs, and the tooltip under the pointer.
public sealed partial class GalacticMapPanel
{
    // How many seconds of war pass per real second at each button; 0 pauses.
    public static readonly int[] SpeedLevels = { 0, 1, 10, 100, 1000, 10000 };
    private static readonly string[] SpeedLabels = { "II", "1×", "10×", "100×", "1000×", "10000×" };

    private static readonly Color BarFill = new(6, 10, 20);
    private static readonly Color PlateFill = new(8, 14, 26);
    private static readonly Color BarLine = new(64, 118, 168);
    private static readonly Color TextBright = new(226, 236, 248);
    private static readonly Color TextDim = new(138, 158, 184);

    private Rectangle SpeedButtonRect(int index)
    {
        const int width = 46;
        const int gap = 3;
        var x = _view.Right - 8 - SpeedLevels.Length * (width + gap) + gap + index * (width + gap);
        return new Rectangle(x, _view.Y + 6, width, 22);
    }

    private Rectangle InfoPanelRect() => new(_view.Right - 256, _view.Y + (int)TopBarHeight + 8, 248, 270);
    private Rectangle JumpButtonRect() => new(InfoPanelRect().X + 10, InfoPanelRect().Bottom - 36, InfoPanelRect().Width - 20, 26);

    private static string OwnerName(byte owner) => owner switch
    {
        1 => "Колониальный союз",
        2 => "Восстание",
        _ => "Свободная система",
    };

    private static string WarDate(double hours)
    {
        var day = (int)(hours / 24.0) + 1;
        var hour = (int)(hours % 24.0);
        var minute = (int)(hours * 60.0 % 60.0);
        return string.Create(CultureInfo.InvariantCulture, $"день {day}, {hour:00}:{minute:00}");
    }

    private void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale = 0.5f)
    {
        spriteBatch.DrawString(_font, text, position + new Vector2(1f, 1f), Color.Black * 0.7f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawPlate(SpriteBatch spriteBatch, Rectangle rect, float alpha = 0.88f)
    {
        spriteBatch.Draw(_pixel, rect, PlateFill * alpha);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), BarLine * 0.7f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), BarLine * 0.35f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), BarLine * 0.5f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), BarLine * 0.5f);
    }

    private void DrawHud(SpriteBatch spriteBatch, WorldSnapshot snapshot, bool hasWar)
    {
        DrawTopBar(spriteBatch, snapshot, hasWar);
        if (SelectedSystemId is { } selectedId && snapshot.StarSystems.FirstOrDefault(s => s.Id == selectedId) is { } selected)
            DrawInfoPanel(spriteBatch, snapshot, selected, Array.IndexOf(snapshot.StarSystems.ToArray(), selected), hasWar);
        else if (_hover >= 0)
            DrawTooltip(spriteBatch, snapshot, _hover, hasWar);
        DrawLegend(spriteBatch, hasWar);
        DrawText(spriteBatch, "ЛКМ — выбрать систему   ПКМ (тащить) — сдвиг   колесо — масштаб   M — закрыть",
            new Vector2(_view.X + 214f, _view.Bottom - 16f), TextDim * 0.9f, 0.42f);
    }

    // ---- the top bar ----

    private void DrawTopBar(SpriteBatch spriteBatch, WorldSnapshot snapshot, bool hasWar)
    {
        var bar = new Rectangle(_view.X, _view.Y, _view.Width, (int)TopBarHeight);
        spriteBatch.Draw(_pixel, bar, BarFill * 0.9f);
        spriteBatch.Draw(_pixel, new Rectangle(bar.X, bar.Bottom - 1, bar.Width, 1), BarLine * 0.8f);

        DrawText(spriteBatch, "КАРТА ГАЛАКТИКИ", new Vector2(bar.X + 12f, bar.Y + 9f), new Color(170, 205, 235), 0.58f);
        if (!hasWar)
        {
            DrawText(spriteBatch, "война не идёт (обучение)", new Vector2(bar.X + 170f, bar.Y + 10f), TextDim, 0.5f);
            return;
        }

        var clock = snapshot.WarClock!;
        var x = bar.X + 168f;
        DrawText(spriteBatch, "ВОЙНА  " + WarDate(clock.Hours), new Vector2(x, bar.Y + 10f), TextBright, 0.52f);
        x += 150f;
        var union = $"Союз: {clock.UnionSystems} систем · {clock.UnionColonies} колоний · {clock.UnionYards} верфей · флот {clock.UnionFleetSupply} · руда {clock.OrePool:0}";
        DrawText(spriteBatch, union, new Vector2(x, bar.Y + 10f), Color.Lerp(OwnerColor(1), Color.White, 0.45f), 0.46f);
        x += _font.MeasureString(union).X * 0.46f + 16f;
        var virus = $"Восстание: {clock.VirusSystems} систем" + (clock.EnemyFleets > 0 ? $" · флотов {clock.EnemyFleets}" : "");
        DrawText(spriteBatch, virus, new Vector2(x, bar.Y + 10f), Color.Lerp(OwnerColor(2), Color.White, 0.35f), 0.46f);
        x += _font.MeasureString(virus).X * 0.46f + 16f;
        if (clock.Battles > 0)
        {
            var pulse = 0.65f + 0.35f * MathF.Sin(_time * 6f);
            DrawText(spriteBatch, $"Боёв: {clock.Battles}", new Vector2(x, bar.Y + 10f), new Color(255, 96, 80) * pulse, 0.46f);
        }
        if (snapshot.WarMother is { } mother)
        {
            // On a plate under the bar, which is already full at the design width.
            var text = mother.Alive ? $"Колониальная матка: прочность {mother.Hp}%" + (mother.Fighting ? " · В БОЮ" : "") : "КОЛОНИАЛЬНАЯ МАТКА УТРАЧЕНА, артефакт потерян";
            var size = _font.MeasureString(text) * 0.46f;
            var plate = new Rectangle((int)bar.X + 8, (int)bar.Bottom + 6, (int)size.X + 16, 20);
            spriteBatch.Draw(_pixel, plate, new Color(8, 14, 26) * 0.82f);
            spriteBatch.Draw(_pixel, new Rectangle(plate.X, plate.Y, 3, plate.Height), mother.Alive ? new Color(255, 214, 120) : new Color(255, 96, 80));
            DrawText(spriteBatch, text, new Vector2(plate.X + 10f, plate.Y + 4f), mother.Alive ? new Color(255, 214, 120) : new Color(255, 96, 80), 0.46f);
        }
        if (snapshot.CampaignObjective is { } objective)
        {
            // The chapter two objective, under the mother ship plate: where the crew has to get to next.
            var size = _font.MeasureString(objective) * 0.46f;
            var top = (int)bar.Bottom + 6 + (snapshot.WarMother is not null ? 26 : 0);
            var plate = new Rectangle((int)bar.X + 8, top, (int)size.X + 16, 20);
            spriteBatch.Draw(_pixel, plate, new Color(8, 14, 26) * 0.82f);
            spriteBatch.Draw(_pixel, new Rectangle(plate.X, plate.Y, 3, plate.Height), new Color(120, 200, 255));
            DrawText(spriteBatch, objective, new Vector2(plate.X + 10f, plate.Y + 4f), new Color(170, 225, 255), 0.46f);
        }

        for (var i = 0; i < SpeedLevels.Length; i++)
        {
            var rect = SpeedButtonRect(i);
            var active = clock.SpeedLevel == SpeedLevels[i];
            var hovered = _pointer is { } pointer && rect.Contains(new Point((int)pointer.X, (int)pointer.Y));
            spriteBatch.Draw(_pixel, rect, active ? OwnerColor(1) * 0.65f : hovered ? BarLine * 0.5f : new Color(20, 32, 52) * 0.9f);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), active ? Color.White * 0.9f : BarLine * 0.5f);
            var size = _font.MeasureString(SpeedLabels[i]) * 0.48f;
            DrawText(spriteBatch, SpeedLabels[i], new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), active ? Color.White : TextBright, 0.48f);
        }
    }

    // ---- the panel of the selected system ----

    private void DrawInfoPanel(SpriteBatch spriteBatch, WorldSnapshot snapshot, StarSystemSummary system, int index, bool hasWar)
    {
        var rect = InfoPanelRect();
        DrawPlate(spriteBatch, rect, 0.92f);
        var node = hasWar && index >= 0 ? snapshot.WarNodes![index] : null;
        var owner = node?.Owner ?? (byte)0;

        spriteBatch.Draw(_pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 40), OwnerColor(owner) * 0.22f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X + 1, rect.Y + 41, rect.Width - 2, 1), OwnerColor(owner) * 0.8f);
        DrawText(spriteBatch, system.Name, new Vector2(rect.X + 10f, rect.Y + 6f), Color.White, 0.72f);
        DrawText(spriteBatch, OwnerName(owner), new Vector2(rect.X + 10f, rect.Y + 25f), Color.Lerp(OwnerColor(owner), Color.White, 0.5f), 0.44f);

        var y = rect.Y + 50f;
        void Row(string label, string value, Color? valueColor = null)
        {
            DrawText(spriteBatch, label, new Vector2(rect.X + 10f, y), TextDim, 0.44f);
            var size = _font.MeasureString(value) * 0.44f;
            DrawText(spriteBatch, value, new Vector2(rect.Right - 10f - size.X, y), valueColor ?? TextBright, 0.44f);
            y += 16f;
        }

        if (node is null)
        {
            Row("Размер поля", $"{system.Width:0}×{system.Height:0}");
        }
        else
        {
            Row("Планет", $"{node.Planets}" + (node.Rocky ? " (есть каменная)" : ""));
            Row("Богатство руды", $"×{node.Richness.ToString("0.0", CultureInfo.InvariantCulture)}");
            if (owner != 0)
            {
                y += 4f;
                if (node.Planet == 2)
                    Row("Мир", "СТОЛИЦА (мега-крепость)", new Color(255, 214, 120));
                else if (node.Planet == 3)
                    Row("Мир", "ГНЕЗДО ВОССТАНИЯ", new Color(170, 255, 110));
                else if (node.Planet == 1)
                    Row("Мир", "колония");
                if (node.Outpost)
                    Row("Аванпост добычи", $"добытчиков {node.Miners}, дронов {node.Drones}");
                if (node.Yard == 1)
                    Row("Верфь", "есть");
                else if (node.Yard == 2)
                    Row("Мегаверфь", "есть (дредноуты)", new Color(255, 214, 120));
                Row("Руда на складе", node.Stock.ToString(CultureInfo.InvariantCulture));
                if (node.Fleet > 0)
                    Row("Флот", $"{node.Fleet} очк.");
                if (node.Project != 0)
                    Row("Строится", node.Project switch { 1 => "колония", 2 => "верфь", _ => "мегаверфь" }, new Color(255, 176, 60));
            }
            else
            {
                y += 4f;
                DrawText(spriteBatch, "Никому не принадлежит.", new Vector2(rect.X + 10f, y), TextDim, 0.44f);
            }
        }

        DrawJumpSection(spriteBatch, snapshot, system);
    }

    private void DrawJumpSection(SpriteBatch spriteBatch, WorldSnapshot snapshot, StarSystemSummary system)
    {
        var button = JumpButtonRect();
        var current = snapshot.StarSystems.FirstOrDefault(s => s.Id == snapshot.CurrentSystemId);
        string label;
        Color fill;
        if (system.Id == snapshot.CurrentSystemId)
        {
            label = "вы здесь";
            fill = new Color(30, 70, 44);
        }
        else if (current is null || Distance(system, current) > GalaxyMap.WarpJumpRadius)
        {
            label = "вне круга прыжка";
            fill = new Color(32, 38, 50);
        }
        else
        {
            var cost = GalaxyMap.HyperiumCostForDistance(Distance(system, current));
            if (!snapshot.CanWarpNow)
            {
                label = "долетите до границы системы";
                fill = new Color(48, 42, 28);
            }
            else if (snapshot.WarpCooldownRemaining > 0f)
            {
                var left = (int)MathF.Ceiling(snapshot.WarpCooldownRemaining);
                label = $"двигатель перезаряжается: {left / 60}:{left % 60:00}";
                fill = new Color(44, 52, 76);
            }
            else if (snapshot.HyperiumAboard < cost)
            {
                label = $"нужно гиперия {cost}, есть {snapshot.HyperiumAboard}";
                fill = new Color(72, 34, 30);
            }
            else
            {
                label = $"ПРЫЖОК (гиперий {cost})";
                fill = new Color(34, 98, 62);
                if (_pointer is { } pointer && button.Contains(new Point((int)pointer.X, (int)pointer.Y)))
                    fill = new Color(48, 130, 82);
            }
        }
        spriteBatch.Draw(_pixel, button, fill);
        spriteBatch.Draw(_pixel, new Rectangle(button.X, button.Bottom - 2, button.Width, 2), BarLine * 0.6f);
        var size = _font.MeasureString(label) * 0.46f;
        DrawText(spriteBatch, label, new Vector2(button.Center.X - size.X / 2f, button.Center.Y - size.Y / 2f), TextBright, 0.46f);
    }

    // ---- the tooltip, the key ----

    private void DrawTooltip(SpriteBatch spriteBatch, WorldSnapshot snapshot, int index, bool hasWar)
    {
        if (_pointer is not { } pointer)
            return;
        var system = snapshot.StarSystems[index];
        var node = hasWar ? snapshot.WarNodes![index] : null;
        var lines = new List<(string Text, Color Color)> { (system.Name, Color.White) };
        if (node is not null)
        {
            lines.Add((OwnerName(node.Owner), Color.Lerp(OwnerColor(node.Owner), Color.White, 0.5f)));
            if (node.Owner != 0)
                lines.Add((node.Planet == 3 ? "гнездо восстания" : node.Planet == 2 ? "столица" : node.Planet == 1 ? "колония" : node.Outpost ? "аванпост добычи" : "", TextDim));
        }
        lines = lines.Where(l => l.Text.Length > 0).ToList();
        var width = lines.Max(l => _font.MeasureString(l.Text).X * 0.46f) + 16f;
        var rect = new Rectangle((int)MathF.Min(pointer.X + 16f, _view.Right - width - 4f), (int)MathF.Min(pointer.Y + 14f, _view.Bottom - lines.Count * 15 - 14), (int)width, lines.Count * 15 + 8);
        DrawPlate(spriteBatch, rect, 0.94f);
        for (var i = 0; i < lines.Count; i++)
            DrawText(spriteBatch, lines[i].Text, new Vector2(rect.X + 8f, rect.Y + 4f + i * 15f), lines[i].Color, 0.46f);
    }

    private void DrawLegend(SpriteBatch spriteBatch, bool hasWar)
    {
        if (!hasWar)
            return;
        var rect = new Rectangle(_view.X + 8, _view.Bottom - 174, 196, 166);
        DrawPlate(spriteBatch, rect, 0.8f);
        var y = rect.Y + 8f;

        void Swatch(Color color, string text)
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + 10, (int)y + 3, 9, 9), color);
            DrawText(spriteBatch, text, new Vector2(rect.X + 26f, y), TextBright, 0.42f);
            y += 14f;
        }
        Swatch(OwnerColor(1), "Колониальный союз");
        Swatch(OwnerColor(2), "Восстание");
        Swatch(OwnerColor(0), "Свободные системы");
        y += 4f;

        void Sign(Mark mark, string text, Color tint)
        {
            DrawBadge(spriteBatch, mark, new Vector2(rect.X + 15f, y + 6f), 11f, tint);
            DrawText(spriteBatch, text, new Vector2(rect.X + 30f, y), TextDim, 0.4f);
            y += 14f;
        }
        var color = OwnerColor(1);
        Sign(Mark.Colony, "колония", color);
        Sign(Mark.Capital, "столица", color);
        Sign(Mark.Outpost, "аванпост добычи", color);
        Sign(Mark.Yard, "верфь", color);
        Sign(Mark.Megayard, "мегаверфь", color);
        Sign(Mark.Nest, "гнездо восстания", OwnerColor(2));
        Primitives.FillPolygon(spriteBatch, _pixel, new Vector2(rect.X + 15f, y + 6f), new[] { new Vector2(rect.X + 18.4f, y + 6f), new Vector2(rect.X + 15f, y + 8f), new Vector2(rect.X + 11.6f, y + 6f), new Vector2(rect.X + 15f, y + 4f) }, Color.Lerp(color, Color.White, 0.35f));
        DrawText(spriteBatch, "грузовой корабль", new Vector2(rect.X + 30f, y), TextDim, 0.4f);
    }
}
