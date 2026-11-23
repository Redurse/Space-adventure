using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

public enum HealthHitKind { None, Close, Part, WholeBody, Item, Target, Cpr, Surgery, Cancel }

public readonly record struct HealthHit(HealthHitKind Kind, BodyPart? Part = null, ItemType? Item = null, int Index = 0, Organ? Organ = null);

// The health window (the H key), after Barotrauma's health interface: the person's body on the left with every part coloured by how hurt
// it is (click one to select it), the afflictions of the selected part and of the body as a whole in the middle with a bar each, and on the
// right the treatments the doctor's inventory could offer (the ones that would help are marked), the medicine to click onto the selected part,
// chest compressions for a stopped heart and the operating buttons. Along the top, whoever is within reach to be treated.
public sealed class HealthWindow
{
    public static readonly Rectangle WindowRect = new(110, 26, 980, 508);
    private const int FigureCenterX = 140;
    private const int SlotSize = 50;
    private const int SlotGap = 6;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public HealthWindow(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    // ---- layout (all in design coordinates) ----

    private static Rectangle Local(int x, int y, int w, int h) => new(WindowRect.X + x, WindowRect.Y + y, w, h);

    public static Rectangle CloseRect => Local(WindowRect.Width - 40, 8, 28, 28);

    public static Rectangle PartRect(BodyPart part) => part switch
    {
        BodyPart.Head => Local(FigureCenterX - 24, 66, 48, 52),
        BodyPart.Torso => Local(FigureCenterX - 40, 124, 80, 130),
        BodyPart.LeftArm => Local(FigureCenterX - 72, 124, 28, 124),
        BodyPart.RightArm => Local(FigureCenterX + 44, 124, 28, 124),
        BodyPart.LeftLeg => Local(FigureCenterX - 40, 260, 38, 150),
        _ => Local(FigureCenterX + 2, 260, 38, 150),
    };

    public static Rectangle WholeBodyRect => Local(20, 424, 240, 30);
    public static Rectangle TargetTabRect(int index) => Local(300 + index * 124, 8, 118, 28);
    public static Rectangle AfflictionsArea => Local(290, 60, 340, 400);
    public static Rectangle ItemSlotRect(int index) => Local(650 + (index % 5) * (SlotSize + SlotGap + 4), 184 + (index / 5) * (SlotSize + SlotGap + 10), SlotSize, SlotSize);
    public static Rectangle RecommendedRect(int index) => Local(650 + index * (40 + 6), 86, 40, 40);
    public static Rectangle CprRect => Local(650, 334, 300, 34);
    public static Rectangle SurgeryRect(Organ organ) => Local(650, 374 + (int)organ * 36, 300, 32);
    public static Rectangle CancelRect => Local(820, 468, 130, 28);

    // The medicine the doctor has, in the order of MedicalTreatment.Items, with how many of each.
    public static List<(ItemType Item, int Count)> MedicineIn(CharacterState? me)
    {
        var counts = new Dictionary<ItemType, int>();
        if (me?.Inventory is { } inventory)
            foreach (var slot in inventory.MainSlots.Concat(inventory.BeltBagSlots))
                if (slot is { } item && MedicalTreatment.IsMedical(item))
                    counts[item] = counts.GetValueOrDefault(item) + 1;
        return MedicalTreatment.Items.Where(counts.ContainsKey).Select(i => (i, counts[i])).ToList();
    }

    public HealthHit HitTest(Point mouse, CharacterState? me, IReadOnlyList<CharacterState> targets)
    {
        if (CloseRect.Contains(mouse))
            return new HealthHit(HealthHitKind.Close);
        if (CancelRect.Contains(mouse))
            return new HealthHit(HealthHitKind.Cancel);
        for (var i = 0; i < targets.Count; i++)
            if (TargetTabRect(i).Contains(mouse))
                return new HealthHit(HealthHitKind.Target, Index: i);
        foreach (var part in Enum.GetValues<BodyPart>())
            if (PartRect(part).Contains(mouse))
                return new HealthHit(HealthHitKind.Part, Part: part);
        if (WholeBodyRect.Contains(mouse))
            return new HealthHit(HealthHitKind.WholeBody);
        var medicine = MedicineIn(me);
        for (var i = 0; i < medicine.Count; i++)
            if (ItemSlotRect(i).Contains(mouse))
                return new HealthHit(HealthHitKind.Item, Item: medicine[i].Item, Index: i);
        if (CprRect.Contains(mouse))
            return new HealthHit(HealthHitKind.Cpr);
        foreach (var organ in Enum.GetValues<Organ>())
            if (SurgeryRect(organ).Contains(mouse))
                return new HealthHit(HealthHitKind.Surgery, Organ: organ);
        return new HealthHit(HealthHitKind.None);
    }

    public static bool Contains(Point mouse) => WindowRect.Contains(mouse);

    // ---- colours ----

    private static Color SeverityColor(float severity)
    {
        severity = MathHelper.Clamp(severity, 0f, 1f);
        return severity < 0.5f
            ? Color.Lerp(new Color(70, 170, 84), new Color(226, 196, 60), severity * 2f)
            : Color.Lerp(new Color(226, 196, 60), new Color(214, 56, 48), (severity - 0.5f) * 2f);
    }

    private static float PartSeverity(BodyPartState part) => Math.Max((part.Trauma + part.Burn) / 80f, part.Bleeding / 60f);

    // ---- drawing ----

    public void Draw(SpriteBatch spriteBatch, CharacterState me, CharacterState target, IReadOnlyList<CharacterState> targets, BodyPart? selected,
        Point mouse, float totalSeconds)
    {
        if (target.Body is not { } bodyState)
            return;
        var body = new BodyHealth();
        body.LoadState(bodyState);
        var isSelf = target.PlayerId == me.PlayerId;
        var hover = HitTest(mouse, me, targets);

        spriteBatch.Draw(_pixel, new Rectangle(0, 0, 1200, 560), Color.Black * 0.35f);
        PanelFrame.Draw(spriteBatch, _pixel, WindowRect, new Color(22, 26, 32), new Color(96, 120, 150), 0.97f, 2);

        DrawHeader(spriteBatch, me, target, targets, body, hover, isSelf);
        DrawFigure(spriteBatch, bodyState, selected, hover, totalSeconds);
        DrawAfflictions(spriteBatch, body, bodyState, selected);
        DrawTreatments(spriteBatch, me, target, body, selected, hover, totalSeconds);

        // The doctor's own progress, if busy.
        if (me.Treatment is { } treatment)
        {
            var bar = Local(300, 472, 500, 18);
            spriteBatch.Draw(_pixel, bar, Color.Black * 0.6f);
            var fraction = treatment.IsCpr ? 1f : treatment.Total <= 0f ? 1f : 1f - treatment.Remaining / treatment.Total;
            spriteBatch.Draw(_pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * fraction), bar.Height), treatment.IsCpr ? Color.IndianRed : new Color(90, 190, 120));
            var label = treatment.IsCpr ? "Массаж сердца..." : treatment.Organ is { } organ ? $"Операция: {MedicalTreatment.OrganName(organ).ToLowerInvariant()}..."
                : $"Лечение: {(treatment.Item is { } item ? ItemDefinitions.DisplayName(item) : "")}...";
            spriteBatch.DrawString(_font, label, new Vector2(bar.X + 6, bar.Y + 1), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            DrawButton(spriteBatch, CancelRect, "Отмена", hover.Kind == HealthHitKind.Cancel, true);
        }
    }

