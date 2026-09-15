using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Anabiosis.Server; // SaveStore only, same reason Game1.ShipEditor.cs already imports it
using Anabiosis.Shared.Model;

namespace Anabiosis.Client;

// Редактор корабля в духе Cosmoteer + несколько сохранённых кораблей (humble-soaring-cat.md, Step
// 6) - New/Save/Save As/Load over CustomShipStore's own per-name slots (Step 5). The single
// "currently open, not yet saved under a name" scratch slot (CustomShipStore.Load/Save, no name
// argument) keeps auto-saving on every edit exactly as before this feature existed - _editorCurrentSlotName
// is a SEPARATE concept, the name of whichever saved slot (if any) the open design also happens to
// be saved under, so a plain "Сохранить" click knows where to write without asking again.
public partial class Game1
{
    private string? _editorCurrentSlotName;
    private bool _editorSaveAsPrompting;
    private string _editorSaveAsInput = "";
    private bool _editorLoadListOpen;
    // Direct user request ("сделай возможность листать сохраненные чертежи в редакторе") - which
    // page of EditorLoadRowsPerPage names is currently shown; reset to 0 wherever the list is opened
    // (Game1.ShipEditor.Layout.cs's own click handler) so it never reopens mid-list from last time.
    private int _editorLoadListPage;

    // "Сохранить" with no known slot behaves like "Сохранить как" (prompts for a name) - the least
    // surprising default for a design that's never been saved under a name yet.
    private void HandleShipEditorSaveClicked()
    {
        if (_editorCurrentSlotName is null)
        {
            OpenEditorSaveAsPrompt();
            return;
        }
        CustomShipStore.SaveShip(_editorCurrentSlotName, BuildEditorDefinition());
        CustomShipStore.SaveShipTileCanvas(_editorCurrentSlotName, BuildEditorTileCanvas());
    }

    private void OpenEditorSaveAsPrompt()
    {
        _editorSaveAsInput = _editorCurrentSlotName ?? _editorShipName;
        _editorSaveAsPrompting = true;
    }

    private void HandleEditorSaveAsPromptInput(KeyboardState keyboard, bool leftClicked)
    {
        if (Pressed(keyboard, Keys.Enter))
        {
            ConfirmEditorSaveAs();
            return;
        }
        if (!leftClicked)
            return;
        if (GetEditorSaveAsConfirmRect().Contains(_designMouse))
            ConfirmEditorSaveAs();
        else if (GetEditorSaveAsCancelRect().Contains(_designMouse))
            _editorSaveAsPrompting = false;
    }

    private void ConfirmEditorSaveAs()
    {
        var slotName = _editorSaveAsInput.Trim();
        if (slotName.Length == 0)
            return; // no sane default to fall back to here (unlike Nickname's "Игрок") - stay open
        CustomShipStore.SaveShip(slotName, BuildEditorDefinition());
        CustomShipStore.SaveShipTileCanvas(slotName, BuildEditorTileCanvas());
        _editorCurrentSlotName = slotName;
        _editorSaveAsPrompting = false;
    }

    // Direct user request ("сделай возможность листать сохраненные чертежи в редакторе") - Left/Right
    // page the same way the Prev/Next buttons do (Pressed's own edge-detection, same helper "Сохранить
    // как"'s Enter shortcut already uses), so a long session of flipping through many saved designs
    // doesn't have to keep re-aiming the mouse at a small button. `page` is clamped every call (not
    // just when the list is (re)opened) so deleting the last name on the final page never strands it
    // one page past the new end.
    private void HandleEditorLoadListInput(bool leftClicked, KeyboardState keyboard)
    {
        var names = CustomShipStore.ListShips();
        var pageCount = Math.Max(1, (names.Count + EditorLoadRowsPerPage - 1) / EditorLoadRowsPerPage);
        _editorLoadListPage = Math.Clamp(_editorLoadListPage, 0, pageCount - 1);

        if (Pressed(keyboard, Keys.Left) && _editorLoadListPage > 0)
            _editorLoadListPage--;
        if (Pressed(keyboard, Keys.Right) && _editorLoadListPage < pageCount - 1)
            _editorLoadListPage++;

        if (!leftClicked)
            return;

        if (GetEditorLoadPrevPageRect().Contains(_designMouse))
        {
            if (_editorLoadListPage > 0)
                _editorLoadListPage--;
            return;
        }
        if (GetEditorLoadNextPageRect().Contains(_designMouse))
        {
            if (_editorLoadListPage < pageCount - 1)
                _editorLoadListPage++;
            return;
        }

        var firstIndex = _editorLoadListPage * EditorLoadRowsPerPage;
        var pageNames = names.Skip(firstIndex).Take(EditorLoadRowsPerPage).ToList();
        for (var i = 0; i < pageNames.Count; i++)
        {
            if (GetEditorLoadRowDeleteRect(i).Contains(_designMouse))
            {
                HandleEditorLoadRowDelete(pageNames[i]);
                return;
            }
            if (GetEditorLoadRowRect(i).Contains(_designMouse))
            {
                HandleEditorLoadRowClick(pageNames[i]);
                return;
            }
        }
        if (GetEditorLoadCloseRect().Contains(_designMouse))
            _editorLoadListOpen = false;
    }

    private void HandleEditorLoadRowClick(string slotName)
    {
        if (CustomShipStore.LoadShip(slotName) is not { } loaded)
            return;
        _editorRooms = loaded.Rooms.ToList();
        _editorDoors = loaded.Doors.ToList();
        _editorAirlocks = loaded.Airlocks.ToList();
        _editorDevices = loaded.Devices.ToList();
        _editorShipName = loaded.Name;
        _editorForwardDegrees = loaded.ForwardDegrees;
        _editorRoomCounter = NextRoomCounter(_editorRooms);
        // The real content - restores the actual drawing if this slot has one saved; an older slot
        // saved before this feature existed falls back to a blank canvas, same as EnterShipEditor's
        // own fallback for the scratch slot.
        if (CustomShipStore.LoadShipTileCanvas(slotName) is { } savedCanvas)
            ApplyEditorTileCanvas(savedCanvas);
        else
        {
            _editorTiles = new TileGrid();
            _editorDeviceKinds.Clear();
            _editorDeviceFootprint.Clear();
            _editorZones.Clear();
            _editorEngineFacing.Clear();
            _editorEngineFootprint.Clear();
        }
        _editorCurrentSlotName = slotName;
        _editorLoadListOpen = false;
        SaveEditorDefinition(); // keeps the scratch slot in sync with whatever's now open, as always
    }

    private void HandleEditorLoadRowDelete(string slotName)
    {
        CustomShipStore.DeleteShip(slotName);
        if (_editorCurrentSlotName == slotName)
            _editorCurrentSlotName = null; // the open ship's own save slot just vanished
    }
}
