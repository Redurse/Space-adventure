namespace SpaceAdventure.Shared.Model;

// What kind of hull a raider in a hostile sector actually is. Boarding used to face the same three
// rooms and the same three defenders every single time, which is the state stations were in before
// they got StationKind - one layout standing in for a whole category of thing.
//
// The classes differ in more than their floor plan: how many defenders, how well armed, and - since
// atmosphere now matters aboard them (World.Atmosphere.cs) - whether the crew fights in suits, which
// decides whether venting the ship is a way to take it or a waste of time.
public enum EnemyShipClass
{
    Raider, // three rooms, three unsuited crew: the sector's ordinary opposition
    Freighter, // big, lightly held, nobody suited - the one you can take by opening its doors
    Gunship, // small and mean, crew in suits, so it has to be cleared the hard way
    Frigate, // Corvette-sized warship, suited crew throughout, fields 2 magnetic turrets + 1 laser
}
