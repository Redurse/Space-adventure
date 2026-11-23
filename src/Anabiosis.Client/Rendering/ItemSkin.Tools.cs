using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Hand tools - painted lying along +X (handle left, working end right), centre line y = 16, so a
// held tool's aim rotation turns the whole picture. The welder's and cutter's muzzle sits on the
// centre line at x = 31, where ItemIcons.GetMuzzleWorldPosition expects the flame to start.
public static partial class ItemSkin
{
    // A combat knife: clip-point blade with a bright edge, steel guard, ribbed black grip.
    private static void Knife()
    {
        Poly(SteelLight, 12.5f, 14.4f, 27f, 14.6f, 31f, 16.6f, 27.5f, 17.8f, 12.5f, 17.6f);
        Line(13f, 17.3f, 27.5f, 17.4f, Color.White, 0.55f);           // honed edge
        Line(13.5f, 15.6f, 25f, 15.7f, Ink, 0.25f);                   // fuller
        Box(10.8f, 12.4f, 1.9f, 7.2f, Steel, 0.5f);                     // guard
        Rod(2.5f, 16f, 10.5f, 16f, 2.3f, Rubber);
        for (var x = 4f; x <= 9.5f; x += 1.5f)
            Line(x, 14f, x, 18f, Color.White, 0.12f);
        Ball(2.4f, 16f, 1.5f, Steel);                                   // pommel
    }

    // A fire axe: black-and-yellow haft, red head with a polished edge and a back spike.
    private static void Axe()
    {
        Rod(1.8f, 16f, 27f, 16f, 1.55f, Blackened);
        for (var x = 3f; x <= 19f; x += 4f)
            Box(x, 14.4f, 2f, 3.2f, HazardYellow);                      // painted bands
        // Head: a narrow neck at the haft flaring out to a broad curved edge, a short spike behind.
        Poly(SafetyRed, 22.5f, 14.2f, 26.2f, 14.2f, 31f, 15f, 31f, 5.6f, 28.4f, 6.2f, 24.6f, 9.8f, 22.5f, 11f);
        Poly(SteelLight, 29.4f, 5.9f, 31f, 5.6f, 31f, 15f, 29.4f, 14.6f);   // ground edge
        Poly(SafetyRed, 22.5f, 17.8f, 26.2f, 17.8f, 24.4f, 23.5f);          // spike
        Line(22.5f, 16f, 26.2f, 16f, Ink, 0.5f);
    }
}
