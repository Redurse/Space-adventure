namespace SpaceAdventure.Shared.Model;

// Click it while docked to visit the station's NPCs (game_design.md section 10). Does nothing
// while the ship isn't actually docked — gated client-side off VoyagePhase.Station.
public sealed record AirlockConsole(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
