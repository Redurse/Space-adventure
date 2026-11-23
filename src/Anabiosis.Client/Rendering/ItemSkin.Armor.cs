using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Armour (ArmorDefinitions.cs): the vest seen from the front, the helmet from the side.
public static partial class ItemSkin
{
    // Body armour: a dark carrier with two shoulder straps, a plate pocket across the chest and a row of pouches.
    private static void BodyArmor()
    {
        var cloth = Paint(58, 64, 58);
        var plate = Paint(80, 88, 82);
        Box(8.5f, 4f, 5f, 8f, cloth, 1.4f);                                              // shoulder straps
        Box(18.5f, 4f, 5f, 8f, cloth, 1.4f);
        Box(6f, 9.5f, 20f, 18.5f, cloth, 2.2f);                                          // the carrier
        Box(8.6f, 11.5f, 14.8f, 9.5f, plate, 1.2f);                                      // plate pocket
        Line(8.6f, 16.2f, 23.4f, 16.2f, Ink, 0.35f);
        Line(16f, 11.6f, 16f, 20.9f, Ink, 0.3f);
        Line(9.2f, 12.3f, 22.8f, 12.3f, Highlight, 0.25f);
        for (var x = 8.2f; x <= 19.6f; x += 5.6f)
        {
            Box(x, 22.4f, 4.4f, 4.4f, Blackened, 0.6f);                                  // pouches
            Line(x + 0.5f, 23.3f, x + 3.9f, 23.3f, Highlight, 0.22f);
        }
        Box(10f, 5.2f, 2f, 1.4f, GunWorn, 0.3f);                                         // buckles
        Box(20f, 5.2f, 2f, 1.4f, GunWorn, 0.3f);
        Line(6.8f, 10.5f, 25.2f, 10.5f, Highlight, 0.22f);
        Grime(71, 0.3f);
    }

    // The ballistic helmet: an olive shell with a short brim, an ear guard and a chin strap.
    private static void BallisticHelmet()
    {
        var shell = Paint(84, 92, 64);
        var shade = Paint(62, 68, 48);
        Poly(shell, 6f, 18f, 6.6f, 13f, 9f, 8.6f, 13f, 6.2f, 17.5f, 5.8f, 22f, 7.4f, 25f, 11f, 26f, 16.5f, 26f, 19f, 6f, 19f);
        Box(4.6f, 17.8f, 22.6f, 2.5f, shade, 1.1f);                                      // brim
        Box(15.5f, 19.6f, 8f, 5.8f, shade, 1.6f);                                        // ear guard
        Line(9f, 20.4f, 13f, 26f, Ink, 0.9f);                                      // chin strap
        Line(13f, 26f, 19f, 25.2f, Ink, 0.9f);
        Line(9.6f, 9.6f, 14.4f, 7f, Highlight, 0.3f);
        Dot(21.5f, 13.6f, Ink, 0.9f, 0.7f);                                        // rail bolt
        Dot(18.4f, 21.6f, Ink, 0.8f, 0.6f);
        Grime(72, 0.3f);
    }
}

// The other rounds the guns take (GunDefinitions.Variants): each is the ordinary round with a coloured corner,
// the way a box of special ammunition is marked - green for a depleted core, blue for rubber, red for explosive,
// violet for the heavy harpoon.
public static partial class ItemSkin
{
    private static void AmmoMark(Mat color)
    {
        Poly(Blackened, 19.5f, 31f, 31f, 19.5f, 31f, 31f);
        Poly(color, 21.5f, 30.2f, 30.2f, 21.5f, 30.2f, 30.2f);
    }

    private static void RevolverRoundsDepleted() { RevolverRounds(); AmmoMark(Paint(92, 196, 96)); }
    private static void SmgMagazineDepleted() { SmgMagazine(); AmmoMark(Paint(92, 196, 96)); }
    private static void ShotgunShellsRubber() { ShotgunShells(); AmmoMark(Paint(76, 132, 214)); }
    private static void ShotgunSlugs() { ShotgunShells(); AmmoMark(Paint(222, 78, 52)); }
    private static void HarpoonsHeavy() { Harpoons(); AmmoMark(Paint(158, 108, 214)); }
    private static void HarpoonsExplosive() { Harpoons(); AmmoMark(Paint(222, 78, 52)); }
}
