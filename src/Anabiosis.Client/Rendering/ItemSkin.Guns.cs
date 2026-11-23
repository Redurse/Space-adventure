using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// The firearms (GunDefinitions.cs), drawn after the look of the Barotrauma weapon sheet (direct user request,
// "сделай текстуры оружия на основе этих спрайтов"): chunky industrial guns in dark worn steel, black polymer
// and scuffed wood, with one small painted accent each. The drawings are original - the sheet is the reference
// for style and for what each gun is, not something traced. Guns lie along +X (stock left, muzzle right).
public static partial class ItemSkin
{
    private static readonly Mat GunSteel = new(new Color(92, 97, 106), 0.45f, 0.07f);
    private static readonly Mat GunDark = new(new Color(60, 63, 70), 0.32f, 0.06f);
    private static readonly Mat GunWorn = new(new Color(134, 138, 144), 0.5f, 0.07f);
    private static readonly Mat Polymer = new(new Color(50, 50, 54), 0.18f, 0.05f);
    private static readonly Mat Walnut = new(new Color(110, 68, 40), 0.18f, 0.11f);
    private static readonly Mat TanGrip = new(new Color(146, 118, 82), 0.15f, 0.09f);
    private static readonly Mat AccentOrange = new(new Color(190, 104, 44), 0.25f, 0.07f);
    private static readonly Mat AccentRed = new(new Color(158, 54, 44), 0.25f, 0.07f);
    private static readonly Mat ShellRed = new(new Color(164, 50, 44), 0.3f, 0.06f);

    // A trigger inside its guard.
    private static void TriggerGuard(float x, float y)
    {
        Ring(x, y, 2f, 0.8f, GunDark);
        Line(x + 0.2f, y - 1.4f, x + 0.6f, y + 0.8f, Ink, 0.8f);
    }

    // ------------------------------------------------------------------ handguns

    // A six-shot revolver: scuffed walnut grip, heavy frame, fluted cylinder, a barrel with a full lug.
    private static void Revolver()
    {
        Poly(Walnut, 3.4f, 17.6f, 9.4f, 16.6f, 11f, 26.2f, 5.2f, 27.2f);               // grip
        Line(5.4f, 20f, 9.4f, 19.4f, Ink, 0.3f);
        Line(5.9f, 23f, 9.9f, 22.4f, Ink, 0.3f);
        Dot(7.6f, 25.6f, Glint, 0.5f, 0.5f);                                            // grip screw
        Box(15.4f, 12.4f, 13.6f, 3f, GunSteel, 0.5f);                                   // barrel
        Box(15.4f, 15.2f, 10.6f, 2f, GunDark, 0.5f);                                    // lug under it
        Box(27.6f, 10.9f, 1.2f, 1.7f, GunDark);                                         // front sight
        Box(7.2f, 11.8f, 9.4f, 6.8f, GunDark, 1f);                                      // frame
        Box(9.4f, 11.1f, 6.8f, 8.2f, GunSteel, 1.9f);                                   // cylinder
        for (var y = 12.9f; y <= 17.6f; y += 2.3f)
            Line(10f, y, 15.6f, y, Ink, 0.45f);                                         // flutes
        Line(9.8f, 11.6f, 15.6f, 11.5f, Highlight, 0.35f);
        Poly(GunDark, 6.4f, 12.2f, 7.2f, 10.4f, 9f, 11f, 9.2f, 12.2f);                            // hammer
        TriggerGuard(11.6f, 20.8f);
        Line(16f, 12.8f, 27.6f, 12.7f, Highlight, 0.3f);
        Grime(51, 0.3f);
    }

