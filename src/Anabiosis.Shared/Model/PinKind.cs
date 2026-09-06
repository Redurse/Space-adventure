namespace Anabiosis.Shared.Model;

// Power pins carry a share of PowerGrid's allocation; Signal pins carry a plain boolean. The two
// never connect to each other (TankSockets is the precedent for "a socket only accepts its own
// matching kind") - a sensor never needs a Power pin at all, it reads world state directly.
public enum PinKind { PowerIn, PowerOut, SignalIn, SignalOut }
