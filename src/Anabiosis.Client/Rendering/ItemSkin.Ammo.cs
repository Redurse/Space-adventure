using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// What the guns fire (GunDefinitions.cs): loose rounds as a small handful, magazines standing upright.
public static partial class ItemSkin
{
    // Three fat hand cannon cartridges: short brass cases, blunt bullets with a painted band.
    private static void HandcannonRounds()
    {
        void Round(float x, float y)
        {
            Rod(x, y + 11f, x, y + 4.4f, 2.7f, Brass);
            Ball(x, y + 3.6f, 2.6f, GunWorn);
            Box(x - 2.7f, y + 5.4f, 5.4f, 1.2f, AccentOrange);
            Box(x - 3.1f, y + 11.2f, 6.2f, 1.5f, Brass, 0.5f);
        }
        Round(8.5f, 11f);
        Round(16f, 8f);
        Round(23.5f, 11.5f);
        Grime(61, 0.25f);
    }

    // A straight stick magazine for the SMG and the machine pistol, standing, rounds showing at the lips.
    private static void SmgMagazine()
    {
        Poly(GunDark, 13f, 6f, 19.4f, 6f, 20.6f, 27.4f, 14.2f, 28f);
        for (var y = 10.5f; y <= 24.5f; y += 3.5f)
            Dot(15.6f + (y - 6f) * 0.055f, y, Ink, 0.8f, 0.6f);                           // witness holes
        Line(18.6f, 7f, 19.6f, 26.6f, Highlight, 0.25f);
        Box(13.6f, 3.6f, 5f, 2.8f, Brass, 1.2f);
        Poly(Copper, 17.6f, 3.7f, 20.2f, 4.5f, 20.2f, 5.5f, 17.6f, 6.3f);              // bullet tip
        Box(13.4f, 26.6f, 8f, 2.2f, Blackened, 0.5f);                                   // floor plate
        Grime(62, 0.25f);
    }

    // Shotgun shells: red hulls on brass heads.
    private static void ShotgunShells()
    {
        void Shell(float x, float y)
        {
            Box(x - 2.5f, y, 5f, 9.4f, ShellRed, 0.8f);
            Line(x - 2.2f, y + 1.2f, x + 2.2f, y + 1.2f, Ink, 0.35f);                   // crimp
            Line(x - 1.4f, y + 2.2f, x - 1.4f, y + 8.6f, Highlight, 0.25f);
            Box(x - 2.8f, y + 9.2f, 5.6f, 3.6f, Brass, 0.6f);
            Line(x - 2.6f, y + 11.6f, x + 2.6f, y + 11.6f, Ink, 0.35f);
        }
        Shell(8f, 6.5f);
        Shell(15.6f, 4.5f);
        Shell(23.2f, 7f);
        Box(7f, 23.6f, 9.6f, 5f, ShellRed, 0.8f);                                       // one lying down
        Box(16.4f, 23.3f, 3.6f, 5.6f, Brass, 0.6f);
        Grime(63, 0.25f);
    }

    // Rifle cartridges: long bottlenecked cases, pointed copper bullets.
    private static void RifleRounds()
    {
        void Round(float x, float y)
        {
            Rod(x, y + 17f, x, y + 8.5f, 2f, Brass);                                    // case
            Taper(x, y + 8.5f, 2f, x, y + 6f, 1.2f, Brass);                             // shoulder
            Taper(x, y + 6f, 1.2f, x, y + 1.2f, 0.25f, Copper);                         // bullet
            Box(x - 2.4f, y + 17.2f, 4.8f, 1.3f, Brass, 0.4f);                          // rim
        }
        Round(9f, 8f);
        Round(15.6f, 5.5f);
        Round(22.2f, 8.5f);
        Grime(64, 0.2f);
    }

    // The machine gun box: olive steel, a stencilled band, a belt of rounds coming out of the top.
    private static void HmgMagazine()
    {
        for (var x = 9.5f; x <= 21.5f; x += 2.4f)
        {
            Rod(x, 9.4f, x, 6f, 0.95f, Brass);                                          // belt
            Ball(x, 5.2f, 0.9f, Copper);
        }
        Line(8.4f, 8.6f, 22.6f, 8.6f, Ink, 0.5f, 1.4f);                                 // belt links
        Box(5f, 10f, 22f, 17.6f, Olive, 1.4f);
        Box(5f, 10f, 22f, 3.6f, Paint(76, 82, 56), 1.4f);                               // lid
        Line(5.6f, 13.6f, 26.4f, 13.6f, Ink, 0.5f);
        Box(8f, 17.4f, 16f, 5f, HazardYellow, 0.3f);                                    // stencil band
        for (var x = 10f; x <= 20f; x += 3.3f)
            Poly(Blackened, x, 18.4f, x + 1.6f, 18.4f, x + 2.4f, 19.9f, x + 1.6f, 21.4f, x, 21.4f);
        Box(14.4f, 11.2f, 3.2f, 3.4f, GunWorn, 0.4f);                                   // latch
        Grime(65, 0.35f);
    }

    // A bundle of harpoons: steel shafts, barbed heads, taped together.
    private static void Harpoons()
    {
        void Spear(float x0, float y0, float x1, float y1)
        {
            Rod(x0, y0, x1, y1, 0.55f, GunWorn);
            var dx = x1 - x0;
            var dy = y1 - y0;
            var length = System.MathF.Sqrt(dx * dx + dy * dy);
            var ux = dx / length;
            var uy = dy / length;
            // the head: a barbed point past the end of the shaft
            Poly(GunWorn, x1 + ux * 3.2f, y1 + uy * 3.2f, x1 - ux * 1.2f - uy * 1.9f, y1 - uy * 1.2f + ux * 1.9f,
                x1, y1, x1 - ux * 1.2f + uy * 1.9f, y1 - uy * 1.2f - ux * 1.9f);
        }
        Spear(4f, 27f, 23f, 8f);
        Spear(6.6f, 28.6f, 25.6f, 9.6f);
        Spear(3f, 24.4f, 22f, 5.4f);
        Poly(AccentOrange, 9.2f, 17f, 12.4f, 14f, 16.6f, 18.4f, 13.4f, 21.4f);          // tape
        Line(10.2f, 17.4f, 14f, 20.6f, Ink, 0.3f);
        Grime(66, 0.2f);
    }
}
