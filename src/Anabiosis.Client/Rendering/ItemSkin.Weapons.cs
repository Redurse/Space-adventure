using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// The laser rifle and the older ammunition; the firearms are in ItemSkin.Guns.cs, their rounds in ItemSkin.Ammo.cs.
public static partial class ItemSkin
{
    // A laser rifle: white composite shell, a glowing energy cell, a coil-wrapped emitter.
    private static void LaserRifle()
    {
        Poly(WhitePlastic, 1.2f, 14.4f, 9f, 13.2f, 9f, 19f, 3.4f, 21.4f, 1.2f, 20.6f);   // stock
        Poly(Rubber, 9.2f, 18.2f, 11.6f, 18.2f, 10.6f, 23.8f, 8.2f, 23.4f);              // grip
        Box(12.6f, 18f, 4.4f, 5.6f, Gunmetal, 0.8f);                                      // cell housing
        Box(13.4f, 19.2f, 2.8f, 3.6f, new Mat(new Color(90, 230, 220), 0.8f, 0f), 0.4f);  // energy cell
        Glow(14.8f, 21f, 3f, new Color(90, 240, 230), 0.5f);
        Box(8.6f, 12.6f, 13f, 6f, WhitePlastic, 2f);                                      // body
        Line(10f, 15.6f, 20f, 15.6f, new Color(60, 200, 210), 0.9f, 1.4f);               // accent stripe
        Rod(21f, 15.4f, 29.5f, 15.4f, 1.4f, Gunmetal);                                  // emitter
        for (var x = 22.2f; x <= 27.4f; x += 1.3f)
            Rod(x, 13.8f, x, 17f, 0.55f, Copper);                                       // coils
        Ball(30f, 15.4f, 1.6f, new Mat(new Color(120, 240, 255), 0.9f, 0f));
        Glow(30.2f, 15.4f, 3.2f, new Color(120, 240, 255), 0.6f);
    }

    // A rifle box magazine, standing: ribbed blackened steel with brass rounds showing at the lips.
    private static void Magazine()
    {
        Poly(Gunmetal, 11.5f, 7f, 20f, 7f, 22.5f, 27f, 14f, 28f);
        for (var y = 11f; y <= 24f; y += 3.2f)
            Line(12.6f + (y - 7f) * 0.12f, y, 20.6f + (y - 7f) * 0.1f, y, Ink, 0.45f);
        Box(12.5f, 4.4f, 6.5f, 3.2f, Brass, 1.4f);
        Poly(Copper, 18.6f, 4.6f, 21.6f, 5.4f, 21.6f, 6.4f, 18.6f, 7.4f);              // bullet tip
        Box(11f, 26.4f, 12f, 2.6f, Blackened, 0.6f);                                     // floor plate
    }

    // A handful of revolver cartridges: brass cases, copper bullets.
    private static void RevolverRounds()
    {
        void Round(float x, float y)
        {
            Rod(x, y + 10f, x, y + 3f, 2f, Brass);
            Ball(x, y + 2.6f, 2f, Copper);
            Box(x - 2.4f, y + 10f, 4.8f, 1.4f, Brass, 0.5f);
        }
        Round(9f, 9f);
        Round(15.5f, 7f);
        Round(22f, 9.5f);
        Rod(11f, 26f, 24f, 26f, 2f, Brass);                                             // one lying down
        Ball(25f, 26f, 2f, Copper);
    }

    // An ammunition crate: olive steel box, stencilled band, two latches, a handle on top.
    private static void AmmoCrate()
    {
        Rod(11f, 7.6f, 21f, 7.6f, 1.2f, Blackened);                                   // handle
        Box(3.5f, 9f, 25f, 18f, Olive, 1.5f);
        Box(3.5f, 9f, 25f, 4f, Paint(80, 88, 58), 1.5f);                                // lid
        Line(4f, 13f, 28f, 13f, Ink, 0.55f);
        Box(6f, 17f, 20f, 5f, HazardYellow, 0.3f);                                      // stencil band
        Poly(Blackened, 9f, 18f, 13.5f, 18f, 15f, 19.5f, 13.5f, 21f, 9f, 21f);          // bullet glyph
        Poly(Blackened, 17f, 18f, 21.5f, 18f, 23f, 19.5f, 21.5f, 21f, 17f, 21f);
        Box(4.6f, 11.2f, 2.4f, 4f, Steel, 0.4f);                                        // latches
        Box(25f, 11.2f, 2.4f, 4f, Steel, 0.4f);
    }
}