    // The hand cannon: a slab of a barrel with ports cut in it, a cylinder too big for the frame, a rubber grip.
    private static void Handcannon()
    {
        Poly(Rubber, 2.6f, 18f, 9.2f, 16.8f, 11.2f, 27.6f, 4.6f, 28.6f);               // grip
        for (var y = 20f; y <= 26f; y += 1.5f)
            Line(4.4f + (y - 18f) * 0.18f, y, 9.8f + (y - 18f) * 0.18f, y - 0.6f, Ink, 0.25f);
        Box(14.2f, 9.8f, 16f, 5.8f, GunSteel, 0.9f);                                    // slab barrel
        for (var x = 18f; x <= 26f; x += 2.6f)
            Box(x, 10.9f, 1.5f, 1.3f, Blackened, 0.3f);                                 // ports
        Box(14.2f, 15.2f, 13f, 2.4f, GunDark, 0.6f);                                    // lug
        Box(29.4f, 11.2f, 1.2f, 3f, Blackened, 0.3f);                                   // muzzle
        Box(26.6f, 8.2f, 1.6f, 1.8f, AccentOrange);                                     // front sight
        Box(6f, 10.6f, 10.4f, 8.4f, GunDark, 1.2f);                                     // frame
        Box(8.2f, 9.5f, 8f, 10.4f, GunWorn, 2.4f);                                      // cylinder
        for (var y = 11.6f; y <= 18f; y += 2.1f)
            Line(8.9f, y, 15.6f, y, Ink, 0.5f);
        Line(8.8f, 10f, 15.4f, 9.9f, Highlight, 0.4f);
        Poly(GunDark, 5f, 11.2f, 5.8f, 9f, 7.8f, 9.6f, 8.2f, 11f);                          // hammer
        TriggerGuard(11.6f, 21.2f);
        Line(15f, 10.3f, 29f, 10.2f, Highlight, 0.3f);
        Grime(52, 0.35f);
    }

    // A machine pistol: a boxy slide, a stub of a barrel, and a magazine far longer than the grip it sits in.
    private static void MachinePistol()
    {
        Box(9.6f, 22f, 4.2f, 7.2f, GunDark, 0.5f);                                      // magazine out of the grip
        Line(10.2f, 25f, 13.2f, 25f, Ink, 0.35f);
        Line(10.2f, 27f, 13.2f, 27f, Ink, 0.35f);
        Poly(Polymer, 8.6f, 15.6f, 14.8f, 15.6f, 14f, 23.4f, 8.2f, 23.4f);              // grip
        for (var y = 17.6f; y <= 22f; y += 1.4f)
            Line(9.2f, y, 13.6f, y, Ink, 0.22f);
        Box(24.6f, 11.9f, 4.8f, 2.8f, GunDark, 0.5f);                                   // barrel
        Box(28.6f, 11.5f, 1.6f, 3.6f, Blackened, 0.4f);                                 // compensator
        Box(5.6f, 10.2f, 19.6f, 6f, GunSteel, 0.9f);                                    // slide
        for (var x = 7f; x <= 10.2f; x += 1.1f)
            Line(x, 11f, x, 15.4f, Ink, 0.4f);                                          // serrations
        Box(15f, 11.6f, 5.4f, 2.2f, Blackened, 0.5f);                                   // ejection port
        Box(6.2f, 9f, 1.8f, 1.5f, GunDark);                                             // rear sight
        Box(22.4f, 9.2f, 1.2f, 1.3f, AccentRed);                                        // front sight
        TriggerGuard(17.2f, 18.2f);
        Line(6.2f, 10.7f, 24.6f, 10.6f, Highlight, 0.35f);
        Grime(53, 0.3f);
    }

    // ------------------------------------------------------------------ long guns

    // A submachine gun: a wire stock, a plain box of a receiver, a holed barrel shroud, a straight magazine.
    private static void Smg()
    {
        Rod(2f, 13.6f, 8.4f, 13.6f, 0.65f, GunDark);                                    // wire stock
        Rod(2f, 19.4f, 8.4f, 17.6f, 0.65f, GunDark);
        Box(1f, 12.6f, 1.7f, 7.8f, Rubber, 0.5f);                                       // butt plate
        Box(16.8f, 17.6f, 3.6f, 9.4f, GunDark, 0.5f);                                   // magazine
        Line(17.4f, 21f, 19.8f, 21f, Ink, 0.35f);
        Line(17.4f, 24f, 19.8f, 24f, Ink, 0.35f);
        Poly(Polymer, 9.4f, 17.6f, 12.8f, 17.6f, 11.6f, 24.6f, 8.2f, 24.2f);            // grip
        Rod(28.4f, 14.8f, 30.8f, 14.8f, 0.8f, GunWorn);                                 // muzzle
        Box(21.6f, 12.8f, 7.2f, 4f, GunDark, 0.8f);                                     // shroud
        for (var x = 23f; x <= 27.4f; x += 1.5f)
        {
            Dot(x, 13.9f, Ink, 0.85f, 0.45f);
            Dot(x, 15.7f, Ink, 0.85f, 0.45f);
        }
        Box(8f, 11.6f, 14f, 6.4f, GunSteel, 0.9f);                                      // receiver
        Box(12.4f, 13.2f, 4.6f, 1.5f, Blackened, 0.4f);                                 // bolt slot
        Ball(15.6f, 13.9f, 0.8f, GunWorn);                                              // charging knob
        Box(9f, 15.6f, 2.6f, 1.2f, AccentOrange, 0.3f);                                 // safety mark
        Box(9.4f, 10.2f, 1.8f, 1.6f, GunDark);                                          // sights
        Box(20f, 10.4f, 1.2f, 1.4f, GunDark);
        TriggerGuard(14.6f, 19.4f);
        Line(8.6f, 12.1f, 21.6f, 12f, Highlight, 0.35f);
        Grime(54, 0.3f);
    }

