using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Shown only while the reactor block is "open" (game_design.md section 1). 4 fuel-rod slots —
// click one to insert a rod held in hand, or click a loaded slot to take its rod back.
//
// W3 of wiring-plan.md (direct user request - a fusion reactor with Barotrauma-style graphs): the
// start/stop button, the readouts (output, the grid's load, plasma temperature, fuel) and the
// graph Barotrauma's reactor screen is known for - output against load over the last minute, so a
// spin-up, a spin-down or a load the reactor is still catching up with is something you watch
// happen. The history is sampled client-side from every snapshot (Record), not sent over the wire.
public sealed class ReactorPanel
{
    public const int SlotSize = 30;
    public const int SlotSpacing = 6;

    private const float SampleIntervalSeconds = 0.25f;
    private const int HistoryLength = 240; // a minute at four samples a second

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private readonly float[] _deliveredHistory = new float[HistoryLength];
    private readonly float[] _loadHistory = new float[HistoryLength];
    private int _historyCount;
    private int _historyHead;
    private float _nextSampleAt;

    public ReactorPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetSlotRect(int index, Vector2 origin)
    {
        var slotOrigin = origin + new Vector2(0, 24) + new Vector2(index * (SlotSize + SlotSpacing), 0);
        return new Rectangle((int)slotOrigin.X, (int)slotOrigin.Y, SlotSize, SlotSize);
    }

    // The start/stop button, right of the rod slots - shared with the click handler (Game1.Input.cs).
    public static Rectangle GetPowerButtonRect(Vector2 origin) =>
        new((int)origin.X + 156, (int)origin.Y + 24, 92, SlotSize);

    private static Rectangle GetGraphRect(Vector2 origin) => new((int)origin.X, (int)origin.Y + 62, 380, 84);

    // Called every frame, panel open or not, so the graph already has a history the moment the
    // reactor is opened.
    public void Record(PowerState power, float totalSeconds)
    {
        if (totalSeconds < _nextSampleAt)
            return;
        _nextSampleAt = totalSeconds + SampleIntervalSeconds;
        _deliveredHistory[_historyHead] = power.ReactorDelivered;
        _loadHistory[_historyHead] = power.ReactorLoad;
        _historyHead = (_historyHead + 1) % HistoryLength;
        _historyCount = Math.Min(HistoryLength, _historyCount + 1);
    }

