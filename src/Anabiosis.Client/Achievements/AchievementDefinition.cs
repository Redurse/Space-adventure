using System;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Achievements;

// Direct user request ("система достижений... как в Стиме") - unlock-once-ever, checked purely
// client-side against the ordinary per-tick WorldSnapshot (no server/protocol change at all -
// nothing server-side needs to know an achievement exists, the same way ChangelogEntries needs no
// server support either). Condition takes the live snapshot, THIS player's own CharacterState (null
// before their character has ever spawned - still at the menu, or between death and respawn), and
// the AchievementTracker itself (only used by the couple of conditions that need one small bit of
// their own remembered state, like "which system did I start this session in").
public sealed record AchievementDefinition(
    string Id, string Name, string Description, string Glyph,
    Func<WorldSnapshot, CharacterState?, AchievementTracker, bool> Condition);
