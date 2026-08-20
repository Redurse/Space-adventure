namespace SpaceAdventure.Shared.Model;

// The oxygen tank is the first item in the game that plugs into another item instead of being used
// on its own. A suit and a cutter each have one socket, and neither works without a charged tank in
// it: the suit stops keeping anyone alive in vacuum, the cutter doesn't light.
//
// That turns two things that were free into things to plan around - going outside and cutting ore
// both burn a consumable, and coming back for a fresh tank is part of the trip.
public static class OxygenTankDefinitions
{
    public const float FullCharge = 100f;

    // Around half an hour of vacuum on one tank (a quarter of the original ~7-minute rate) -
    // long enough that a suit's own air stops being the thing that ends an EVA trip.
    public const float SuitDrainPerSecond = 0.055f;

    // Cutting is the expensive use: about half a minute of continuous flame per tank, so working a
    // vein dry costs most of a tank and a careless burn costs the trip home.
    public const float CutterDrainPerSecond = 3.2f;
}
