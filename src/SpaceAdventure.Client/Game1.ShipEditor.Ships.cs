using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SpaceAdventure.Server; // SaveStore only, same reason Game1.ShipEditor.cs already imports it
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client;

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

    private void HandleEditorLoadListInput(bool leftClicked)
    {
        if (!leftClicked)
            return;
        var names = CustomShipStore.ListShips();
        for (var i = 0; i < names.Count; i++)
        {
            if (GetEditorLoadRowDeleteRect(i).Contains(_designMouse))
            {
                HandleEditorLoadRowDelete(names[i]);
                return;
            }
            if (GetEditorLoadRowRect(i).Contains(_designMouse))
            {
                HandleEditorLoadRowClick(names[i]);
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
