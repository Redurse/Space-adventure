using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Medical supplies (World.Treatment.cs) - painted upright.
public static partial class ItemSkin
{
    private static readonly Mat Gauze = new(new Color(236, 232, 220), 0.1f, 0.08f);
    private static readonly Color CrossRed = new(206, 46, 44);

    private static void Cross(float cx, float cy, float arm, float thick, Color c)
    {
        Box(cx - thick / 2, cy - arm, thick, arm * 2, new Mat(c, 0.2f, 0f));
        Box(cx - arm, cy - thick / 2, arm * 2, thick, new Mat(c, 0.2f, 0f));
    }

    // A first-aid case: red hard shell, white cross, carry handle and latches.
    private static void MedKit()
    {
        Rod(11.5f, 7.2f, 20.5f, 7.2f, 1.3f, Rubber);
        Box(3.5f, 8.5f, 25f, 18.5f, Paint(180, 44, 40, 0.35f), 2.6f);
        Line(4f, 12.6f, 28f, 12.6f, Ink, 0.45f);
        Cross(16f, 19.6f, 5f, 3.4f, new Color(240, 238, 232));
        Box(5f, 10.4f, 2.6f, 3.6f, Steel, 0.5f);
        Box(24.4f, 10.4f, 2.6f, 3.6f, Steel, 0.5f);
    }

    // A rolled bandage seen from the end: gauze body, spiralled face, a loose tail.
    private static void Bandage()
    {
        Poly(Gauze, 16f, 22f, 29f, 24.5f, 28.5f, 28f, 15f, 26f);         // tail
        Box(4f, 9f, 17f, 15f, Gauze, 6f);
        for (var x = 7f; x <= 17f; x += 2.5f)
            Line(x, 9.6f, x, 23.4f, Ink, 0.12f);
        Ball(20f, 16.5f, 7.4f, Gauze, sphere: false);
        for (var r = 1.6f; r <= 6.4f; r += 1.6f)
            Ring(20f, 16.5f, r, 0.45f, Paint(190, 184, 170, 0.05f));
    }

    // A windlass tourniquet: blue strap in a loop, a red windlass rod, a buckle.
    private static void Tourniquet()
    {
        Ring(16f, 16f, 9f, 3.4f, Paint(54, 92, 160, 0.2f, 0.1f));
        Box(12f, 4.4f, 8f, 4.4f, Steel, 0.8f);                           // buckle
        EraseBox(13.4f, 5.6f, 5.2f, 2f);
        Rod(7f, 22f, 25f, 12f, 1.5f, SafetyRed);                        // windlass
        Box(4.4f, 21f, 5f, 3f, Blackened, 0.8f);                          // clip
    }

    // An antiseptic bottle: amber glass, white cap, a label with a green cross.
    private static void Antiseptic()
    {
        Box(13f, 3.8f, 6f, 3.6f, WhitePlastic, 1f);
        Box(13.8f, 7.2f, 4.4f, 3.4f, Paint(150, 92, 40, 0.6f, 0f), 0.6f);
        Box(9.5f, 10f, 13f, 18f, Paint(158, 96, 42, 0.7f, 0f), 2.6f);
        Box(10.6f, 14.6f, 10.8f, 8.6f, Gauze, 0.6f);
        Cross(16f, 18.9f, 2.8f, 1.6f, new Color(50, 160, 80));
        Line(11f, 12f, 11f, 26f, Color.White, 0.45f);
    }

    // A SAM-style splint: two orange padded boards held by black straps.
    private static void Splint()
    {
        var board = Paint(226, 128, 46, 0.25f);
        Box(4f, 7.5f, 24f, 6f, board, 2.6f);
        Box(4f, 18.5f, 24f, 6f, board, 2.6f);
        Box(4f, 9.5f, 24f, 1.4f, Paint(70, 110, 180), 0.6f);
        Box(4f, 20.5f, 24f, 1.4f, Paint(70, 110, 180), 0.6f);
        Box(8.5f, 5.4f, 3f, 21.2f, Rubber, 0.8f);
        Box(20.5f, 5.4f, 3f, 21.2f, Rubber, 0.8f);
    }

    // A plaster cast: a chalk-white forearm cast with a thumb hole and a padded cuff, wrapped turns
    // showing - shaped like the limb it holds, so it never reads as the bandage roll.
    private static void PlasterCast()
    {
        var plaster = new Mat(new Color(238, 236, 228), 0.05f, 0.18f);
        Rod(6f, 25f, 20f, 11f, 5f, plaster);
        Ball(22.5f, 8.5f, 5.2f, plaster, sphere: false);
        for (var i = 1; i <= 4; i++)
        {
            var t = i / 5f;
            var cx = 6f + 14f * t;
            var cy = 25f - 14f * t;
            Line(cx - 3.6f, cy - 3.6f, cx + 3.6f, cy + 3.6f, Ink, 0.15f);
        }
        Rod(3.6f, 22.6f, 8.4f, 27.4f, 1.6f, Paint(90, 130, 190, 0.1f, 0.1f));   // padded cuff
        EraseBall(24.6f, 10.2f, 1.6f);                                          // thumb hole
        Dot(19f, 6.6f, Glint, 0.5f);
    }

