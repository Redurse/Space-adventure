using System;
using System.Collections.Generic;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// Where the crew station's own outward wall gets replaced with structural glass, in world units -
// shared by ShipRenderer (which paints it, converted to pixels) and Game1's own sight-gap list
// (Occluders.SightGap), so the drawn glass and the gap that lets the player actually see through it
// can never drift apart. "Outward" reuses the exact same room-center-minus-hull-center direction
// HullSkin's DrawViewports already keys its small round ports off.
public static class CockpitWindows
{
    public const float ThicknessUnits = ShipRenderer.WallThickness / ShipRenderer.PixelsPerUnit;
    private const float MarginUnits = 30f / ShipRenderer.PixelsPerUnit;

    public readonly record struct Pane(float Left, float Top, float Right, float Bottom, bool HorizontalBand);

    public static IEnumerable<Pane> Panes(IReadOnlyList<Room> rooms)
    {
        if (rooms.Count == 0)
            yield break;
        var hullCenter = ShipLocalFrame.GetHullCenter(rooms);
        foreach (var room in rooms)
        {
            if (!room.Id.Contains("cockpit") && !room.Id.Contains("bridge"))
                continue;
            if (TryGetPane(room, hullCenter, out var pane))
                yield return pane;
        }
    }

    public static bool TryGetPane(Room room, Vec2 hullCenter, out Pane pane)
    {
        var centerX = (room.Left + room.Right) / 2f;
        var centerY = (room.Top + room.Bottom) / 2f;
        var outwardX = centerX - hullCenter.X;
        var outwardY = centerY - hullCenter.Y;
        if (outwardX * outwardX + outwardY * outwardY < 0.01f)
        {
            pane = default;
            return false;
        }

        float left, top, right, bottom;
        bool horizontal;
        var half = ThicknessUnits / 2f;
        if (MathF.Abs(outwardX) >= MathF.Abs(outwardY))
        {
            horizontal = false;
            var x = outwardX > 0 ? room.Right - half : room.Left - half;
            left = x;
            right = x + ThicknessUnits;
            top = room.Top + MarginUnits;
            bottom = room.Bottom - MarginUnits;
        }
        else
        {
            horizontal = true;
            var y = outwardY > 0 ? room.Bottom - half : room.Top - half;
            top = y;
            bottom = y + ThicknessUnits;
            left = room.Left + MarginUnits;
            right = room.Right - MarginUnits;
        }

        if (right - left <= 0f || bottom - top <= 0f)
        {
            pane = default;
            return false;
        }

        pane = new Pane(left, top, right, bottom, horizontal);
        return true;
    }
}
