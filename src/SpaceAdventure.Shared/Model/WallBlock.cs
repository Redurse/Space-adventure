namespace SpaceAdventure.Shared.Model;

// A single 1x1 segment of a room's wall (game_design.md sections 1-2 — block-based structure,
// continuous player movement). Most are the OUTER hull - exterior edges, breachable, vent oxygen
// when broken (World.Atmosphere.cs). IsInterior marks the other kind (enemy/weapon overhaul -
// "внутренние стены корабля также блокировали снаряды врага"): a bulkhead shared with another
// pressurized room, generated the same way and just as solid to a shot (World.EnemyAi.cs's
// ApplyEnemyAttack treats every WallBlock alike), but with nothing to decompress into on the other
// side - World.Atmosphere.cs's own leak sum and the client's steam-particle effect
// (AtmosphereParticles.cs) both skip it specifically for that reason.
// X/Y is the block's center point.
public sealed record WallBlock(string Id, string RoomId, float X, float Y, bool IsInterior = false)
{
    public Vec2 Position => new(X, Y);
}
