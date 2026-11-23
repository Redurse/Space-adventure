using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The window of a station on the line (Cosmoteer's CommWindow): tabs for what the station offers - Trade (the store window, TradeWindow), Hire Crew and the Mission
// Board - and a button to hang up. The store keeps its own full-screen frame; the tabs are drawn over its header, and the other two tabs use the same frame.
public sealed class CommsStationWindow
{
    public enum Tab { Trade, Hire, Jobs }

    private static readonly string[] TabLabels = { "ТОРГОВЛЯ", "НАЙМ ЭКИПАЖА", "ЗАДАНИЯ" };
    private static readonly Color Gold = new(214, 178, 112);
    private static readonly Color PanelFill = new(16, 20, 28);
    private static readonly Color RowFill = new(30, 36, 48);
    private static readonly Color RowHover = new(46, 56, 74);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public CommsStationWindow(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle Frame => TradeWindow.WindowRect;
    public static Rectangle TabRect(int index) => new(Frame.X + 372 + index * 156, Frame.Y + 6, 150, 28);
    public static Rectangle EndRect => new(Frame.X + 10, Frame.Bottom - 44, 136, 34);
    private static Rectangle RowRect(int index) => new(Frame.X + 170, Frame.Y + 120 + index * 56, 760, 48);
    public static Rectangle HireButtonRect(int index) => new(RowRect(index).Right - 130, RowRect(index).Y + 8, 120, 32);
    public static Rectangle JobButtonRect(int index) => new(RowRect(index).Right - 130, RowRect(index).Y + 8, 120, 32);

    public static bool Available(CommsState comms, Tab tab) => tab switch
    {
        Tab.Trade => comms.HasTrader,
        Tab.Hire => comms.HasRecruiter,
        _ => comms.HasAdministrator,
    };

    // The first tab the station can serve (Trade, Hire, Jobs in that order); Trade when it offers nothing at all.
    public static Tab FirstAvailable(CommsState comms) =>
        new[] { Tab.Trade, Tab.Hire, Tab.Jobs }.FirstOrDefault(t => Available(comms, t));

    // The tab strip and the hang-up button, over whichever tab is open (over the store's own header for Trade).
    public void DrawChrome(SpriteBatch spriteBatch, CommsState comms, Tab current, Point mouse)
    {
        for (var i = 0; i < TabLabels.Length; i++)
        {
            var tab = (Tab)i;
            var rect = TabRect(i);
            var available = Available(comms, tab);
            var selected = tab == current;
            spriteBatch.Draw(_pixel, rect, selected ? new Color(70, 58, 36) : available && rect.Contains(mouse) ? RowHover : RowFill);
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, selected ? Gold : new Color(70, 78, 96), selected ? 2 : 1);
            Centered(spriteBatch, TabLabels[i], rect.Center.X, rect.Y + 7, available ? (selected ? Gold : Color.LightGray) : Color.Gray * 0.5f, 0.5f);
        }

        var end = EndRect;
        spriteBatch.Draw(_pixel, end, end.Contains(mouse) ? new Color(176, 80, 72) : new Color(140, 60, 56));
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, end, new Color(220, 150, 140), 1);
        Centered(spriteBatch, "ЗАВЕРШИТЬ СВЯЗЬ", end.Center.X, end.Y + 9, Color.White, 0.44f);
    }

    private void Frame_(SpriteBatch spriteBatch, WorldSnapshot snapshot, string title)
    {
        spriteBatch.Draw(_pixel, Frame, PanelFill * 0.97f);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, Frame, Gold, 2);
        spriteBatch.Draw(_pixel, new Rectangle(Frame.X, Frame.Y, Frame.Width, 40), new Color(24, 30, 42));
        Text(spriteBatch, title, new Vector2(Frame.X + 14, Frame.Y + 10), Gold, 0.72f);
        var credits = $"Кредиты: {snapshot.Credits}";
        Text(spriteBatch, credits, new Vector2(Frame.Right - 24 - _font.MeasureString(credits).X * 0.68f, Frame.Y + 11), Color.LightGreen, 0.68f);
    }

    public void DrawHire(SpriteBatch spriteBatch, WorldSnapshot snapshot, string stationName, Point mouse)
    {
        Frame_(spriteBatch, snapshot, $"НАЙМ ЭКИПАЖА - {stationName}");
        var hired = snapshot.Characters.Count(c => c.IsBot);
        Text(spriteBatch, $"Экипаж на найме: {hired}/{Anabiosis.Server.World.MaxHiredBots}", new Vector2(Frame.X + 170, Frame.Y + 76), Color.White, 0.6f);
        if (snapshot.RecruitCandidates.Count == 0)
        {
            Text(spriteBatch, "Сейчас никого нет на примете.", new Vector2(Frame.X + 170, Frame.Y + 126), Color.Gray, 0.58f);
            return;
        }
        for (var i = 0; i < snapshot.RecruitCandidates.Count; i++)
        {
            var candidate = snapshot.RecruitCandidates[i];
            var row = RowRect(i);
            spriteBatch.Draw(_pixel, row, RowFill);
            Text(spriteBatch, candidate.Name, new Vector2(row.X + 14, row.Y + 7), Color.White, 0.62f);
            Text(spriteBatch, CrewRoles.Name(candidate.Role), new Vector2(row.X + 14, row.Y + 27), Color.LightGray, 0.46f);
            Text(spriteBatch, $"{candidate.Cost} кред.", new Vector2(row.X + 420, row.Y + 14), Color.LightGreen, 0.6f);
            var canHire = snapshot.Credits >= candidate.Cost && hired < Anabiosis.Server.World.MaxHiredBots;
            Button(spriteBatch, HireButtonRect(i), "НАНЯТЬ", canHire, mouse);
        }
    }

    public void DrawJobs(SpriteBatch spriteBatch, WorldSnapshot snapshot, string stationName, Point mouse)
    {
        Frame_(spriteBatch, snapshot, $"ДОСКА ЗАДАНИЙ - {stationName}");
        if (snapshot.ActiveQuest is { } quest)
        {
            Text(spriteBatch, "Текущее задание:", new Vector2(Frame.X + 170, Frame.Y + 80), Gold, 0.62f);
            Text(spriteBatch, $"{quest.Describe()} (+{quest.RewardCredits} кред.)", new Vector2(Frame.X + 170, Frame.Y + 112), Color.White, 0.6f);
            Text(spriteBatch, quest.ObjectiveComplete ? "Цель выполнена." : "Цель ещё не выполнена.",
                new Vector2(Frame.X + 170, Frame.Y + 146), quest.ObjectiveComplete ? Color.LightGreen : Color.LightGray, 0.52f);
            Text(spriteBatch, "Сдать задание и получить награду можно только на самой станции: пристыкуйтесь.",
                new Vector2(Frame.X + 170, Frame.Y + 178), Color.Gray, 0.46f);
            return;
        }
        Text(spriteBatch, "Доступные задания:", new Vector2(Frame.X + 170, Frame.Y + 80), Gold, 0.62f);
        for (var i = 0; i < StationPanel.OfferedQuestKinds.Length; i++)
        {
            var row = RowRect(i);
            spriteBatch.Draw(_pixel, row, RowFill);
            var label = StationPanel.QuestKindLabel(StationPanel.OfferedQuestKinds[i]);
            Text(spriteBatch, char.ToUpperInvariant(label[0]) + label[1..], new Vector2(row.X + 14, row.Y + 14), Color.White, 0.64f);
            Button(spriteBatch, JobButtonRect(i), "ВЗЯТЬ", true, mouse);
        }
    }

    // The click targets of the open tab, for the caller to test: which candidate's "НАНЯТЬ" and which job's "ВЗЯТЬ" the point is on (-1 = none).
    public static int HireIndexAt(WorldSnapshot snapshot, Point mouse)
    {
        for (var i = 0; i < snapshot.RecruitCandidates.Count; i++)
            if (HireButtonRect(i).Contains(mouse))
                return i;
        return -1;
    }

    public static int JobIndexAt(Point mouse)
    {
        for (var i = 0; i < StationPanel.OfferedQuestKinds.Length; i++)
            if (JobButtonRect(i).Contains(mouse))
                return i;
        return -1;
    }

    private void Button(SpriteBatch spriteBatch, Rectangle rect, string label, bool enabled, Point mouse)
    {
        var fill = !enabled ? new Color(60, 64, 72) : rect.Contains(mouse) ? new Color(96, 150, 100) : new Color(70, 120, 80);
        spriteBatch.Draw(_pixel, rect, fill);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, enabled ? new Color(170, 220, 170) : new Color(90, 96, 108), 1);
        Centered(spriteBatch, label, rect.Center.X, rect.Y + 8, enabled ? Color.White : Color.Gray, 0.5f);
    }

    private void Text(SpriteBatch spriteBatch, string text, Vector2 at, Color color, float scale) =>
        spriteBatch.DrawString(_font, text, at, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

    private void Centered(SpriteBatch spriteBatch, string text, float centerX, float y, Color color, float scale)
    {
        var size = _font.MeasureString(text) * scale;
        spriteBatch.DrawString(_font, text, new Vector2(centerX - size.X / 2f, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