    // A pump shotgun: walnut stock and pump, a barrel over its tube magazine.
    private static void Shotgun()
    {
        Poly(Walnut, 1.2f, 14.2f, 9.2f, 13.4f, 11.6f, 18.4f, 9.6f, 19.4f, 3.2f, 21.6f, 1.2f, 20.8f);   // stock
        Line(3f, 16f, 8.6f, 15.2f, Highlight, 0.2f);
        Box(0.8f, 14.4f, 1.3f, 6.4f, Rubber, 0.4f);                                     // butt pad
        Rod(16.6f, 13.6f, 30.8f, 13.6f, 1f, GunSteel);                                  // barrel
        Rod(16.6f, 16.4f, 27.4f, 16.4f, 1f, GunDark);                                   // tube magazine
        Box(27.2f, 15.2f, 1.2f, 2.4f, GunWorn, 0.3f);                                   // magazine cap
        Box(18.8f, 15f, 6.8f, 3.2f, Walnut, 1.4f);                                      // pump
        for (var x = 19.8f; x <= 24.8f; x += 1.2f)
            Line(x, 15.4f, x, 17.8f, Ink, 0.3f);
        Box(8.8f, 12.2f, 8.4f, 6f, GunSteel, 0.9f);                                     // receiver
        Box(11.4f, 14f, 4.2f, 1.6f, Blackened, 0.4f);                                   // ejection port
        TriggerGuard(13f, 19.6f);
        Ball(30.2f, 12.3f, 0.55f, Brass);                                               // bead sight
        Line(9.4f, 12.7f, 30f, 12.7f, Highlight, 0.3f);
        Grime(55, 0.35f);
    }

    // An automatic shotgun: black polymer, a drum under the receiver, a fat barrel in a slotted heat shield.
    private static void AutoShotgun()
    {
        Poly(Polymer, 1.2f, 13.2f, 8.8f, 12.4f, 8.8f, 18.6f, 3.2f, 20.8f, 1.2f, 20f);   // stock
        Box(0.8f, 13.4f, 1.3f, 6.6f, Rubber, 0.4f);
        Box(3.4f, 15.2f, 4f, 2f, Blackened, 0.8f);                                      // stock cutout
        Ball(15.6f, 21.8f, 4.6f, GunDark, false);                                       // drum
        Ring(15.6f, 21.8f, 3f, 0.6f, GunWorn);
        Ball(15.6f, 21.8f, 1f, GunWorn);
        Poly(Polymer, 8.8f, 18.2f, 11.6f, 18.2f, 10.4f, 24.6f, 7.4f, 24.2f);            // grip
        Rod(28.6f, 14.4f, 31f, 14.4f, 1.35f, GunWorn);                                  // muzzle
        Box(20.6f, 12f, 8.6f, 5f, GunDark, 0.9f);                                       // heat shield
        for (var x = 21.8f; x <= 27.6f; x += 1.9f)
            Box(x, 13f, 1f, 3f, Blackened, 0.4f);
        Box(8.2f, 11.2f, 13f, 7f, GunSteel, 1f);                                        // receiver
        Box(12f, 13f, 4.6f, 1.7f, Blackened, 0.4f);                                     // port
        Rod(10f, 9.6f, 19f, 9.6f, 0.55f, GunDark);                                      // carry rail
        Line(10.6f, 10f, 10.6f, 11.2f, Ink, 0.9f, 1.4f);
        Line(18.4f, 10f, 18.4f, 11.2f, Ink, 0.9f, 1.4f);
        Box(9.2f, 16f, 3f, 1.2f, AccentRed, 0.3f);
        TriggerGuard(13.4f, 19.4f);
        Line(8.8f, 11.7f, 20.6f, 11.6f, Highlight, 0.35f);
        Grime(56, 0.3f);
    }

