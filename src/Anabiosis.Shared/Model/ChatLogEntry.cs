namespace Anabiosis.Shared.Model;

// One crew-wide text channel (direct user request, "как в Баротравме") - no radio device, no
// proximity gating, everyone connected always sees every message, matching how the rest of
// WorldSnapshot already broadcasts full state to everyone every tick. SenderName is resolved and
// baked in server-side at send time (not looked up again client-side), so a chat log entry keeps
// showing the name the sender had *at the time*, robust to them renaming mid-session.
public sealed record ChatLogEntry(int Id, int SenderPlayerId, string SenderName, string Text);
