namespace Anabiosis.Shared.Model;

// The systems on the shared energy grid a player can allocate reactor output to
// (game_design.md section 1). Secondary bundles airlocks/lights/scanner under one slider.
public enum PowerSystemId
{
    Oxygen,
    Engine,
    Shields,
    WeaponCharger,
    Secondary,
}
