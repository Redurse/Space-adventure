using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Wearables, personal kit and ship parts - painted upright.
public static partial class ItemSkin
{
    // An EVA suit: white shell with orange panels, a gold-mirrored visor, a chest control unit.
    private static void Spacesuit()
    {
        var shell = WhitePlastic with { Gloss = 0.35f };
        var panel = Paint(224, 120, 42, 0.3f);
        Rod(8.6f, 15f, 5.4f, 24f, 2.5f, shell);                 // arms
        Rod(23.4f, 15f, 26.6f, 24f, 2.5f, shell);
        Ball(5.2f, 25f, 2f, Rubber);                            // gloves
        Ball(26.8f, 25f, 2f, Rubber);
        Box(10.2f, 22.5f, 5f, 7.5f, shell, 1.4f);              // legs
        Box(16.8f, 22.5f, 5f, 7.5f, shell, 1.4f);
        Box(10f, 28.4f, 5.4f, 2.4f, Rubber, 0.8f);              // boots
        Box(16.6f, 28.4f, 5.4f, 2.4f, Rubber, 0.8f);
        Box(8.5f, 13.5f, 15f, 10.5f, shell, 3f);               // torso
        Box(8.5f, 19f, 15f, 2.2f, panel);                       // waist band
        Box(12.6f, 14.6f, 6.8f, 3.6f, Gunmetal, 0.8f);          // chest unit
        Dot(14f, 16.4f, new Color(90, 230, 120));
        Dot(16f, 16.4f, new Color(250, 190, 60));
        Dot(18f, 16.4f, new Color(230, 70, 60));
        Ball(16f, 8.4f, 6.2f, shell);                           // helmet
        Poly(new Mat(new Color(214, 152, 52), 0.9f, 0f), 11.6f, 7f, 20.4f, 7f, 19.6f, 11.4f, 12.4f, 11.4f);  // visor
        Line(12.6f, 7.8f, 15.4f, 7.8f, Color.White, 0.7f);
    }

    // A crew ID card: blue-and-white card, photo, gold chip, a clip slot.
    private static void IdCard()
    {
        Box(4f, 8f, 24f, 17f, WhitePlastic, 2f);
        Box(4f, 8f, 24f, 4.6f, Paint(52, 108, 186, 0.3f), 2f);
        Box(13.6f, 9.2f, 4.8f, 1.4f, Gunmetal, 0.6f);                 // clip slot
        Box(6.4f, 14f, 7f, 8.6f, Paint(150, 160, 172, 0.1f), 0.6f);    // photo
        Ball(9.9f, 16.6f, 1.8f, Paint(90, 96, 108, 0.1f), sphere: false);
        Box(7.2f, 19f, 5.4f, 3.6f, Paint(90, 96, 108, 0.1f), 1.4f);
        Box(16f, 14.2f, 5f, 3.8f, Brass, 0.6f);                        // chip
        Line(17.6f, 14.4f, 17.6f, 17.8f, Ink, 0.4f);
        Line(16f, 16.1f, 21f, 16.1f, Ink, 0.4f);
        Line(16f, 20f, 26f, 20f, Ink, 0.5f);
        Line(16f, 22f, 23f, 22f, Ink, 0.5f);
    }

    // A reactor fuel rod: a steel housing with a glowing green core and hazard-striped caps.
    private static void FuelRod()
    {
        Rod(16f, 4.5f, 16f, 27.5f, 4f, Steel);
        Box(14.3f, 8f, 3.4f, 16f, Paint(40, 46, 40, 0.2f), 1.4f);
        Box(14.9f, 8.6f, 2.2f, 14.8f, new Mat(new Color(120, 240, 120), 0.7f, 0f), 1f);
        Glow(16f, 16f, 6f, new Color(120, 255, 120), 0.45f);
        foreach (var y in new[] { 2.2f, 26f })
        {
            Box(11.6f, y, 8.8f, 3.6f, HazardYellow, 1f);
            for (var x = 12.4f; x < 20f; x += 2.4f)
                Poly(Blackened, x, y + 3.6f, x + 1.2f, y + 3.6f, x + 2.4f, y, x + 1.2f, y);
        }
    }

    // A spool of wire, face on: red plastic flange, copper windings, a dark hub, a loose end.
    private static void WireSpool()
    {
        Ball(16f, 16f, 13f, Paint(170, 46, 40, 0.35f), sphere: false);
        Ball(16f, 16f, 10f, Copper, sphere: false);
        for (var r = 4.6f; r <= 9.6f; r += 1.25f)
            Ring(16f, 16f, r, 0.5f, Paint(150, 78, 40, 0.2f));
        Ball(16f, 16f, 3.8f, Paint(52, 30, 26, 0.3f), sphere: false);
        EraseBall(16f, 16f, 1.6f);
        Line(24.6f, 21f, 29.5f, 27.5f, new Color(214, 132, 76), 1f, 1.6f);
        Dot(23.5f, 9.2f, Glint, 0.8f);
    }

    // A hull plate: a riveted steel panel with a weld seam and some scuffs.
    private static void HullPlating()
    {
        Poly(Steel, 3f, 10.5f, 24.5f, 6.5f, 29f, 21f, 7.5f, 25.5f);
        Poly(SteelLight, 3f, 10.5f, 24.5f, 6.5f, 25.2f, 8.6f, 3.6f, 12.6f);
        Line(15f, 9f, 18.4f, 23.4f, Ink, 0.5f);                        // weld seam
        Line(15.6f, 9f, 19f, 23.4f, Color.White, 0.25f);
        foreach (var (x, y) in new[] { (5.5f, 12.5f), (22.6f, 9f), (26.4f, 19.6f), (9f, 23f) })
            Dot(x, y, Ink, 0.9f, 0.8f);
        Line(8f, 17f, 12f, 16f, Color.White, 0.3f);
        Line(20.5f, 16.5f, 23f, 15.8f, Color.White, 0.3f);
    }
}
