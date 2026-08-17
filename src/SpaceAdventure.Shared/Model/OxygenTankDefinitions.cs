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

    // Around seven minutes of vacuum on one tank. Long enough to cover a mining run out and back,
    // or a fight fought in a ship with its rooms open to space - a tank that runs out during an
    // ordinary battle would make the suit a liability rather than equipment - and short enough that
    // a spare is worth carrying on a long trip outside.
    public const float SuitDrainPerSecond = 0.22f;

    // Cutting is the expensive use: about half a minute of continuous flame per tank, so working a
    // vein dry costs most of a tank and a careless burn costs the trip home.
    public const float CutterDrainPerSecond = 3.2f;
}
