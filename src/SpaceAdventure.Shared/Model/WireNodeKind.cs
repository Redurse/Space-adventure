namespace SpaceAdventure.Shared.Model;

// A point in the wiring schematic (game_design.md section 1, M14). Distribution is the single
// source; each PowerSystemId gets one Junction (the "локальная коробка электропередачи"); each
// physical ShipSystemDevice gets one Device node (its "провод-отвод").
public enum WireNodeKind
{
    Distribution,
    Junction,
    Device,
}
