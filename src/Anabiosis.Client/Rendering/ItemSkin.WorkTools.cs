using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// The crew's work tools, redrawn after Barotrauma's toolkit sheet (direct user request: "переделай по
// аналогии инструменты игрока" - its look, not its pixels): chunky, used, mostly dull steel and black
// rubber, with colour only where a real tool has it - the welder's rusty-orange tank, the plasma
// cutter's blue-green housing, a screwdriver's amber handle. Supersampled 4x (ItemSkin.Materials.cs's
// list) so the edges are smooth at slot size, and grimed after painting.
// Same frame as every tool: lying along +X, centre line y = 16; the welder and the cutter put their
// business end on that line at x = 31 (ItemIcons.GetMuzzleWorldPosition).
public static partial class ItemSkin
{
    private static readonly Mat ToolSteel = new(new Color(150, 152, 154), 0.55f, 0.06f);
    private static readonly Mat ToolSteelDark = new(new Color(96, 98, 102), 0.45f, 0.06f);
    private static readonly Mat GripBlack = new(new Color(40, 40, 42), 0.18f, 0.07f);
    private static readonly Mat Amber = new(new Color(196, 146, 48), 0.25f, 0.07f);
    private static readonly Mat RustOrange = new(new Color(170, 92, 44), 0.25f, 0.1f);
    private static readonly Mat CutterTeal = new(new Color(70, 120, 128), 0.35f, 0.07f);
    private static readonly Mat GaugeFace = new(new Color(206, 202, 188), 0.3f, 0.03f);
    private static readonly Color Highlight = new(232, 228, 216);

    // ------------------------------------------------------------------ screwdriver

    private static void Screwdriver() => Driver(Amber, ToolSteel);

    // Gosha's screwdriver: the same tool with the saboteur's magenta handle and a dark shaft.
    private static void GoshaScrewdriver()
    {
        Driver(Paint(160, 52, 128, 0.25f, 0.07f), ToolSteelDark);
        Dot(7f, 14.8f, new Color(240, 214, 110), 1f, 0.7f); // a gold star sticker
    }

    // A long workshop screwdriver: a fat moulded handle - coloured core, black rubber cap and grip
    // pads - a steel ferrule, a long shaft and a flat tip.
    private static void Driver(Mat handle, Mat shaft)
    {
        Rod(13.4f, 16f, 27.6f, 16f, 0.85f, shaft);
        Line(14f, 15.45f, 27f, 15.45f, Highlight, 0.35f);
        Poly(shaft, 27f, 15.2f, 29.8f, 14.8f, 31f, 15.4f, 31f, 16.6f, 29.8f, 17.2f, 27f, 16.8f); // flattened tip
        Line(29.8f, 15f, 29.8f, 17f, Ink, 0.45f);
        Box(12f, 14.3f, 2f, 3.4f, ToolSteelDark, 0.5f);                  // ferrule
        Taper(2.4f, 16f, 2.9f, 8.6f, 16f, 3.4f, handle);                 // handle swells, then necks in
        Taper(8.6f, 16f, 3.4f, 12.4f, 16f, 2.2f, handle);
        Rod(1.4f, 16f, 2.6f, 16f, 2.7f, GripBlack);                      // end cap
        Box(3.6f, 12.9f, 6.6f, 1.2f, GripBlack, 0.5f);                   // rubber pads top and bottom
        Box(3.6f, 17.9f, 6.6f, 1.2f, GripBlack, 0.5f);
        for (var x = 4.4f; x <= 9.6f; x += 1.3f)
            Line(x, 14.5f, x, 17.5f, Ink, 0.2f);                          // grip flutes
        Line(3f, 14.4f, 9.6f, 14.2f, Highlight, 0.3f);
        Grime(31, 0.35f);
    }

    // ------------------------------------------------------------------ wrench

    // A long adjustable wrench, the way Barotrauma draws it: a slim forged handle with a hang hole, a
    // chunky head, the fixed jaw on top, the sliding jaw below with its worm screw.
    private static void Wrench()
    {
        Taper(2f, 16f, 1.3f, 19.5f, 16f, 1.8f, ToolSteel);               // handle widens toward the head
        Line(4f, 15.3f, 18.5f, 15.1f, Highlight, 0.3f);
        Line(5f, 16.1f, 18f, 16.1f, Ink, 0.25f);                          // forged ridge
        EraseBall(3.2f, 16f, 0.6f);                                       // hang hole
        // head: one rounded forging, a narrow jaw cut into its front - the fixed jaw on top runs a little
        // longer than the sliding one below, which sits on the worm screw
        Ball(24.2f, 16f, 4.4f, ToolSteel);
        Box(19.4f, 13.4f, 5f, 5.2f, ToolSteel, 1.2f);
        Poly(ToolSteel, 25f, 11.8f, 30.6f, 13.4f, 30.6f, 15f, 26f, 15f);              // fixed jaw
        Poly(ToolSteelDark, 26f, 17f, 29.8f, 17f, 29.6f, 18.8f, 25.8f, 19.8f);        // sliding jaw
        EraseBox(26.4f, 15f, 6f, 2f);                                     // the jaw opening
        Line(26.4f, 15f, 26.4f, 17f, Ink, 0.6f);
        Line(25.8f, 17f, 25.6f, 19.6f, Ink, 0.4f);                       // seam of the sliding jaw
        Box(20.8f, 17.4f, 4.2f, 1.8f, ToolSteelDark, 0.6f);              // worm screw
        Knurl(21.2f, 17.6f, 3.4f, 1.4f, 0.45f);
        Line(20.2f, 13.2f, 26.6f, 12.6f, Highlight, 0.4f);
        Grime(32, 0.4f);
    }

