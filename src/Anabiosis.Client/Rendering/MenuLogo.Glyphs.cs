namespace Anabiosis.Client.Rendering;

// The six letters of ANABIOSIS, written as rectangles and quads in a 140-unit cell with the origin
// at the top left. Solids are filled, holes are cleared afterwards, so a counter is just a shape
// stamped second rather than a hand-traced outline.
//
// The style is one decision repeated: flat terminals, square corners, counters cut as slots rather
// than rounded. Only A and N lean, and only where a squared letter would stop being that letter.
public static partial class MenuLogo
{
    private static float[] Rect(float x, float y, float w, float h) =>
        new[] { x, y, x + w, y, x + w, y + h, x, y + h };

    private static float Advance(char ch) => GlyphOf(ch).Advance;

    private static (float Advance, float[][] Solids, float[][] Holes) GlyphOf(char ch) => ch switch
    {
        // The one splayed glyph. Legs run from a flat 34-unit apex; the counter above the crossbar is
        // a wedge and the gap below it a wider one.
        'A' => (104f,
                new[] { new[] { 0f, CellHeight, 34f, 0f, 70f, 0f, 104f, CellHeight } },
                new[]
                {
                    new[] { 37f, 84f, 48f, 32f, 56f, 32f, 67f, 84f },
                    new[] { 28f, CellHeight, 34f, 112f, 70f, 112f, 76f, CellHeight },
                }),

        // Two posts and a diagonal heavy enough to match them. A thin diagonal here would make the
        // letter read as two separate bars with a scratch between them.
        'N' => (104f,
                new[]
                {
                    Rect(0, 0, Stroke, CellHeight),
                    Rect(104 - Stroke, 0, Stroke, CellHeight),
                    new[] { Stroke, 0f, Stroke + 26f, 0f, 104f - Stroke, CellHeight, 104f - Stroke - 26f, CellHeight },
                },
                new float[0][]),

        // Two counters cut out of one slab, the lower a shade taller than the upper - which is what
        // stops a squared B from reading as an 8.
        'B' => (96f,
                new[] { Rect(0, 0, 96, CellHeight) },
                new[] { Rect(Stroke, 26, 42, 30), Rect(Stroke, 84, 42, 30) }),

        // A bare stem. No slab serifs: they would be the only serifs in the word.
        'I' => (46f,
                new[] { Rect(9, 0, Stroke, CellHeight) },
                new float[0][]),

        'O' => (104f,
                new[] { Rect(0, 0, 104, CellHeight) },
                new[] { Rect(Stroke, Stroke, 104 - Stroke * 2, CellHeight - Stroke * 2) }),

        // Five bars. Drawn as a stack rather than as a curve, which is what keeps its weight equal to
        // the letters either side of it.
        'S' => (96f,
                new[]
                {
                    Rect(0, 0, 96, Stroke),
                    Rect(0, Stroke, Stroke, Stroke),
                    Rect(0, 56, 96, Stroke),
                    Rect(96 - Stroke, 84, Stroke, Stroke),
                    Rect(0, 112, 96, Stroke),
                },
                new float[0][]),

        _ => (0f, new float[0][], new float[0][]),
    };
}
