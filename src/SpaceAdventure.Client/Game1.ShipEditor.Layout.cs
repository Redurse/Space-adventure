using Microsoft.Xna.Framework;

namespace SpaceAdventure.Client;

// Rect layout for the Ship Editor's sidebar/bottom bar, shared between the click handler
// (Game1.ShipEditor.cs) and the renderer (Game1.ShipEditor.Draw.cs) so a button always catches
// exactly the click it looks like it should - same convention as StationPanel's GetGoodRect etc.
public partial class Game1
{
    // Moved up near the title (direct user request - see ShipEditorCanvas's own doc comment) so the
    // device-tab panel can own the true bottom of the screen instead.
    private static Rectangle GetEditorForwardArrowRect(int index) => new(340 + index * 36, 32, 32, 24);

    private static Rectangle GetEditorActionRect(EditorAction action) => action switch
    {
        EditorAction.Back => new Rectangle(20, 60, 80, 26),
        EditorAction.New => new Rectangle(105, 60, 80, 26),
        EditorAction.Save => new Rectangle(190, 60, 80, 26),
        EditorAction.SaveAs => new Rectangle(275, 60, 110, 26),
        EditorAction.Load => new Rectangle(390, 60, 80, 26),
        EditorAction.Play => new Rectangle(475, 60, 140, 26),
        _ => Rectangle.Empty,
    };

    // Редактор корабля в духе Cosmoteer + несколько сохранённых кораблей (humble-soaring-cat.md,
    // Step 6) - "Сохранить как"'s own small modal, centred over the canvas.
    private static Rectangle GetEditorSaveAsBoxRect() => new(220, 200, 340, 120);
    private static Rectangle GetEditorSaveAsInputRect() => new(240, 240, 300, 26);
    private static Rectangle GetEditorSaveAsConfirmRect() => new(240, 278, 140, 28);
    private static Rectangle GetEditorSaveAsCancelRect() => new(400, 278, 140, 28);

    // "Загрузить"'s own list modal - one row per saved slot, a small delete box beside each.
    private static Rectangle GetEditorLoadBoxRect() => new(200, 90, 380, 400);
    private static Rectangle GetEditorLoadRowRect(int index) => new(216, 120 + index * 30, 300, 26);
    private static Rectangle GetEditorLoadRowDeleteRect(int index) => new(524, 120 + index * 30, 40, 26);
    private static Rectangle GetEditorLoadCloseRect() => new(216, 460, 100, 26);

    // Tile-painting redo - the Zone tool's own naming prompt, same small-modal convention as
    // "Сохранить как" above.
    private static Rectangle GetEditorZoneNameBoxRect() => new(220, 200, 340, 120);
    private static Rectangle GetEditorZoneNameInputRect() => new(240, 240, 300, 26);
    private static Rectangle GetEditorZoneNameConfirmRect() => new(240, 278, 140, 28);
    private static Rectangle GetEditorZoneNameCancelRect() => new(400, 278, 140, 28);

    private bool HandleShipEditorSidebarClick(bool leftClicked)
    {
        if (!leftClicked)
            return false;
        var point = _designMouse;

        if (HandleDeviceTabClick(point))
            return true;

        for (var i = 0; i < EditorForwardOptions.Length; i++)
        {
            if (!GetEditorForwardArrowRect(i).Contains(point))
                continue;
            _editorForwardDegrees = EditorForwardOptions[i];
            SaveEditorDefinition();
            return true;
        }

        if (GetEditorActionRect(EditorAction.Back).Contains(point))
        {
            _menuScreen = MenuScreen.Main;
            return true;
        }
        if (GetEditorActionRect(EditorAction.New).Contains(point))
        {
            HandleShipEditorNewClicked();
            return true;
        }
        if (GetEditorActionRect(EditorAction.Save).Contains(point))
        {
            HandleShipEditorSaveClicked();
            return true;
        }
        if (GetEditorActionRect(EditorAction.SaveAs).Contains(point))
        {
            OpenEditorSaveAsPrompt();
            return true;
        }
        if (GetEditorActionRect(EditorAction.Load).Contains(point))
        {
            _editorLoadListOpen = true;
            return true;
        }
        if (GetEditorActionRect(EditorAction.Play).Contains(point))
        {
            HandleShipEditorPlayClicked();
            return true;
        }

        return false;
    }
}
