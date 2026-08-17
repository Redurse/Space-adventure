using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Shared.Model;

// Replaces WireLink. NOTE: SpaceAdventure.Shared.Networking also has its own Wire.cs (the TCP frame
// format) - different namespace, no compile conflict, but a file-name search will hit both.
//
// Only the two endpoints are stored, no physical path - the player's walked route while laying one
// (M20) is cosmetic, not simulation state; repair/damage/connectivity only ever need the two
// endpoint components' identities, and a straight pin-to-pin record is strictly simpler to
// store/replicate than a variable-length route with no gameplay payoff. This is a smaller departure
// from the old model than it looks: WireNode positions were already an abstract schematic, not
// physical space, so "no stored path" isn't new information being thrown away.
//
// Pin cardinality (enforced where a Wire gets created, not here): an output pin fans out to any
// number of wires; a Power input accepts up to 2 (this <em>is</em> the old backup mechanic,
// generalized - "at least one of up to two wires is intact" reproduces HasBackup for every power
// input for free); a Signal input accepts exactly 1 (more than one needs a combine rule - last
// write? OR? AND? - this project deliberately avoids inventing one).
public sealed record Wire(string Id, PinRef FromPin, PinRef ToPin);