    // ------------------------------------------------------------------ welding tool

    // A welding gun: a rusty-orange fuel tank at the back in two steel bands, a gunmetal body with a
    // pressure gauge, a black pistol grip and trigger, and a brass neck ending in a copper nozzle.
    private static void WeldingTool()
    {
        Rod(1.6f, 18f, 0.6f, 23.5f, 0.7f, GripBlack);                    // hose out of the tank
        Rod(3f, 14.6f, 11.4f, 14.6f, 3.4f, RustOrange);                  // tank
        foreach (var bx in new[] { 4.4f, 9.6f })
            Box(bx, 11f, 0.9f, 7.2f, ToolSteelDark, 0.3f);
        Ball(2.4f, 14.6f, 2.2f, ToolSteelDark);                          // tank end cap
        Line(5.4f, 12.4f, 9f, 12.4f, Highlight, 0.3f);
        Poly(GripBlack, 11.6f, 18f, 15.6f, 18f, 14.6f, 27.4f, 10.4f, 27.4f);          // grip
        for (var y = 20f; y <= 26f; y += 1.5f)
            Line(11f, y, 14.8f, y, Ink, 0.3f);
        Poly(GripBlack, 16f, 18f, 17.2f, 18f, 16.6f, 21.6f, 15.4f, 21.2f);            // trigger
        Box(10.2f, 11.4f, 12.4f, 7.4f, Gunmetal, 1.6f);                  // body
        Box(10.6f, 16.4f, 11.6f, 1.6f, RustOrange, 0.5f);                // paint stripe
        Ball(14.4f, 13.8f, 2f, ToolSteelDark);                            // gauge bezel
        Ball(14.4f, 13.8f, 1.45f, GaugeFace, sphere: false);
        Line(14.4f, 13.8f, 15.3f, 13.1f, new Color(150, 40, 30), 1f);    // needle
        Box(18f, 12.2f, 3.6f, 1.4f, ToolSteelDark, 0.4f);                // valve block on top
        Taper(22.4f, 16f, 1.25f, 27.4f, 16f, 0.95f, Brass);              // neck
        Box(22.2f, 14.4f, 1.2f, 3.2f, ToolSteelDark, 0.3f);              // union nut
        Taper(27.2f, 16f, 1.2f, 31f, 16f, 0.75f, Copper);               // nozzle
        Line(27.6f, 15.3f, 30.6f, 15.5f, Highlight, 0.35f);
        Dot(31f, 16f, new Color(30, 22, 18), 1f, 0.35f);                  // orifice
        Grime(33, 0.5f);
    }

    // ------------------------------------------------------------------ plasma cutter

    // A plasma cutter: a blue-green housing with a vented top and a dial, a coil exposed behind a
    // guard, a black grip and trigger guard, and a ribbed nozzle with a hot ring at the mouth.
    private static void Cutter()
    {
        Rod(1.4f, 19f, 0.6f, 24f, 0.7f, GripBlack);                      // power lead
        Poly(GripBlack, 7f, 18.4f, 11.2f, 18.4f, 10.2f, 27.6f, 5.8f, 27.6f);          // grip
        for (var y = 20.4f; y <= 26.4f; y += 1.5f)
            Line(6.4f, y, 10.4f, y, Ink, 0.3f);
        Poly(ToolSteelDark, 11.2f, 18.4f, 16.6f, 18.4f, 16.2f, 22.8f, 11f, 22.8f, 11f, 21.8f, 15.2f, 21.8f, 15.4f, 19.4f, 11.2f, 19.4f); // trigger guard
        Box(1.6f, 10.8f, 19.6f, 8f, CutterTeal, 2.4f);                   // housing
        Box(1.6f, 16.6f, 19.6f, 2.2f, ToolSteelDark, 0.8f);              // under-rail
        for (var x = 9.6f; x <= 18.6f; x += 1.2f)
            Box(x, 11.6f, 0.6f, 2.4f, Gunmetal, 0.2f);                    // vents
        Ball(5.2f, 13.6f, 1.9f, ToolSteelDark);                           // dial
        Ball(5.2f, 13.6f, 1.35f, GaugeFace, sphere: false);
        Line(5.2f, 13.6f, 4.3f, 12.9f, new Color(150, 40, 30), 1f);
        Box(21f, 12.4f, 3.4f, 7.2f, Gunmetal, 0.8f);                     // coil housing
        for (var x = 21.6f; x <= 23.8f; x += 0.7f)
            Line(x, 12.8f, x, 19.2f, new Color(176, 112, 60), 0.75f);     // copper turns
        Rod(24.2f, 16f, 29.4f, 16f, 1.5f, Gunmetal);                     // nozzle barrel
        for (var x = 24.8f; x <= 28.6f; x += 0.9f)
            Line(x, 14.6f, x, 17.4f, Ink, 0.45f);                         // ribs
        Ring(30.2f, 16f, 1.2f, 0.45f, Copper);                           // hot ring at the mouth
        Glow(31f, 16f, 1.6f, new Color(120, 220, 255), 0.35f);
        Line(2.6f, 11.6f, 8.6f, 11.6f, Highlight, 0.3f);
        Grime(34, 0.45f);
    }
}