    // Painkillers: an orange pill bottle with a white cap and two capsules in front.
    private static void Painkiller()
    {
        Box(8f, 6f, 13f, 4.4f, WhitePlastic, 1.2f);
        Box(8.8f, 10f, 11.4f, 15f, Paint(232, 140, 40, 0.7f, 0f), 1.8f);
        Box(9.8f, 14f, 9.4f, 6f, Gauze, 0.6f);
        Line(11f, 16f, 18f, 16f, Ink, 0.4f);
        Line(11f, 18f, 16f, 18f, Ink, 0.4f);
        Rod(20f, 25.5f, 28f, 22.5f, 1.8f, WhitePlastic);
        Rod(24f, 24f, 28f, 22.5f, 1.8f, SafetyRed);
        Rod(16f, 28.4f, 22.4f, 28.4f, 1.6f, WhitePlastic);
        Rod(19.2f, 28.4f, 22.4f, 28.4f, 1.6f, Paint(60, 120, 200));
    }

    // Antibiotics: a foil blister strip of blue-and-white capsules.
    private static void Antibiotic()
    {
        Box(5f, 6.5f, 22f, 19.5f, new Mat(new Color(186, 192, 202), 0.6f, 0.05f), 1.6f);
        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 2; col++)
        {
            var x = 10f + col * 12f;
            var y = 10.5f + row * 5.8f;
            Rod(x - 3.2f, y, x + 3.2f, y, 1.9f, WhitePlastic);
            Rod(x, y, x + 3.2f, y, 1.9f, Paint(60, 120, 210));
        }
    }

    // Burn gel: a squeezed white tube with a blue band and blue cap.
    private static void BurnGel()
    {
        Poly(WhitePlastic, 3f, 11f, 22f, 12.4f, 22f, 19.6f, 3f, 21f);
        Box(3f, 10.6f, 2f, 10.8f, Paint(190, 194, 198), 0.4f);             // crimp
        Box(9f, 13.6f, 10f, 4.8f, Paint(60, 130, 210, 0.3f));
        Line(10f, 16f, 18f, 16f, Color.White, 0.6f);
        Box(21.6f, 13.6f, 2.6f, 4.8f, Steel, 0.4f);
        Box(24f, 12.8f, 5.4f, 6.4f, Paint(60, 120, 200, 0.4f), 1.2f);
    }

    // An adrenaline auto-injector: yellow pen, blue safety cap, orange needle end, a window.
    private static void Adrenaline()
    {
        Rod(6f, 16f, 25f, 16f, 2.8f, HazardYellow);
        Box(2.4f, 13.4f, 5f, 5.2f, Paint(60, 110, 200, 0.4f), 1.6f);
        Box(25f, 13.6f, 4.6f, 4.8f, Paint(232, 120, 40, 0.4f), 1.6f);
        Box(12f, 14.6f, 7f, 2.8f, Paint(250, 236, 200, 0.8f, 0f), 0.8f);
        Box(13f, 15.2f, 3f, 1.6f, Paint(220, 170, 60, 0.6f, 0f));
    }

    // A portable defibrillator: yellow case, a small screen, two paddles, a lightning mark.
    private static void Defibrillator()
    {
        Box(3f, 10f, 26f, 17f, Paint(226, 176, 44, 0.3f), 2.6f);
        Box(5.4f, 12.4f, 9f, 6f, Paint(24, 28, 26), 0.6f);
        Line(6f, 16f, 8f, 16f, new Color(110, 230, 120));
        Line(8f, 16f, 9f, 13.6f, new Color(110, 230, 120));
        Line(9f, 13.6f, 10.4f, 17.6f, new Color(110, 230, 120));
        Line(10.4f, 17.6f, 11.4f, 16f, new Color(110, 230, 120));
        Line(11.4f, 16f, 13.8f, 16f, new Color(110, 230, 120));
        Poly(new Mat(CrossRed, 0.2f, 0f), 21f, 12f, 18f, 18f, 20.4f, 18f, 19f, 24f, 24f, 16.4f, 21.4f, 16.4f, 23f, 12f);
        Box(5f, 5f, 8f, 4.6f, Gunmetal, 1.4f);                             // paddles
        Box(19f, 5f, 8f, 4.6f, Gunmetal, 1.4f);
        Rod(9f, 9.6f, 9f, 11f, 0.9f, Rubber);
        Rod(23f, 9.6f, 23f, 11f, 0.9f, Rubber);
        Box(6f, 21.6f, 7f, 3f, Rubber, 0.6f);                               // buttons
    }

    // A surgery kit: a steel instrument case with a red cross, and a scalpel laid on top.
    private static void SurgeryKit()
    {
        Box(3f, 9f, 26f, 16f, Paint(132, 146, 160, 0.45f), 2.2f);
        Line(3.5f, 13f, 28.5f, 13f, Ink, 0.45f);
        Box(14f, 10.4f, 4f, 3.6f, Steel, 0.5f);                             // latch
        Cross(10f, 19.4f, 3.4f, 2.2f, CrossRed);
        Rod(15f, 21f, 23f, 17f, 0.9f, Steel);                             // scalpel handle
        Poly(SteelLight, 23f, 16.4f, 28.5f, 13.2f, 27f, 15.8f, 23.6f, 17.8f);
    }
}
