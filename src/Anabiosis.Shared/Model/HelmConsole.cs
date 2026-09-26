namespace Anabiosis.Shared.Model;

// The pilot's console (game_design.md Phase 3 — open space movement): stand here to take manual
// control of the ship. Brings up a Barotrauma-style joystick schematic instead of the ship view.
//
// An ordinary rotatable floor-standing device (same shape as Bed/StorageRack) - Rotated swaps its
// own drawn box between 1.5x2 and 2x1.5 tile proportions (ShipRenderer.Devices.cs's own
// DrawHelmConsole), while CustomDeviceFootprint.Size(Helm) always reserves a full 2x2 tiles for
// collision (no fractional-tile concept exists in the tile-grid model). A prior session briefly
// replaced this with a wall-mounted-panel mechanic (FacingSide) - rejected twice by the user and
// fully rolled back; every hand-authored default hull and every custom ship ever saved keeps
// working unchanged since this record's own shape never actually changed underneath them.
// HalfSide (direct user bug report, "не поворачиваются на все 4 стороны") - which side of the
// console's own 2x2 anchor box its half tile sits on; Rotated alone only ever distinguished
// East/South (the ONLY two the tile editor used to be able to place), so it stays for the
// width/height swap (FootprintPixelSize) while HalfSide separately carries the full 4-way choice.
// Always resolved (CustomDeviceFootprint.ResolveHalfSide) before reaching this record - never left
// to disagree with Rotated.
public sealed record HelmConsole(string Id, string RoomId, float X, float Y, bool Rotated = false, TileSide HalfSide = TileSide.East)
{
    public Vec2 Position => new(X, Y);
}
