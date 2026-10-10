using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client;

// The Ship Editor's half of junction doors (Anabiosis.Shared/Model/JunctionDoor.cs): a single, double or
// triple door that cuts through a half-block wall - the junction of two compartments, or a compartment's
// wall against open space - instead of standing on bare floor. The Door tool hands a click on a wall
// tile to this file; a click on anything else keeps working exactly as before.
public partial class Game1
{
    // The wall tiles each junction door cleared, keyed by the id its door edges share, so deleting
    // the door can put the walls back (and so a saved/reloaded ship still can).
    private readonly Dictionary<string, List<SavedJunctionWall>> _editorJunctionWalls = new();

    // R in the Door tool: which way a character walks through the door.
    private TileSide EditorDoorPassageSide => _editorDoorPendingVertical ? TileSide.South : TileSide.East;

    // True when the click was on a wall tile and so was this file's to answer (placed, or refused with
    // a toast); false when it was on anything else, leaving the ordinary floor door to handle it.
    private bool TryPlaceJunctionDoor(TileCoord anchor, int span)
    {
        if (_editorTiles.CellAt(anchor) is not { Wall: TileWallKind.Solid })
            return false;

        var plan = JunctionDoor.Plan(_editorTiles, anchor, EditorDoorPassageSide, span, out var failure);
        if (plan is null)
        {
            ShowEditorToast(JunctionFailureText(failure));
            return true;
        }

        var id = $"door-edge-{_editorNextDoorEdgeId++}";
        if (JunctionDoor.Apply(_editorTiles, plan, id) is { } removedWalls)
            _editorJunctionWalls[id] = removedWalls.ToList();
        else
            ShowEditorToast("Здесь не получается поставить дверь - проход должен быть свободен.");
        return true;
    }

    // Deleting a door that was cut through walls puts those walls back. Called with the id of the
    // door group that has just been removed.
    private void RestoreJunctionWalls(string? doorId)
    {
        if (doorId is not null && _editorJunctionWalls.Remove(doorId, out var walls))
            JunctionDoor.Restore(_editorTiles, walls);
    }

    private void ShowEditorToast(string message)
    {
        _editorToastMessage = message;
        _editorToastUntilTicks = Environment.TickCount64 + EditorToastMilliseconds;
    }

    private static string JunctionFailureText(JunctionDoorFailure failure) => failure switch
    {
        JunctionDoorFailure.NotHalfBlock => "Дверь в стене можно поставить только на полублочные стены.",
        JunctionDoorFailure.TooThick => "Стена здесь слишком толстая, или дверь повёрнута вдоль стены - нажмите R, чтобы повернуть её.",
        JunctionDoorFailure.BadBoundary => "С обеих сторон стены должен быть пол (с одной - может быть открытый космос, это будет шлюз).",
        JunctionDoorFailure.NotEveryTileHalfBlock => "Вся ширина двери должна приходиться на полублочные стены.",
        JunctionDoorFailure.NoSideWall => "По бокам двери должна продолжаться стена - дверь не может стоять сама по себе.",
        JunctionDoorFailure.WallDevice => "На этой стене стоит устройство (терминал, лампа) - сначала уберите его.",
        _ => "Здесь нельзя поставить дверь.",
    };

    // Green preview of what the click would do: the wall tiles that become the opening and the door
    // across it. False when the hovered tile isn't a placeable junction (the caller then falls back to
    // the ordinary preview, which shows red over a wall).
    private bool DrawEditorJunctionDoorPreview(TileCoord anchor, int span)
    {
        if (_editorTiles.CellAt(anchor) is not { Wall: TileWallKind.Solid })
            return false;
        var plan = JunctionDoor.Plan(_editorTiles, anchor, EditorDoorPassageSide, span, out _);
        if (plan is null)
            return false;

        foreach (var tile in plan.WallTiles)
        {
            var rect = EditorTileRect(tile);
            _spriteBatch.Draw(_pixel, rect, new Color(90, 160, 110) * 0.35f);
            DrawRectOutline(rect, Color.LightGreen, 2f);
        }

        var thickness = Math.Max(4, (int)(0.22f * EditorCellSize));
        foreach (var (coord, side) in plan.Edges)
        {
            // The door's bar sits on the edge between `coord` and its neighbour on `side`.
            var rect = EditorTileRect(coord);
            var bar = side == TileSide.East
                ? new Rectangle(rect.Right - thickness / 2, rect.Y, thickness, rect.Height)
                : new Rectangle(rect.X, rect.Bottom - thickness / 2, rect.Width, thickness);
            _spriteBatch.Draw(_pixel, bar, new Color(90, 230, 120) * 0.8f);
        }
        return true;
    }
}
