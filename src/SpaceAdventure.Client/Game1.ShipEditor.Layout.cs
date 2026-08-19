using Microsoft.Xna.Framework;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client;

// Rect layout for the Ship Editor's sidebar/bottom bar, shared between the click handler
// (Game1.ShipEditor.cs) and the renderer (Game1.ShipEditor.Draw.cs) so a button always catches
// exactly the click it looks like it should - same convention as StationPanel's GetGoodRect etc.
public partial class Game1
{
    private const int EditorSidebarX = 800;

    private static Rectangle GetEditorToolButtonRect(int index) => new(EditorSidebarX, 64 + index * 32, 170, 26);

    private static Rectangle GetEditorPaletteRect(int index)
    {
        var column = index / 8;
        var row = index % 8;
        return new Rectangle(EditorSidebarX + column * 180, 200 + row * 26, 170, 22);
    }

    private static Rectangle GetEditorMountSideRect(int index) => new(EditorSidebarX + index * 90, 400, 84, 22);

    private static Rectangle GetEditorForwardArrowRect(int index) => new(340 + index * 36, 500, 32, 24);

    private static Rectangle GetEditorActionRect(EditorAction action) => action switch
    {
        EditorAction.Back => new Rectangle(20, 530, 90, 26),
        EditorAction.Clear => new Rectangle(120, 530, 90, 26),
        EditorAction.Save => new Rectangle(220, 530, 90, 26),
        EditorAction.Play => new Rectangle(320, 530, 160, 26),
        _ => Rectangle.Empty,
    };

    private bool HandleShipEditorSidebarClick(bool leftClicked)
    {
        if (!leftClicked)
            return false;
        var point = _designMouse;

        for (var i = 0; i < 3; i++)
        {
            if (!GetEditorToolButtonRect(i).Contains(point))
                continue;
            _editorTool = (EditorTool)i;
            return true;
        }

        if (_editorTool == EditorTool.Device)
        {
            for (var i = 0; i < CustomDeviceCatalog.All.Length; i++)
            {
                if (!GetEditorPaletteRect(i).Contains(point))
                    continue;
                _editorDeviceKind = CustomDeviceCatalog.All[i];
                return true;
            }

            if (_editorDeviceKind is CustomDeviceKind.TurretBallistic or CustomDeviceKind.TurretLaser)
            {
                for (var i = 0; i < EditorMountSides.Length; i++)
                {
                    if (!GetEditorMountSideRect(i).Contains(point))
                        continue;
                    _editorTurretMountSide = EditorMountSides[i];
                    return true;
                }
            }
        }

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
        if (GetEditorActionRect(EditorAction.Clear).Contains(point))
        {
            HandleShipEditorClearClicked();
            return true;
        }
        if (GetEditorActionRect(EditorAction.Save).Contains(point))
        {
            SaveEditorDefinition();
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
