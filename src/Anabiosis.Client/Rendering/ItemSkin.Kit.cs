using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// The kit the crew carries besides the tools, redrawn with them after Barotrauma's toolkit sheet
// (direct user request): a rugged olive radio, gas cylinders with a sight window onto what is inside,
// a black utility belt with pouches. Upright, never rotated; supersampled and grimed like the tools.
public static partial class ItemSkin
{
    private static readonly Mat OliveDrab = new(new Color(84, 94, 62), 0.2f, 0.08f);
    private static readonly Mat Webbing = new(new Color(46, 46, 46), 0.1f, 0.12f);
    private static readonly Mat CylinderSteel = new(new Color(128, 130, 134), 0.45f, 0.07f);

    // A field radio: olive body with black rubber corner bumpers, a green screen, a speaker grille, a
    // channel knob, a stubby rubber antenna and a push-to-talk bar on the side.
    private static void Radio()
    {
        Rod(19.6f, 8.6f, 19.6f, 1.6f, 1f, GripBlack);                     // antenna
        Ball(19.6f, 1.8f, 0.9f, GripBlack);
        Box(9f, 7.6f, 14f, 22f, OliveDrab, 2.6f);
        foreach (var (cx, cy) in new[] { (10.2f, 8.8f), (21.8f, 8.8f), (10.2f, 28.4f), (21.8f, 28.4f) })
            Ball(cx, cy, 1.5f, GripBlack);                                 // corner bumpers
        Box(11.4f, 10f, 9.2f, 6.2f, Paint(28, 32, 28, 0.4f), 1f);         // screen bezel
        Box(12.2f, 10.8f, 7.6f, 4.6f, Paint(92, 168, 96, 0.5f, 0f), 0.4f); // screen
        Line(12.8f, 12f, 17.4f, 12f, Paint(30, 70, 34).Base, 0.8f);
        Line(12.8f, 13.6f, 15.6f, 13.6f, Paint(30, 70, 34).Base, 0.8f);
        for (var y = 18.4f; y <= 26.2f; y += 1.6f)
            Box(11.8f, y, 8.4f, 0.7f, Paint(30, 32, 30, 0.1f), 0.3f);     // speaker slots
        Ball(12.6f, 6.8f, 1.6f, ToolSteelDark);                           // channel knob
        Box(7.6f, 13.6f, 1.6f, 6f, GripBlack, 0.6f);                      // push-to-talk
        Dot(18.8f, 9.1f, new Color(220, 70, 50), 1f, 0.35f);              // transmit LED
        Grime(41, 0.45f);
    }

    // A breathing-gas cylinder: dull steel, a sight window with the blue gas behind it, a valve and wheel.
    private static void OxygenTank() => GasCylinder(Paint(70, 140, 200, 0.6f, 0.02f), Paint(64, 120, 168, 0.3f));

    // The welder's fuel cylinder: the same, amber fuel and an orange band.
    private static void WeldingTank() => GasCylinder(Paint(214, 150, 54, 0.6f, 0.02f), RustOrange);

    private static void GasCylinder(Mat contents, Mat band)
    {
        Rod(16f, 9.4f, 16f, 27.6f, 5.6f, CylinderSteel);                  // body
        Box(10.4f, 26.6f, 11.2f, 2.6f, ToolSteelDark, 1f);                // foot ring
        Box(10.4f, 11.2f, 11.2f, 2.2f, band, 0.4f);                       // colour band
        Box(13.6f, 14.8f, 4.8f, 10.8f, Paint(22, 26, 30, 0.6f), 1f);      // sight window...
        Box(14.2f, 18.4f, 3.6f, 6.6f, contents, 0.6f);                    // ...two-thirds full
        Line(14.6f, 15.4f, 14.6f, 24.6f, Highlight, 0.35f);               // glass glint
        Box(14.2f, 3.6f, 3.6f, 3.8f, Brass, 0.8f);                        // valve
        Rod(11.8f, 3.2f, 20.2f, 3.2f, 0.9f, ToolSteelDark);              // handwheel bar
        Ball(16f, 3.2f, 1.4f, ToolSteelDark);
        Line(11.4f, 10f, 11.4f, 26f, Highlight, 0.25f);                   // the cylinder's own sheen
        Grime(42, 0.5f);
    }

    // A utility belt: a black webbing strap with a steel buckle and three pouches hanging off it.
    private static void BeltBag()
    {
        Box(0.8f, 9.6f, 30.4f, 3.6f, Webbing, 1f);                        // strap
        Box(13.4f, 8.8f, 5.2f, 5.2f, ToolSteel, 0.8f);                    // buckle
        Box(14.8f, 10.2f, 2.4f, 2.4f, Webbing);
        foreach (var (x, w, h) in new[] { (2.2f, 8.4f, 13.4f), (19.8f, 9.6f, 15.6f), (11.2f, 6.4f, 9f) })
        {
            if (x == 11.2f) continue; // the middle stays clear under the buckle
            Box(x, 12.6f, w, h, Webbing, 1.6f);                           // pouch
            Box(x - 0.2f, 12.4f, w + 0.4f, 4.2f, Paint(36, 36, 36, 0.1f, 0.12f), 1.2f); // flap
            Ball(x + w / 2f, 15.4f, 0.8f, ToolSteelDark);                  // snap
            Line(x + 0.8f, 12.6f + h - 1.2f, x + w - 0.8f, 12.6f + h - 1.2f, Ink, 0.35f); // stitching
        }
        Rod(23.2f, 15.6f, 23.2f, 10.6f, 0.45f, Amber);                    // a screwdriver handle peeking out
        Grime(43, 0.45f);
    }
}