    // The assault rifle: polymer stock, tan handguard, a curved magazine, iron sights on a top rail.
    private static void Rifle()
    {
        Poly(Polymer, 1.2f, 13.6f, 8.6f, 13f, 8.6f, 18.6f, 3f, 21.2f, 1.2f, 20.6f);     // stock
        Box(0.8f, 13.8f, 1.3f, 6.6f, Rubber, 0.4f);
        Poly(GunDark, 12.4f, 18f, 16f, 18f, 18f, 25.4f, 14.2f, 26.2f);                  // curved magazine
        Line(13.2f, 20.8f, 16.8f, 20.4f, Ink, 0.4f);
        Line(13.8f, 23.4f, 17.4f, 23f, Ink, 0.4f);
        Poly(Polymer, 8.8f, 18f, 11.4f, 18f, 10.2f, 23.8f, 7.6f, 23.4f);                // grip
        Rod(24f, 14.8f, 30.6f, 14.8f, 0.85f, GunWorn);                                  // barrel
        Box(28.4f, 13.5f, 2.6f, 2.6f, Blackened, 0.5f);                                 // muzzle brake
        Box(17.8f, 12.8f, 7.4f, 4.2f, TanGrip, 1.1f);                                   // handguard
        for (var x = 19.2f; x <= 24f; x += 1.6f)
            Line(x, 13.4f, x, 16.4f, Ink, 0.3f);
        Box(8.2f, 12.2f, 10.2f, 6f, GunSteel, 0.9f);                                    // receiver
        Box(11.4f, 13.8f, 4f, 1.5f, Blackened, 0.4f);                                   // port
        Rod(9f, 11.2f, 24f, 11.2f, 0.45f, GunDark);                                     // top rail
        Box(9.6f, 9.4f, 1.8f, 1.8f, GunDark);                                           // rear sight
        Box(25.6f, 11.6f, 1.2f, 2.2f, GunDark);                                         // front sight post
        TriggerGuard(13.4f, 19.6f);
        Line(8.8f, 12.7f, 17.8f, 12.6f, Highlight, 0.35f);
        Grime(57, 0.3f);
    }

    // The bolt-action rifle: one long walnut stock, a thin barrel, a bolt handle, a scope.
    private static void BoltRifle()
    {
        Poly(Walnut, 1.2f, 14.8f, 9.6f, 13.8f, 22.6f, 14.6f, 22.6f, 17.2f, 12.6f, 18.4f, 10f, 19.2f, 3.4f, 22.2f, 1.2f, 21.4f);
        Line(3f, 16.4f, 9.4f, 15.2f, Highlight, 0.2f);
        Line(13f, 17.4f, 22f, 16.4f, Ink, 0.25f);
        Box(0.8f, 15f, 1.3f, 6.6f, Rubber, 0.4f);                                       // butt pad
        Rod(18f, 13.8f, 31f, 13.8f, 0.75f, GunSteel);                                   // barrel
        Box(30f, 12.4f, 0.9f, 1.2f, GunDark);                                           // front sight
        Box(9.4f, 12.6f, 9.4f, 2.8f, GunSteel, 0.9f);                                   // action
        Rod(14.6f, 14.4f, 16.2f, 17.4f, 0.45f, GunWorn);                                // bolt handle
        Ball(16.4f, 17.8f, 0.9f, GunWorn);
        Rod(10f, 10f, 20f, 10f, 1.2f, GunDark);                                         // scope
        Rod(9.4f, 10f, 10.8f, 10f, 1.55f, GunDark);
        Rod(18.8f, 10f, 20.6f, 10f, 1.6f, GunDark);
        Ball(20.8f, 10f, 1.15f, new Mat(new Color(70, 150, 190), 0.9f, 0f));            // lens
        Box(14.2f, 8f, 1.6f, 1.4f, GunWorn, 0.4f);                                      // turret
        Line(12.4f, 11.4f, 12.4f, 12.8f, Ink, 0.9f, 1.4f);                              // scope mounts
        Line(17.4f, 11.4f, 17.4f, 12.8f, Ink, 0.9f, 1.4f);
        TriggerGuard(12.6f, 19.8f);
        Grime(58, 0.35f);
    }

