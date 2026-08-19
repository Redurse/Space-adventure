using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Shared.Model;

// Replaces WireLink. NOTE: SpaceAdventure.Shared.Networking also has its own Wire.cs (the TCP frame
// format) - different namespace, no compile conflict, but a file-name search will hit both.
//
// Only the two endpoints carry any gameplay meaning - repair/damage/connectivity only ever need
// the two endpoint components' identities, never the path between them.
//
// Pin cardinality (enforced where a Wire gets created, not here): an output pin fans out to any
// number of wires; a Power input accepts up to 2 (this <em>is</em> the old backup mechanic,
// generalized - "at least one of up to two wires is intact" reproduces HasBackup for every power
// input for free); a Signal input accepts exactly 1 (more than one needs a combine rule - last
// write? OR? AND? - this project deliberately avoids inventing one).
//
// Bends is purely cosmetic routing (the LMB-fixed points a player laid it through,
// World.Wiring.cs's HandleWireBend/HandleWireLayCancel) - null/empty draws the same straight
// pin-to-pin line as before; never read by anything connectivity-related.
public sealed record Wire(string Id, PinRef FromPin, PinRef ToPin, IReadOnlyList<Vec2>? Bends = null);
