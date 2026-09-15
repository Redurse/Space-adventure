using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // humble-soaring-cat.md "Дверь как устройство со своим footprint'ом" - Door.IsVertical/
    // FootprintRect are pure geometry (no MonoGame involved), so covered directly here rather than
    // only through rendering, which this test project can't exercise.

    private static bool Door_IsVertical_UsesExplicitFieldWhenGiven()
    {
        var narrowVertical = new Door("d1", "a", "b", 5f, 3f, 1f, 1f, Vertical: true);
        var narrowHorizontal = new Door("d2", "a", "b", 5f, 3f, 1f, 1f, Vertical: false);
        return narrowVertical.IsVertical && !narrowHorizontal.IsVertical;
    }

    private static bool Door_IsVertical_FallsBackToWidthHeightWhenOmitted()
    {
        var vertical = new Door("d1", "a", "b", 5f, 3f, 1f, 2f); // Width < Height - always the wide-door shape
        var horizontal = new Door("d2", "a", "b", 5f, 3f, 2f, 1f);
        return vertical.IsVertical && !horizontal.IsVertical;
    }

    private static bool Door_FootprintRect_NarrowDoorIsOneByTwoTiles()
    {
        var vertical = new Door("d1", "a", "b", 5f, 3f, 1f, 1f, Vertical: true);
        var (left, top, width, height) = vertical.FootprintRect();
        if (width != 2f || height != 1f || left != 4f || top != 2.5f)
            return false;

        var horizontal = new Door("d2", "a", "b", 5f, 3f, 1f, 1f, Vertical: false);
        var (hLeft, hTop, hWidth, hHeight) = horizontal.FootprintRect();
        return hWidth == 1f && hHeight == 2f && hLeft == 4.5f && hTop == 2f;
    }

    private static bool Door_FootprintRect_WideDoorIsTwoByTwoTiles()
    {
        var door = new Door("d1", "a", "b", 5f, 3f, 1f, Door.StandardSpanUnits, Vertical: true);
        var (left, top, width, height) = door.FootprintRect();
        return width == 2f && height == 2f && left == 4f && top == 2f;
    }
}
