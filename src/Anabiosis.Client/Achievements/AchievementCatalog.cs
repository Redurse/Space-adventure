using System.Collections.Generic;
using System.Linq;

namespace Anabiosis.Client.Achievements;

// Direct user request ("сделай достижений 20") - a starter set (the user is picking the REAL 20
// themselves; this proves the plumbing end to end) deliberately covering a wide variety of
// condition SHAPES - a one-off state flip (Voyage.IsInBattle), a count (Characters.Count(IsBot)),
// a credits/level threshold, "ever saw this list non-empty" (PendingRoomBuilds) - so swapping any
// entry for a real one later is a matter of copying the closest-shaped example, not inventing the
// plumbing from scratch. Glyphs are single Latin/Cyrillic letters or digits only, same convention
// CustomDeviceCatalog.ShortGlyph already uses - this project's own SpriteFont has a fixed, hand-
// picked glyph set (Latin+Cyrillic+digits for the UI text everywhere else) and throws on anything
// outside it, so no emoji/symbols here despite how a Steam achievement icon might normally look.
public static class AchievementCatalog
{
    public static readonly IReadOnlyList<AchievementDefinition> All = new[]
    {
        new AchievementDefinition("first_flight", "Первый полёт", "Задайте курс автопилоту", "A",
            (s, _, _) => s.Autopilot is { IsActive: true }),
        // NOT a bare "docked now" check - a fresh campaign STARTS already docked at the home
        // station (World.cs's own doc comment, "a fresh run starts docked"), so that would unlock
        // on tick 0 of any new game. AchievementTracker.HasReDocked requires having left first.
        new AchievementDefinition("first_dock", "Первая стыковка", "Пристыкуйтесь к станции", "D",
            (s, _, tracker) => tracker.HasReDocked(s.Voyage.DockedPointId is not null)),
        new AchievementDefinition("first_landing", "Первая посадка", "Посадите корабль на поверхность планеты", "L",
            (s, _, _) => s.Voyage.LandedBodyId is not null),
        new AchievementDefinition("first_battle", "Крещение боем", "Ввяжитесь в бой", "B",
            (s, _, _) => s.Voyage.IsInBattle),
        new AchievementDefinition("first_eva", "В открытый космос", "Выйдите в открытый космос без корабля", "V",
            (_, me, _) => me?.IsOutside == true),
        new AchievementDefinition("first_recruit", "Новый рекрут", "Наймите первого члена экипажа", "R",
            (s, _, _) => s.Characters.Any(c => c.IsBot)),
        new AchievementDefinition("growing_crew", "Экипаж растёт", "Наймите 3 членов экипажа", "C",
            (s, _, _) => s.Characters.Count(c => c.IsBot) >= 3),
        new AchievementDefinition("first_death", "Первая смерть", "Погибните и возродитесь", "X",
            (_, me, _) => (me?.RespawnSecondsRemaining ?? 0f) > 0f),
        new AchievementDefinition("gunner", "Стрелок", "Встаньте за турель", "G",
            (s, me, _) => me is not null && s.TurretStates.Any(t => t.MannedByPlayerId == me.PlayerId)),
        // NOT EnemyShip.Rooms.Count > 0 - that list is the enemy hull's own static room TEMPLATE,
        // resent every tick regardless of whether any boarding is actually happening (World.cs's own
        // CreateSnapshot, unconditional), so it's never empty from the very first tick of any
        // campaign. CharacterState.OnEnemyShip is the real "physically standing over there right
        // now" fact (World.PersonalShots.cs already keys shot VFX off the same field).
        new AchievementDefinition("boarding", "Абордаж", "Возьмите на абордаж вражеский корабль", "O",
            (_, me, _) => me?.OnEnemyShip == true),
        new AchievementDefinition("traveler", "Путешественник", "Перейдите в другую звёздную систему", "T",
            (s, _, tracker) => tracker.HasChangedSystem(s.CurrentSystemId)),
        new AchievementDefinition("credits_1000", "Мимо кассы", "Накопите 1000 кредитов", "1",
            (s, _, _) => s.Credits >= 1000),
        new AchievementDefinition("credits_5000", "Круглая сумма", "Накопите 5000 кредитов", "5",
            (s, _, _) => s.Credits >= 5000),
        new AchievementDefinition("credits_20000", "Магнат", "Накопите 20000 кредитов", "9",
            (s, _, _) => s.Credits >= 20000),
        new AchievementDefinition("time_x10", "Ускоритель времени", "Включите ускорение времени x10", "2",
            (s, _, _) => s.TimeAccelerationLevel >= 10),
        new AchievementDefinition("time_x1000", "Хроноскок", "Включите максимальное ускорение времени", "3",
            (s, _, _) => s.TimeAccelerationLevel >= 1000),
        new AchievementDefinition("upgrade_lv3", "Полный апгрейд", "Прокачайте систему корабля до 3 уровня", "U",
            (s, _, _) => s.ShipUpgradeLevels.Values.Any(level => level >= 3)),
        new AchievementDefinition("card_player", "Карточный игрок", "Сыграйте партию в Дурака за карточным столом", "K",
            (s, _, _) => s.CardGame is not null),
        new AchievementDefinition("fronts_player", "Фронтовик", "Сыграйте партию в «Фронты»", "F",
            (s, _, _) => s.FrontsGame is not null),
        new AchievementDefinition("shipwright", "Инженер космоса", "Начните постройку нового отсека", "S",
            (s, _, _) => s.PendingRoomBuilds is { Count: > 0 }),
    };
}
