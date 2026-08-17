namespace SpaceAdventure.Shared.Model;

// Replaces WireNodeKind. Distribution/Junction/Device are the ship's built-in, non-removable power
// backbone (WireGraphFactory) - the same three roles WireNode.Kind used to distinguish. Everything
// else is a purchasable signal-logic part installed at a ComponentMount (M23) - one value per
// distinct part, the same "one enum value per thing" convention ItemType already uses, so each maps
// 1:1 onto its own ItemType.
public enum ComponentKind
{
    Distribution,
    Junction,
    Device,

    GateAnd,
    GateOr,
    GateNot,
    GateXor,
    Timer,
    Memory,
    Relay,

    OxygenSensor,
    BreachSensor,
    PowerLossSensor,
    MotionSensor,

    AutoDoorController,
    AlarmKlaxon,
    LightToggle,
}