    public void Draw(SpriteBatch spriteBatch, ReactorState reactor, PowerState power, Vector2 origin, float totalSeconds)
    {
        // The housing is drawn around the content rather than the content being re-laid-out
        // inside it: the slot rectangles are already published by GetSlotRect and the click
        // handler uses the same numbers, so moving them would mean moving input too.
        var bounds = DevicePanelChrome.StandardBounds(origin);
        var phosphor = new Color(236, 176, 92);
        DevicePanelChrome.Draw(spriteBatch, _font, bounds, "ТЕРМОЯДЕРНЫЙ РЕАКТОР", "RX-04", phosphor, totalSeconds);

        var deliveredColor = new Color(120, 230, 150);
        var loadColor = new Color(240, 170, 80);
        DevicePanelChrome.DrawReadout(spriteBatch, _font, origin + new Vector2(0, -6),
            "ВЫРАБОТКА", $"{power.ReactorDelivered:0}", $"/ {reactor.MaxOutput:0}", deliveredColor);
        DevicePanelChrome.DrawReadout(spriteBatch, _font, origin + new Vector2(100, -6),
            "НАГРУЗКА", $"{power.ReactorLoad:0}", "", loadColor);
        DevicePanelChrome.DrawReadout(spriteBatch, _font, origin + new Vector2(190, -6),
            "ПЛАЗМА", $"{power.PlasmaTemperature:0}", "млн K", phosphor);
        DevicePanelChrome.DrawReadout(spriteBatch, _font, origin + new Vector2(290, -6),
            "ТОПЛИВО", $"{reactor.Fuel:0}", $"/ {reactor.MaxFuel:0}",
            reactor.Fuel <= reactor.MaxFuel * 0.2f ? new Color(232, 108, 84) : phosphor);

        DrawRodSlots(spriteBatch, reactor, origin);
        DrawPowerButton(spriteBatch, power, reactor, origin, totalSeconds);
        DrawGraph(spriteBatch, reactor, origin, deliveredColor, loadColor);

        spriteBatch.DrawString(_font, "Клик по слоту: вставить/забрать стержень", origin + new Vector2(0, 150),
            new Color(170, 170, 120), 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawRodSlots(SpriteBatch spriteBatch, ReactorState reactor, Vector2 origin)
    {
        for (var i = 0; i < reactor.RodCharges.Count; i++)
        {
            var rect = GetSlotRect(i, origin);

            spriteBatch.Draw(_pixel, rect, Color.DimGray * 0.5f);
            DrawRectOutline(spriteBatch, rect, Color.LightGray, 1);

            if (reactor.RodCharges[i] is not { } charge)
                continue;

            // The rod body is always drawn, and only the charge left in it is lit up from the
            // bottom — a spent rod still occupying a slot has to look different from an empty
            // slot, otherwise "swap the dead one out" isn't a readable instruction.
            const int margin = 4;
            var body = new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2);
            spriteBatch.Draw(_pixel, body, Color.DarkSlateGray);

            var litHeight = (int)(body.Height * MathHelper.Clamp(charge, 0f, 1f));
            if (litHeight > 0)
            {
                var fill = charge > 0.5f ? Color.YellowGreen : charge > 0.2f ? Color.Orange : Color.OrangeRed;
                spriteBatch.Draw(_pixel, new Rectangle(body.X, body.Bottom - litHeight, body.Width, litHeight), fill);
            }

            var label = litHeight > 0 ? "Т" : "0%";
            spriteBatch.DrawString(_font, label, new Vector2(rect.X + 9, rect.Y + 8), litHeight > 0 ? Color.Black : Color.OrangeRed, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }
    }

    // ПУСК when stopped, СТОП while running; the lamp beside the word says what the plasma is
    // actually doing - spinning up, running, spinning down, or held dark by the emergency lever,
    // damage or an empty fuel load.
    private void DrawPowerButton(SpriteBatch spriteBatch, PowerState power, ReactorState reactor, Vector2 origin, float totalSeconds)
    {
        var rect = GetPowerButtonRect(origin);
        var running = power.ReactorRunning;
        var face = running ? new Color(150, 46, 40) : new Color(40, 120, 60);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X + 1, rect.Y + 2, rect.Width, rect.Height), Color.Black * 0.4f);
        spriteBatch.Draw(_pixel, rect, face);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), Color.White * 0.3f);
        DrawRectOutline(spriteBatch, rect, Color.Black * 0.6f, 1);
        var text = running ? "СТОП" : "ПУСК";
        var size = _font.MeasureString(text) * 0.7f;
        spriteBatch.DrawString(_font, text, new Vector2(rect.X + 28, rect.Center.Y - size.Y / 2f), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        var blink = 0.5f + 0.5f * MathF.Sin(totalSeconds * 6f);
        var lit = reactor.CurrentOutput > 0.01f;
        var lamp = !lit && running ? new Color(240, 80, 60) * blink        // lit but held dark (lever, damage, no fuel)
            : lit && running && power.ReactorDelivered < power.ReactorLoad - 0.5f ? new Color(250, 200, 70) * blink // still catching up
            : lit ? new Color(110, 230, 120)
            : new Color(60, 64, 70);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X + 8, rect.Center.Y - 5, 10, 10), Color.Black * 0.6f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X + 9, rect.Center.Y - 4, 8, 8), lamp);
    }

    private void DrawGraph(SpriteBatch spriteBatch, ReactorState reactor, Vector2 origin, Color deliveredColor, Color loadColor)
    {
        var rect = GetGraphRect(origin);
        spriteBatch.Draw(_pixel, rect, new Color(10, 14, 12));
        DrawRectOutline(spriteBatch, rect, new Color(60, 70, 64), 1);
        for (var i = 1; i < 4; i++)
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + 1, rect.Y + rect.Height * i / 4, rect.Width - 2, 1), new Color(30, 44, 36));
        for (var i = 1; i < 6; i++)
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + rect.Width * i / 6, rect.Y + 1, 1, rect.Height - 2), new Color(30, 44, 36));

        if (_historyCount >= 2)
        {
            var top = Math.Max(1f, reactor.MaxOutput);
            for (var i = 0; i < _historyCount; i++)
                top = Math.Max(top, Math.Max(_loadHistory[i], _deliveredHistory[i]));

            Vector2 Point(float[] series, int i)
            {
                var index = (_historyHead - _historyCount + i + HistoryLength) % HistoryLength;
                var x = rect.Right - 2 - (_historyCount - 1 - i) * (rect.Width - 4f) / (HistoryLength - 1);
                var y = rect.Bottom - 3 - series[index] / top * (rect.Height - 6f);
                return new Vector2(x, y);
            }

            // Load as a wide, dim band under a thin bright output line: in the usual case the two
            // coincide (the reactor is keeping up) and both stay visible - green inside orange.
            for (var i = 1; i < _historyCount; i++)
                HudIcons.DrawLine(spriteBatch, _pixel, Point(_loadHistory, i - 1), Point(_loadHistory, i), loadColor * 0.55f, 5f);
            for (var i = 1; i < _historyCount; i++)
                HudIcons.DrawLine(spriteBatch, _pixel, Point(_deliveredHistory, i - 1), Point(_deliveredHistory, i), deliveredColor, 1.5f);
        }

        spriteBatch.DrawString(_font, "выработка", new Vector2(rect.X + 6, rect.Y + 3), deliveredColor, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, "нагрузка", new Vector2(rect.X + 80, rect.Y + 3), loadColor, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, "1 мин", new Vector2(rect.Right - 36, rect.Y + 3), new Color(120, 138, 150), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
