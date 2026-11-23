namespace Anabiosis.Shared.Model;

// The lobby's campaign modifiers that actually reach the game: how much a finished quest pays, how much the trader asks (and gives), how much the
// shipyard (building compartments and doors in the station) costs. All 1.0 = the game as designed. They ride in every snapshot so the client's own price
// displays (the trade window, the build panel) show what the server will really charge.
public sealed record CampaignModifiers(float Reward = 1f, float ShopPrice = 1f, float ShipyardPrice = 1f)
{
    public const float Min = 0.1f;
    public const float Max = 5f;
    public const float Step = 0.1f;

    public static CampaignModifiers Default { get; } = new();

    public CampaignModifiers Clamped() => new(Snap(Reward), Snap(ShopPrice), Snap(ShipyardPrice));

    // To one decimal inside [Min, Max]: what the -/+ buttons can reach, whatever arrives from the wire.
    private static float Snap(float value) =>
        float.IsNaN(value) ? 1f : MathF.Round(Math.Clamp(value, Min, Max) * 10f) / 10f;

    // A price scaled, never below 1 credit for something that had a price.
    public static int Scale(int price, float multiplier) =>
        price <= 0 ? price : Math.Max(1, (int)MathF.Round(price * multiplier));
}