    // The heavy machine gun: a slab receiver with its ammunition box hung under it, a jacketed barrel, a bipod.
    private static void Hmg()
    {
        Poly(Polymer, 1f, 12.6f, 5.4f, 12.2f, 5.4f, 18.6f, 2.6f, 20f, 1f, 19.4f);       // stub stock
        Box(0.6f, 12.8f, 1.2f, 6.4f, Rubber, 0.4f);
        Box(8f, 18.2f, 8.2f, 8f, Olive, 0.9f);                                          // ammunition box
        Box(8f, 18.2f, 8.2f, 2f, Paint(74, 80, 54), 0.9f);
        Box(9.4f, 21.6f, 5.4f, 2.4f, HazardYellow, 0.3f);                               // stencil
        Box(11.4f, 19.4f, 1.6f, 1.6f, GunWorn, 0.3f);                                   // latch
        Poly(Polymer, 4.8f, 18.2f, 7.4f, 18.2f, 6.4f, 24f, 3.6f, 23.6f);                // grip
        Rod(24f, 17.2f, 27.6f, 22.6f, 0.5f, GunDark);                                   // bipod, folded forward
        Rod(25.4f, 17.2f, 29.6f, 21.8f, 0.5f, GunDark);
        Rod(27.4f, 14.4f, 31f, 14.4f, 1.05f, GunWorn);                                  // barrel
        Box(30f, 13f, 1.4f, 2.8f, Blackened, 0.4f);                                     // flash hider
        Box(18f, 11.8f, 10f, 5.2f, GunDark, 1.3f);                                      // cooling jacket
        for (var x = 19.6f; x <= 26.8f; x += 1.8f)
        {
            Dot(x, 13.2f, Ink, 0.85f, 0.5f);
            Dot(x + 0.9f, 15.4f, Ink, 0.85f, 0.5f);
        }
        Box(4.8f, 10.6f, 13.6f, 7.8f, GunSteel, 1f);                                    // receiver
        Box(8.4f, 12.6f, 6.4f, 1.8f, Blackened, 0.4f);                                  // feed cover seam
        Rod(7f, 8.8f, 15.6f, 8.8f, 0.6f, GunDark);                                      // carry handle
        Line(7.6f, 9.2f, 7.6f, 10.6f, Ink, 0.9f, 1.4f);
        Line(15f, 9.2f, 15f, 10.6f, Ink, 0.9f, 1.4f);
        Box(5.6f, 15.6f, 2.6f, 1.2f, AccentOrange, 0.3f);
        Line(5.4f, 11.1f, 18f, 11f, Highlight, 0.35f);
        Grime(59, 0.35f);
    }

    // The harpoon gun: a painted tube over a gas cylinder, a spear standing out of the front of it.
    private static void HarpoonGun()
    {
        Poly(Rubber, 7f, 17f, 10.2f, 17f, 9f, 24.4f, 5.8f, 24f);                        // grip
        Rod(12.4f, 18.8f, 20.6f, 18.8f, 1.45f, CylinderSteel);                          // gas cylinder
        Rod(20.6f, 18.8f, 21.8f, 18.8f, 0.8f, Brass);                                   // its valve
        Line(13f, 18f, 20f, 18f, Highlight, 0.3f);
        Rod(22f, 14.6f, 30f, 14.6f, 0.4f, GunWorn);                                     // spear shaft
        Poly(GunWorn, 28.6f, 12.8f, 31.6f, 14.6f, 28.6f, 16.4f, 29.4f, 14.6f);          // barbed head
        Rod(6f, 14.6f, 23.4f, 14.6f, 1.7f, Amber);                                      // body tube
        Box(11.6f, 12.7f, 1.3f, 3.8f, GunDark, 0.3f);                                   // bands
        Box(19.6f, 12.7f, 1.3f, 3.8f, GunDark, 0.3f);
        Box(22.6f, 12.9f, 1.6f, 3.4f, GunSteel, 0.4f);                                  // muzzle collar
        Box(3.6f, 12.4f, 5.4f, 4.6f, GunSteel, 0.9f);                                   // rear housing
        Ball(4.6f, 14.7f, 0.8f, GunWorn);
        TriggerGuard(12f, 17.6f);
        Line(7f, 13.6f, 22f, 13.5f, Highlight, 0.3f);
        Grime(60, 0.35f);
    }
}
