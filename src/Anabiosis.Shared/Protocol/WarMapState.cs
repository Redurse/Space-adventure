namespace Anabiosis.Shared.Protocol;

// What the galactic map needs to show the war (World.War.cs): the lanes between systems (they never change), what stands in
// every system, the ships travelling the lanes, and the war clock. Systems are counted in the order of the snapshot list of
// star systems. These ride the snapshot as separate properties so each is sent only when it changes.

public sealed record WarLaneState(short A, short B);

// Owner: 0 free, 1 Colonial Union, 2 virus. Planet: 0 none, 1 colony, 2 capital, 3 the virus nest. Yard: 0 none, 1 yard, 2 megayard.
// Project: 0 none, 1 colony, 2 yard, 3 megayard under construction. Fleet: warship supply points standing here.
// Fort: how much of its fortifications are left, in percent. Battle: a fight is going on in the system. Occupation: how far
// an enemy holding it with nothing left to oppose has got toward taking it, in percent.
// Richness: how much ore the asteroid field holds (1 is ordinary); Planets: how many planets circle the star; Rocky: one of them is rocky.
public sealed record WarNodeState(byte Owner, byte Planet, byte Yard, bool Outpost, byte Project, int Stock, short Miners, short Drones, short Fleet,
    float Richness, byte Planets, bool Rocky,
    byte Fort, bool Battle, byte Occupation);

// Class: the ShipClass number. A ship standing in a system has To equal to From and Fraction 0; one on a lane is Fraction
// of the way from From to To. Speed is how many map units it covers per second of war time, so the map can carry it forward
// between updates.
public sealed record WarMoverState(int Id, byte Class, byte Owner, short From, short To, float Fraction, float LaneLength, float Speed);

// SpeedLevel: how many seconds of war pass per real second (0 = paused). The rest is the Union at a glance.
public sealed record WarClockState(double Hours, int SpeedLevel, int UnionSystems, int UnionColonies, int UnionYards,
    int UnionFleetSupply, int UnionFreighters, double OrePool, int VirusSystems,
    int EnemyFleets = 0, int Battles = 0, int Operations = 0);

// A fleet of warships (not the freighters, which are movers): where it is, how big, and what it is doing. Task: 0 standing,
// 1 on the move, 2 withdrawing, 3 striking an enemy, 4 an enemy fleet marching on a system. Supply is its size in supply points.
public sealed record WarFleetState(int Id, byte Owner, short From, short To, float Fraction, float LaneLength, float Speed,
    int Supply, byte Task, bool Fighting);

// The mother ship: where it is (From, or on the lane From to To, Fraction of the way), how much of its armour is left in percent,
// whether it is alive and whether it is fighting. Null in the snapshot when there is none.
public sealed record WarMotherState(short From, short To, float Fraction, float LaneLength, float Speed, byte Hp, bool Alive, bool Fighting);
