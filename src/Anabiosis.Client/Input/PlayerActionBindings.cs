using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace Anabiosis.Client.Input;

// Direct user request ("чтобы в 3 настройке были расписаны все активные действия на клавишах и
// чтобы я мог их назначать и чтобы с перезаходом они сохранялись") - every gameplay action that
// used to read a hardcoded Keys.X literal directly now goes through one of these instead. Menu/UI
// navigation (Escape, Enter-to-confirm on other screens) and dev-diagnostic keys (F3, F11, the
// tilde cheat panel, the Ъ tile-grid overlay) are deliberately NOT here - see the audit this was
// built from: those aren't the kind of thing a player would expect in a Controls list, and F11/F3
// etc changing out from under a rebind would be its own kind of confusing.
//
// Arrow keys stay a hardcoded, always-on SECOND way to move (ReadMoveInput/ReadAimDirection each
// still check them directly, in addition to whatever this rebinds) - preserves today's behavior
// for anyone already used to arrows, without needing more rebindable rows for a second control
// scheme nobody asked to remap.
//
// Direct user request ("уберём возможность управлять кораблём игроку... автопилот") - manual
// flight is gone entirely (ReadHelmInput and everything it drove - HelmTurnLeft/HelmTurnRight, the
// A/D-as-strafe reuse of MoveLeft/MoveRight while at helm), replaced by clicking a destination and
// holding RMB to aim the nose (Game1.Input.cs's own new click/RMB handling, gated on IsAtHelm the
// same way the old stick input was). MoveUp/Down/Left/Right (W/A/S/D) are walking-only again now.
// HelmStabilize renamed to AutopilotStop (same default key, X) - "Стоп" cancels the current
// destination instead of releasing a manual throttle stick that no longer exists.
//
// ToggleHelmMode retired earlier this session too (direct user request - "хочу чтобы режим рсу
// был всегда включен... полностью удали кнопку") - there was no longer an Arc/Rcs mode to switch
// between even before autopilot replaced manual flight outright.
public enum PlayerAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Interact,
    Fire,
    AutopilotStop,
    ToggleLanding,
    VoicePushToTalk,
    RadioPushToTalk,
    ToggleGalacticMap,
    OpenChat,
}

public sealed class PlayerActionBindings
{
    public static readonly IReadOnlyList<PlayerAction> All = Enum.GetValues<PlayerAction>();

    private static readonly Dictionary<PlayerAction, Keys> Defaults = new()
    {
        [PlayerAction.MoveUp] = Keys.W,
        [PlayerAction.MoveDown] = Keys.S,
        [PlayerAction.MoveLeft] = Keys.A,
        [PlayerAction.MoveRight] = Keys.D,
        [PlayerAction.Interact] = Keys.F,
        [PlayerAction.Fire] = Keys.Space,
        [PlayerAction.AutopilotStop] = Keys.X,
        [PlayerAction.ToggleLanding] = Keys.L,
        [PlayerAction.VoicePushToTalk] = Keys.V,
        [PlayerAction.RadioPushToTalk] = Keys.R,
        [PlayerAction.ToggleGalacticMap] = Keys.M,
        [PlayerAction.OpenChat] = Keys.Enter,
    };

    private static readonly Dictionary<PlayerAction, string> Labels = new()
    {
        [PlayerAction.MoveUp] = "Движение — вверх",
        [PlayerAction.MoveDown] = "Движение — вниз",
        [PlayerAction.MoveLeft] = "Движение — влево",
        [PlayerAction.MoveRight] = "Движение — вправо",
        [PlayerAction.Interact] = "Взаимодействие",
        [PlayerAction.Fire] = "Стрельба / использовать",
        [PlayerAction.AutopilotStop] = "Штурвал — стоп (отменить курс)",
        [PlayerAction.ToggleLanding] = "Штурвал — посадка / взлёт",
        [PlayerAction.VoicePushToTalk] = "Голосовой чат (рядом)",
        [PlayerAction.RadioPushToTalk] = "Голосовой чат (рация)",
        [PlayerAction.ToggleGalacticMap] = "Карта галактики",
        [PlayerAction.OpenChat] = "Открыть чат экипажа",
    };

    public static string Label(PlayerAction action) => Labels[action];
    public static Keys DefaultFor(PlayerAction action) => Defaults[action];

    private readonly Dictionary<PlayerAction, Keys> _bindings = new(Defaults);

    public Keys Get(PlayerAction action) => _bindings.TryGetValue(action, out var key) ? key : Defaults[action];
    public void Set(PlayerAction action, Keys key) => _bindings[action] = key;
    public void ResetToDefault(PlayerAction action) => _bindings[action] = Defaults[action];

    public void ResetAll()
    {
        foreach (var (action, key) in Defaults)
            _bindings[action] = key;
    }

    public PlayerActionBindings Clone()
    {
        var copy = new PlayerActionBindings();
        foreach (var (action, key) in _bindings)
            copy._bindings[action] = key;
        return copy;
    }

    // Flat "Action=Key;Action=Key" text, not JSON - PlayerSettings/GraphicsSettings (Player
    // SettingsStore.cs) are otherwise entirely flat scalar fields with nothing nested, so this
    // keeps a new KeyBindings field the same shape as every field already next to it rather than
    // being the first non-scalar one. Unknown/malformed entries are just skipped (TryParse guards),
    // so a hand-edited or half-corrupted string degrades to "some bindings reset to default"
    // instead of failing to load at all - the same swallow-failures spirit PlayerSettingsStore's
    // own Load already uses for a bad file.
    public string ToSettingsString() =>
        string.Join(';', _bindings.Select(kv => $"{kv.Key}={kv.Value}"));

    public static PlayerActionBindings FromSettingsString(string? raw)
    {
        var bindings = new PlayerActionBindings();
        if (string.IsNullOrEmpty(raw))
            return bindings;
        foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split('=', 2);
            if (parts.Length == 2 && Enum.TryParse<PlayerAction>(parts[0], out var action) && Enum.TryParse<Keys>(parts[1], out var key))
                bindings.Set(action, key);
        }
        return bindings;
    }

    // A short, readable name for the rebind row's own "socket" button - Keys.ToString() is already
    // fine for the vast majority (letters, F-keys, arrows); only the couple of keys in the default
    // set whose raw enum name reads oddly get a nicer label.
    public static string KeyDisplayName(Keys key) => key switch
    {
        Keys.Space => "Пробел",
        Keys.Enter => "Enter",
        _ => key.ToString(),
    };
}