    private void DrawHeader(SpriteBatch spriteBatch, CharacterState me, CharacterState target, IReadOnlyList<CharacterState> targets, BodyHealth body,
        HealthHit hover, bool isSelf)
    {
        var name = isSelf ? "Вы" : target.BotName ?? target.Nickname ?? $"Игрок {target.PlayerId}";
        spriteBatch.DrawString(_font, $"Здоровье: {name}", new Vector2(WindowRect.X + 18, WindowRect.Y + 10), Color.White, 0f, Vector2.Zero, 0.62f, SpriteEffects.None, 0f);

        // Who is within reach.
        for (var i = 0; i < targets.Count && i < 6; i++)
        {
            var rect = TargetTabRect(i);
            var other = targets[i];
            var active = other.PlayerId == target.PlayerId;
            var downed = other.Body?.IsDowned == true;
            spriteBatch.Draw(_pixel, rect, active ? new Color(70, 100, 140) : hover.Kind == HealthHitKind.Target && hover.Index == i ? new Color(48, 60, 78) : new Color(34, 40, 50));
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, downed ? Color.IndianRed : new Color(90, 100, 118), 1);
            var label = other.PlayerId == me.PlayerId ? "Вы" : other.BotName ?? other.Nickname ?? $"Игрок {other.PlayerId}";
            if (label.Length > 12)
                label = label[..12];
            spriteBatch.DrawString(_font, label, new Vector2(rect.X + 6, rect.Y + 5), downed ? Color.IndianRed : Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }

        DrawButton(spriteBatch, CloseRect, "×", hover.Kind == HealthHitKind.Close, true);

        // Vitality bar and state.
        var bar = Local(20, 42, 240, 14);
        spriteBatch.Draw(_pixel, bar, Color.Black * 0.6f);
        var vitality = MathHelper.Clamp(target.Health / 100f, 0f, 1f);
        spriteBatch.Draw(_pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * vitality), bar.Height), SeverityColor(1f - vitality));
        var state = target.Body is { Dead: true } ? "Мёртв" : body.InArrest ? $"Остановка сердца: {body.ArrestSecondsLeft:0} с" : body.Unconscious ? "Без сознания" : $"Жизненные силы: {target.Health:0}%";
        spriteBatch.DrawString(_font, state, new Vector2(bar.X + 4, bar.Y - 1), Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }

    private void DrawFigure(SpriteBatch spriteBatch, BodyState bodyState, BodyPart? selected, HealthHit hover, float totalSeconds)
    {
        var pulse = 0.55f + 0.45f * MathF.Sin(totalSeconds * 6f);
        foreach (var part in Enum.GetValues<BodyPart>())
        {
            var state = bodyState.Parts.First(p => p.Part == part);
            var rect = PartRect(part);
            var hovered = hover.Kind == HealthHitKind.Part && hover.Part == part;
            var color = SeverityColor(PartSeverity(state));
            spriteBatch.Draw(_pixel, rect, hovered ? Color.Lerp(color, Color.White, 0.25f) : color);
            // Shading: a lighter top edge and a darker bottom, so it reads as a body rather than flat blocks.
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), Color.White * 0.25f);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 4, rect.Width, 4), Color.Black * 0.25f);
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, selected == part ? Color.White : Color.Black * 0.8f, selected == part ? 3 : 1);

            if (state.Bandaged)
                spriteBatch.Draw(_pixel, new Rectangle(rect.X + 2, rect.Center.Y - 6, rect.Width - 4, 12), new Color(236, 232, 220) * 0.9f);
            if (state.Tourniquet)
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y + 8, rect.Width, 8), new Color(230, 140, 40));
            if (state.Cast)
                spriteBatch.Draw(_pixel, new Rectangle(rect.X + 2, rect.Y + rect.Height / 3, rect.Width - 4, rect.Height / 3), Color.White * 0.9f);
            else if (state.Splinted)
                spriteBatch.Draw(_pixel, new Rectangle(rect.Center.X - 4, rect.Y + 6, 8, rect.Height - 12), new Color(190, 148, 92));
            if (state.Fractured)
            {
                spriteBatch.Draw(_pixel, new Rectangle(rect.X + 4, rect.Center.Y - 1, rect.Width - 8, 3), Color.Black);
                spriteBatch.Draw(_pixel, new Rectangle(rect.Center.X - 1, rect.Center.Y - 8, 3, 16), Color.Black);
            }
            if (state.Bleeding > 0.5f)
                for (var i = 0; i < 3; i++)
                    spriteBatch.Draw(_pixel, new Rectangle(rect.X + 6 + i * (rect.Width / 4), rect.Bottom - 12 - (int)(8 * ((totalSeconds * 1.5f + i * 0.33f) % 1f)), 4, 6), Color.DarkRed * pulse);
            if (state.Infection > 20f)
                spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 12, rect.Y + 4, 8, 8), new Color(150, 200, 60));
        }

        spriteBatch.DrawString(_font, "Голова", new Vector2(PartRect(BodyPart.Head).Right + 8, PartRect(BodyPart.Head).Y + 14), Color.LightGray, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
        DrawButton(spriteBatch, WholeBodyRect, selected is null ? "[ Всё тело ]" : "Всё тело", hover.Kind == HealthHitKind.WholeBody, true);
    }

    private void DrawAfflictions(SpriteBatch spriteBatch, BodyHealth body, BodyState state, BodyPart? selected)
    {
        var area = AfflictionsArea;
        var y = area.Y;
        void Title(string text)
        {
            spriteBatch.DrawString(_font, text, new Vector2(area.X, y), new Color(160, 190, 230), 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            y += 22;
        }
        void Row(string name, float fraction, string detail = "")
        {
            if (y > area.Bottom - 20)
                return;
            fraction = MathHelper.Clamp(fraction, 0f, 1f);
            var color = SeverityColor(fraction);
            spriteBatch.Draw(_pixel, new Rectangle(area.X, y + 2, 12, 12), color);
            spriteBatch.DrawString(_font, name, new Vector2(area.X + 18, y), Color.White, 0f, Vector2.Zero, 0.47f, SpriteEffects.None, 0f);
            var track = new Rectangle(area.X + 150, y + 3, 110, 10);
            spriteBatch.Draw(_pixel, track, Color.Black * 0.55f);
            spriteBatch.Draw(_pixel, new Rectangle(track.X, track.Y, (int)(track.Width * fraction), track.Height), color);
            spriteBatch.DrawString(_font, $"{fraction * 100f:0}%  {detail}", new Vector2(track.Right + 6, y), Color.LightGray, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
            y += 20;
        }

        if (selected is { } part)
        {
            var injury = state.Parts.First(p => p.Part == part);
            Title(MedicalTreatment.PartName(part));
            var any = false;
            void Part(string name, float value, string detail = "")
            {
                if (value <= 0.5f)
                    return;
                any = true;
                Row(name, value / 100f, detail);
            }
            Part("Травма", injury.Trauma);
            Part("Ожог", injury.Burn);
            Part("Кровотечение", injury.Bleeding, injury.Tourniquet ? "(жгут)" : injury.Bandaged ? "(перевязано)" : "");
            if (injury.Fractured)
            {
                any = true;
                Row("Перелом", 1f, injury.Cast ? $"(гипс, {Math.Max(0f, 180f - injury.CastSeconds):0} с)" : injury.Splinted ? "(шина)" : "");
            }
            Part("Инфекция", injury.Infection, injury.DisinfectedSeconds > 0f ? "(обработано)" : "");
            if (!any)
            {
                spriteBatch.DrawString(_font, "Повреждений нет", new Vector2(area.X, y), Color.LightGreen, 0f, Vector2.Zero, 0.47f, SpriteEffects.None, 0f);
                y += 20;
            }
            y += 6;
        }

        Title("Состояние организма");
        var anyBody = false;
        void Whole(string name, float value, string detail = "")
        {
            if (value <= 0.5f)
                return;
            anyBody = true;
            Row(name, value / 100f, detail);
        }
        Whole("Потеря крови", state.BloodLoss);
        Whole("Кислородное голодание", state.Hypoxia);
        Whole("Боль", state.Pain, state.AnalgesiaSeconds > 0f ? "(обезболено)" : "");
        Whole("Сотрясение", state.Concussion);
        Whole("Сепсис", state.Sepsis);
        Whole("Сердце", state.HeartDamage, "(операция)");
        Whole("Лёгкие", state.LungDamage, "(операция)");
        Whole("Мозг", state.BrainDamage, "(операция)");
        if (body.Overdosing)
        {
            anyBody = true;
            Row("Передозировка", 1f);
        }
        if (state.AdrenalineSeconds > 0f)
        {
            anyBody = true;
            spriteBatch.DrawString(_font, $"Адреналин: {state.AdrenalineSeconds:0} с", new Vector2(area.X, y), Color.LightSkyBlue, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
            y += 18;
        }
        if (state.AntibioticSeconds > 0f)
        {
            anyBody = true;
            spriteBatch.DrawString(_font, $"Антибиотик: {state.AntibioticSeconds:0} с", new Vector2(area.X, y), Color.LightGreen, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
            y += 18;
        }
        if (!anyBody)
            spriteBatch.DrawString(_font, "Всё в порядке", new Vector2(area.X, y), Color.LightGreen, 0f, Vector2.Zero, 0.47f, SpriteEffects.None, 0f);
    }

    private void DrawTreatments(SpriteBatch spriteBatch, CharacterState me, CharacterState target, BodyHealth body, BodyPart? selected, HealthHit hover, float totalSeconds)
    {
        var recommended = MedicalTreatment.Recommended(body, selected);
        var medicine = MedicineIn(me);

        spriteBatch.DrawString(_font, "Рекомендуемое лечение", new Vector2(Local(650, 62, 0, 0).X, Local(650, 62, 0, 0).Y), new Color(160, 190, 230), 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        if (recommended.Count == 0)
            spriteBatch.DrawString(_font, "лечение не требуется", new Vector2(RecommendedRect(0).X, RecommendedRect(0).Y + 10), Color.Gray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        for (var i = 0; i < recommended.Count && i < 6; i++)
        {
            var rect = RecommendedRect(i);
            var owned = medicine.Any(m => m.Item == recommended[i]);
            spriteBatch.Draw(_pixel, rect, owned ? new Color(40, 70, 48) : new Color(48, 38, 38));
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, owned ? Color.LightGreen : Color.IndianRed * 0.8f, 1);
            ItemIcons.Draw(spriteBatch, _pixel, recommended[i], new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4));
            if (!owned)
                spriteBatch.Draw(_pixel, rect, Color.Black * 0.5f);
        }

        var inventoryTitle = Local(650, 150, 0, 0);
        spriteBatch.DrawString(_font, selected is null ? "Ваши препараты (выберите часть тела)" : "Ваши препараты: нажмите, чтобы применить",
            new Vector2(inventoryTitle.X, inventoryTitle.Y), new Color(160, 190, 230), 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        if (medicine.Count == 0)
            spriteBatch.DrawString(_font, "в инвентаре нет медицинских предметов", new Vector2(ItemSlotRect(0).X, ItemSlotRect(0).Y + 14), Color.Gray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        for (var i = 0; i < medicine.Count; i++)
        {
            var (item, count) = medicine[i];
            var rect = ItemSlotRect(i);
            var helps = recommended.Contains(item);
            var hovered = hover.Kind == HealthHitKind.Item && hover.Index == i;
            spriteBatch.Draw(_pixel, rect, hovered ? new Color(64, 84, 108) : new Color(34, 40, 50));
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, helps ? Color.LightGreen : new Color(80, 90, 108), helps ? 2 : 1);
            ItemIcons.Draw(spriteBatch, _pixel, item, new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6));
            if (count > 1)
                spriteBatch.DrawString(_font, $"x{count}", new Vector2(rect.Right - 22, rect.Bottom - 14), Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
            var name = ItemDefinitions.DisplayName(item);
            spriteBatch.DrawString(_font, name.Length > 9 ? name[..9] : name, new Vector2(rect.X, rect.Bottom + 1), helps ? Color.LightGreen : Color.LightGray, 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);
        }

        var isSelf = target.PlayerId == me.PlayerId;
        var cprOn = !isSelf && body.InArrest;
        DrawButton(spriteBatch, CprRect, me.Treatment is { IsCpr: true } ? "Массаж сердца: идёт (держите)" : "Массаж сердца (держать кнопку)",
            hover.Kind == HealthHitKind.Cpr, cprOn);

        var hasKit = HasMedicine(me, ItemType.SurgeryKit);
        foreach (var organ in Enum.GetValues<Organ>())
        {
            var damage = body.OrganDamage(organ);
            var can = !isSelf && hasKit && body.Unconscious && damage > 0.5f;
            DrawButton(spriteBatch, SurgeryRect(organ), $"Операция: {MedicalTreatment.OrganName(organ).ToLowerInvariant()} ({damage:0}%)",
                hover.Kind == HealthHitKind.Surgery && hover.Organ == organ, can);
        }
    }

    private static bool HasMedicine(CharacterState me, ItemType item) => MedicineIn(me).Any(m => m.Item == item);

    private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string label, bool hovered, bool enabled)
    {
        spriteBatch.Draw(_pixel, rect, enabled ? (hovered ? new Color(74, 96, 124) : new Color(46, 58, 76)) : new Color(34, 36, 40));
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, enabled ? new Color(120, 150, 190) : new Color(64, 66, 72), 1);
        var size = _font.MeasureString(label) * 0.5f;
        spriteBatch.DrawString(_font, label, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), enabled ? Color.White : Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }
}
