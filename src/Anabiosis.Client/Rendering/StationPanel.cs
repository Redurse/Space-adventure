using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The Trader's buy/sell lists (M10 economy), the Administrator's delivery quest (M11,
// game_design.md section 7), and the Mechanic's ship upgrades (M13, game_design.md section 9) -
// shown as a small HUD dialogue panel once the player clicks an NPC physically standing in a
// station room (StationRenderer draws the room/NPCs themselves; this class no longer does).
public sealed class StationPanel
{
    private const int RowHeight = 20;
    private const int RowWidth = 210;
    private static readonly Vector2 TradeListOrigin = new(0, 170);
    private static readonly Vector2 SellColumnOffset = new(230, 0);
    private static readonly Vector2 ComponentColumnOffset = new(460, 0);

    // TradeCatalog.Goods[0..11] are general gear (fits the original single column); the 14 wiring
    // components appended after them (M23) get their own third column instead of stretching the
    // list past the panel's height.
    private const int ComponentColumnStart = 12;

    private readonly SpriteFont _font;

    public StationPanel(SpriteFont font)
    {
        _font = font;
    }

    // "Купить" column(s) — one row per TradeCatalog.Goods entry, in catalog order; entries from
    // ComponentColumnStart on spill into a second buy column so the list never grows past the panel.
    public static Rectangle GetGoodRect(int index, Vector2 panelOrigin)
    {
        var (columnOffset, row) = index < ComponentColumnStart
            ? (Vector2.Zero, index)
            : (ComponentColumnOffset, index - ComponentColumnStart);
        var origin = panelOrigin + TradeListOrigin + columnOffset + new Vector2(0, row * RowHeight);
        return new Rectangle((int)origin.X, (int)origin.Y, RowWidth, RowHeight - 2);
    }

    // "Продать" column — one row per main inventory slot index (not every slot is sellable; the
    // caller/click-handler still needs to check the slot actually holds a cataloged item).
    public static Rectangle GetSellRect(int slotIndex, Vector2 panelOrigin)
    {
        var origin = panelOrigin + TradeListOrigin + SellColumnOffset + new Vector2(0, slotIndex * RowHeight);
        return new Rectangle((int)origin.X, (int)origin.Y, RowWidth, RowHeight - 2);
    }

    // The Administrator's single action button — "turn in" once the active job is finishable,
    // absent (nothing to click) otherwise. Doubles as the job-board header when nothing's active.
    public static Rectangle GetAdminActionRect(Vector2 panelOrigin)
    {
        var origin = panelOrigin + new Vector2(0, 160);
        return new Rectangle((int)origin.X, (int)origin.Y, RowWidth, RowHeight - 2);
    }

    // The job board shown when no quest is active — one row per kind (game_design.md section 7).
    public static readonly QuestKind[] OfferedQuestKinds = { QuestKind.Delivery, QuestKind.Bounty, QuestKind.Mining };

    public static Rectangle GetQuestOfferRect(int index, Vector2 panelOrigin)
    {
        var origin = panelOrigin + new Vector2(0, 180 + index * RowHeight);
        return new Rectangle((int)origin.X, (int)origin.Y, RowWidth, RowHeight - 2);
    }

    public static string QuestKindLabel(QuestKind kind) => kind switch
    {
        QuestKind.Bounty => "охота за головой",
        QuestKind.Mining => "добыча руды",
        _ => "доставка груза",
    };

    // The Mechanic's upgrade list — one row per ShipUpgradeCatalog.Tracks entry, in catalog order.
    public static Rectangle GetUpgradeRect(int index, Vector2 panelOrigin)
    {
        var origin = panelOrigin + TradeListOrigin + new Vector2(0, index * RowHeight);
        return new Rectangle((int)origin.X, (int)origin.Y, RowWidth, RowHeight - 2);
    }

    // The Recruiter's board — one row per BotCandidate currently on offer, same geometry.
    public static Rectangle GetCandidateRect(int index, Vector2 panelOrigin)
    {
        var origin = panelOrigin + TradeListOrigin + new Vector2(0, index * RowHeight);
        return new Rectangle((int)origin.X, (int)origin.Y, RowWidth + 60, RowHeight - 2);
    }

    // The Shipwright's hull list — one row per ShipKind, same geometry as the other lists.
    public static readonly ShipKind[] PurchasableShipKinds = { ShipKind.Scout, ShipKind.Frigate, ShipKind.Cruiser };

    public static Rectangle GetShipRect(int index, Vector2 panelOrigin)
    {
        var origin = panelOrigin + TradeListOrigin + new Vector2(0, index * RowHeight);
        return new Rectangle((int)origin.X, (int)origin.Y, RowWidth + 120, RowHeight - 2);
    }

    // M61 - "снести отсек": no per-room picker yet (same "later milestone" deferral M60's own build
    // placement made), just one button that demolishes whichever player-built room has the highest
    // "room-N" suffix - the single most-recently-built one, the only one there usually is to undo.
    // Sits right below the hull-swap rows and a small gap - the old flat "Построить отсек" list that
    // used to sit between them moved out to StationBuildPanel (content-каталог отсеков's own
    // bottom-of-screen category/module UI).
    private const int DemolishRowOffset = 5; // PurchasableShipKinds.Length(3) + a 2-row gap
    public static Rectangle GetDemolishLastRoomRect(Vector2 panelOrigin)
    {
        var origin = panelOrigin + TradeListOrigin + new Vector2(0, DemolishRowOffset * RowHeight);
        return new Rectangle((int)origin.X, (int)origin.Y, RowWidth + 120, RowHeight - 2);
    }

    // Same id-suffix convention World.ShipBuilding.cs's own NextRoomId/Game1.ShipEditor.cs's
    // NextRoomCounter already use - the highest surviving "room-N" id is the most recently built
    // room (ids are never reused once freed, so this stays correct even after an earlier demolish).
    public static string? LastBuiltRoomId(IReadOnlyList<Room> rooms)
    {
        string? best = null;
        var bestN = -1;
        foreach (var room in rooms)
        {
            if (!room.Id.StartsWith("room-") || !int.TryParse(room.Id.AsSpan(5), out var n) || n <= bestN)
                continue;
            bestN = n;
            best = room.Id;
        }
        return best;
    }

    // Shown as a small HUD panel (like ReactorPanel/PowerPanel) once the player clicks an NPC
    // physically standing in a station room (StationRenderer draws the room/NPCs themselves) -
    // the sub-lists below (trade/quest/upgrade) are unchanged from when this used to be a
    // full-screen menu, just anchored to a HUD slot instead of taking over the whole screen.
    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, Vector2 panelOrigin, string? talkingToNpcId)
    {
        if (talkingToNpcId is null)
            return;

        var talkingTo = snapshot.Station.Npcs.FirstOrDefault(n => n.Id == talkingToNpcId);
        if (talkingTo is null)
            return;

        spriteBatch.DrawString(_font, $"Кредиты: {snapshot.Credits}", panelOrigin + new Vector2(0, -44),
            Color.LightGreen, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        // Reputation with whoever holds this station (game_design.md section 12) - it's what
        // drives the prices in the lists below and whether the Administrator offers work at all,
        // so it belongs right next to them rather than on a separate screen.
        var dockedPoint = snapshot.GalaxyPoints.FirstOrDefault(p => p.Id == snapshot.Voyage.DockedPointId);
        if (dockedPoint is not null)
        {
            var standing = snapshot.FactionStandings.FirstOrDefault(f => f.Faction == dockedPoint.Faction);
            if (standing is not null)
            {
                var label = FactionDefinitions.StandingLabel(standing.Standing);
                var color = standing.Standing >= FactionDefinitions.FriendlyThreshold ? Color.LightGreen
                    : standing.Standing <= FactionDefinitions.HostileThreshold ? Color.OrangeRed
                    : Color.LightGray;
                spriteBatch.DrawString(_font, $"{standing.Name}: {label} ({standing.Standing:+0;-0;0})",
                    panelOrigin + new Vector2(0, -26), color, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            }
        }

        var dialogueOrigin = panelOrigin + new Vector2(0, 140);
        spriteBatch.DrawString(_font, $"{talkingTo.Name}:", dialogueOrigin, Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        switch (talkingTo.Kind)
        {
            case NpcKind.Administrator:
                DrawAdminQuest(spriteBatch, snapshot, dialogueOrigin, panelOrigin);
                return;
            case NpcKind.Mechanic:
                DrawMechanicUpgrades(spriteBatch, snapshot, panelOrigin);
                return;
            case NpcKind.Shipwright:
                DrawShipyard(spriteBatch, snapshot, panelOrigin);
                return;
            case NpcKind.Recruiter:
                DrawRecruiterBoard(spriteBatch, snapshot, panelOrigin);
                return;
            default:
                DrawTraderLists(spriteBatch, snapshot, playerId, panelOrigin);
                return;
        }
    }

    // Hull list at the Shipwright (game_design.md section 9). Prices are shown net of the trade-in
    // on the current hull, which is what actually gets charged (World.ShipPurchase.cs) - trading
    // down therefore reads as a negative number, i.e. the yard pays you.
    private void DrawShipyard(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        var headerOrigin = panelOrigin + TradeListOrigin + new Vector2(0, -20);
        spriteBatch.DrawString(_font, $"Верфь (сейчас: {ShipCatalog.Name(snapshot.CurrentShipKind)})", headerOrigin,
            Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        for (var i = 0; i < PurchasableShipKinds.Length; i++)
        {
            var kind = PurchasableShipKinds[i];
            var rect = GetShipRect(i, panelOrigin);

            string label;
            Color color;
            if (kind == snapshot.CurrentShipKind)
            {
                label = $"{ShipCatalog.Name(kind)} — ваш текущий корабль";
                color = Color.LightGreen;
            }
            else
            {
                var cost = ShipCatalog.Price(kind) - ShipCatalog.TradeInValue(snapshot.CurrentShipKind);
                label = cost >= 0
                    ? $"{ShipCatalog.Name(kind)} — доплата {cost}"
                    : $"{ShipCatalog.Name(kind)} — возврат {-cost}";
                color = snapshot.Credits >= cost ? Color.White : Color.Gray;
            }

            spriteBatch.DrawString(_font, label, new Vector2(rect.X, rect.Y), color, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }

        // Содержательный каталог отсеков's own build UI (StationBuildPanel, the bottom-of-screen
        // category tabs + module row) replaced this panel's old flat "Построить отсек" text list -
        // Game1.cs's Draw shows that panel instead, right alongside this one, whenever the player's
        // talking to the Shipwright. Only "снести" (M61) is still this panel's own job.
        var platingOrigin = panelOrigin + TradeListOrigin + new Vector2(0, (DemolishRowOffset - 1) * RowHeight - 4);
        spriteBatch.DrawString(_font, $"Обшивка в трюме: {snapshot.HullPlatingStock}", platingOrigin, Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        // M61 - only shown once there's actually a player-built room to demolish.
        if (LastBuiltRoomId(snapshot.Rooms) is { } lastRoomId)
        {
            var demolishRect = GetDemolishLastRoomRect(panelOrigin);
            var roomName = snapshot.Rooms.First(r => r.Id == lastRoomId).Name;
            spriteBatch.DrawString(_font, $"Снести «{roomName}»", new Vector2(demolishRect.X, demolishRect.Y),
                Color.OrangeRed, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }
    }

    // The Recruiter's board (game_design.md section 10 - "случайный набор кандидатов... у каждого
    // своё имя/характеристики/специализация"). Hired crew already aboard (World.Recruiting.cs's
    // World.MaxHiredBots) shows as a plain headcount rather than a list — there's nothing to click
    // on a crew member who's just doing their job.
    private void DrawRecruiterBoard(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        var headerOrigin = panelOrigin + TradeListOrigin + new Vector2(0, -20);
        var hired = snapshot.Characters.Count(c => c.IsBot);
        spriteBatch.DrawString(_font, $"Экипаж на найме: {hired}/4", headerOrigin, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        if (snapshot.RecruitCandidates.Count == 0)
        {
            spriteBatch.DrawString(_font, "Сейчас никого нет на примете.", new Vector2(headerOrigin.X, headerOrigin.Y + RowHeight),
                Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            return;
        }

        for (var i = 0; i < snapshot.RecruitCandidates.Count; i++)
        {
            var candidate = snapshot.RecruitCandidates[i];
            var rect = GetCandidateRect(i, panelOrigin);
            var label = $"{candidate.Name} ({CrewRoles.Name(candidate.Role)}) - {candidate.Cost}";
            var affordable = snapshot.Credits >= candidate.Cost;
            spriteBatch.DrawString(_font, label, new Vector2(rect.X, rect.Y), affordable ? Color.White : Color.Gray,
                0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }
    }

    private void DrawMechanicUpgrades(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        var headerOrigin = panelOrigin + TradeListOrigin + new Vector2(0, -20);
        spriteBatch.DrawString(_font, "Прокачка корабля", headerOrigin, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        for (var i = 0; i < ShipUpgradeCatalog.Tracks.Count; i++)
        {
            var track = ShipUpgradeCatalog.Tracks[i];
            var level = snapshot.ShipUpgradeLevels.TryGetValue(track.Track, out var lvl) ? lvl : 0;
            var rect = GetUpgradeRect(i, panelOrigin);

            string label;
            Color color;
            if (level >= track.MaxLevel)
            {
                label = $"{track.Name}: {level}/{track.MaxLevel} (макс.)";
                color = Color.LightGreen;
            }
            else
            {
                var cost = track.CostPerLevel[level];
                var affordable = snapshot.Credits >= cost;
                label = $"{track.Name}: {level}/{track.MaxLevel} - след. уровень {cost}";
                color = affordable ? Color.White : Color.Gray;
            }

            spriteBatch.DrawString(_font, label, new Vector2(rect.X, rect.Y), color, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }
    }

    private void DrawAdminQuest(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 dialogueOrigin, Vector2 panelOrigin)
    {
        var actionRect = GetAdminActionRect(panelOrigin);
        var actionPosition = new Vector2(actionRect.X, actionRect.Y);

        if (snapshot.ActiveQuest is null)
        {
            // One row per kind of job on offer (game_design.md section 7) - picking is the
            // player's call, not a random draw.
            spriteBatch.DrawString(_font, "Доска заданий:", actionPosition, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            for (var i = 0; i < OfferedQuestKinds.Length; i++)
            {
                var rect = GetQuestOfferRect(i, panelOrigin);
                spriteBatch.DrawString(_font, $"[Клик] {QuestKindLabel(OfferedQuestKinds[i])}",
                    new Vector2(rect.X, rect.Y), Color.Yellow, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            }
            return;
        }

        var quest = snapshot.ActiveQuest;

        // Where a job can be handed in depends on its kind (World.Quests.cs): a delivery at its
        // destination, a bounty or a mining haul back at whoever issued it.
        var turnInHere = quest.Kind == QuestKind.Delivery
            ? quest.DestinationPointId == snapshot.Voyage.DockedPointId
            : quest.IssuedByPointId == snapshot.Voyage.DockedPointId;
        var objectiveMet = quest.Kind != QuestKind.Bounty || quest.ObjectiveComplete;

        if (turnInHere && objectiveMet)
        {
            spriteBatch.DrawString(_font, $"[Клик] Сдать задание (+{quest.RewardCredits} кред.)",
                actionPosition, Color.Yellow, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            return;
        }

        spriteBatch.DrawString(_font, $"Задание: {quest.Describe()} (+{quest.RewardCredits} кред.)",
            dialogueOrigin + new Vector2(0, 20), Color.LightGray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        // Same button as the turn-in above, shown instead of it while the job can't be finished
        // here - giving up costs standing with whoever issued it (World.Quests.cs's TryAbandonQuest).
        spriteBatch.DrawString(_font, "[Клик] Отказаться от задания", actionPosition,
            Color.OrangeRed, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private void DrawTraderLists(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, Vector2 panelOrigin)
    {
        var buyHeaderOrigin = panelOrigin + TradeListOrigin + new Vector2(0, -20);
        spriteBatch.DrawString(_font, "Купить", buyHeaderOrigin, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, "Продать", buyHeaderOrigin + SellColumnOffset, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, "Компоненты", buyHeaderOrigin + ComponentColumnOffset, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        for (var i = 0; i < TradeCatalog.Goods.Count; i++)
        {
            var good = TradeCatalog.Goods[i];
            var rect = GetGoodRect(i, panelOrigin);
            var label = $"{ItemDefinitions.DisplayName(good.Item)} - {good.BuyPrice}";
            var affordable = snapshot.Credits >= good.BuyPrice;
            spriteBatch.DrawString(_font, label, new Vector2(rect.X, rect.Y), affordable ? Color.White : Color.Gray,
                0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        if (me?.Inventory is not { } inventory)
            return;

        for (var i = 0; i < inventory.MainSlots.Count; i++)
        {
            if (inventory.MainSlots[i] is not { } item || TradeCatalog.Find(item) is not { } good)
                continue;

            var rect = GetSellRect(i, panelOrigin);
            var label = $"{ItemDefinitions.DisplayName(item)} - {good.SellPrice}";
            spriteBatch.DrawString(_font, label, new Vector2(rect.X, rect.Y), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }
    }
}
