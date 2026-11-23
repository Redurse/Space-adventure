using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// The medical items (MedicalTreatment.Items), drawn in the same flat, readable pictogram style as the other item icons: each reads at a
// glance by its silhouette and colour - a roll of gauze, a strap, a bottle, planks, a syringe, paddles, a surgeon's case.
public static partial class ItemIcons
{
    private static bool HasMedicalIcon(ItemType type) =>
        type is ItemType.Bandage or ItemType.Tourniquet or ItemType.Antiseptic or ItemType.Splint or ItemType.PlasterCast or ItemType.Painkiller
            or ItemType.Antibiotic or ItemType.BurnGel or ItemType.Adrenaline or ItemType.Defibrillator or ItemType.SurgeryKit;

    private static bool DrawMedicalIcon(SpriteBatch spriteBatch, Texture2D pixel, ItemType type, Rectangle rect)
    {
        if (!HasMedicalIcon(type))
            return false;
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f;
        GroundShadow(spriteBatch, pixel, origin, scale, 0.34f, 0.30f);

        switch (type)
        {
            case ItemType.Bandage:
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.62f, 0.40f, new Color(236, 232, 220)); // the roll
                Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.10f, 0.62f, 0.05f, new Color(206, 198, 180));
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.10f, 0.62f, 0.05f, new Color(206, 198, 180));
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.16f, 0.16f, new Color(210, 50, 50)); // a little red cross
                Bar(spriteBatch, pixel, origin, a, scale, 0.34f, 0.16f, 0.22f, 0.08f, new Color(236, 232, 220)); // loose end
                break;
            case ItemType.Tourniquet:
                Circle(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.30f, new Color(230, 140, 40));
                Circle(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.18f, new Color(24, 26, 32));
                Bar(spriteBatch, pixel, origin, a, scale, 0.20f, -0.20f, 0.20f, 0.12f, new Color(90, 92, 100)); // buckle
                Bar(spriteBatch, pixel, origin, a, scale, 0.28f, -0.30f, 0.10f, 0.22f, new Color(60, 62, 70)); // windlass
                break;
            case ItemType.Antiseptic:
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.05f, 0.36f, 0.50f, new Color(120, 190, 230)); // bottle
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.08f, 0.36f, 0.22f, new Color(240, 244, 248)); // label
                Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.28f, 0.18f, 0.12f, new Color(70, 140, 200)); // neck
                Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.38f, 0.24f, 0.08f, new Color(230, 230, 236)); // cap
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.08f, 0.14f, 0.05f, new Color(210, 50, 50));
                break;
            case ItemType.Splint:
                Bar(spriteBatch, pixel, origin, a, scale, -0.12f, 0f, 0.14f, 0.66f, new Color(206, 164, 104)); // plank
                Bar(spriteBatch, pixel, origin, a, scale, 0.12f, 0f, 0.14f, 0.66f, new Color(190, 148, 92)); // plank
                Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.16f, 0.46f, 0.07f, new Color(230, 226, 214)); // strap
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.16f, 0.46f, 0.07f, new Color(230, 226, 214));
                break;
            case ItemType.PlasterCast:
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.64f, 0.36f, new Color(246, 246, 244)); // rolled plaster
                Bar(spriteBatch, pixel, origin, a, scale, -0.18f, 0f, 0.04f, 0.36f, new Color(212, 212, 208));
                Bar(spriteBatch, pixel, origin, a, scale, 0.18f, 0f, 0.04f, 0.36f, new Color(212, 212, 208));
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.20f, 0.20f, new Color(150, 150, 156));
                break;
            case ItemType.Painkiller:
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.62f, 0.44f, new Color(196, 204, 214)); // blister strip
                for (var i = 0; i < 3; i++)
                    Circle(spriteBatch, pixel, origin, a, scale, -0.18f + i * 0.18f, 0f, 0.075f, i == 1 ? new Color(250, 214, 80) : new Color(244, 244, 248));
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.22f, 0.62f, 0.05f, new Color(120, 130, 150));
                break;
            case ItemType.Antibiotic:
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.05f, 0.34f, 0.48f, new Color(200, 130, 40)); // amber bottle
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.08f, 0.34f, 0.20f, new Color(238, 244, 238));
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.08f, 0.22f, 0.07f, new Color(60, 160, 90));
                Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.36f, 0.22f, 0.10f, new Color(60, 160, 90)); // cap
                break;
            case ItemType.BurnGel:
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.26f, 0.60f, new Color(238, 150, 60)); // tube
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.28f, 0.34f, 0.07f, new Color(210, 120, 40)); // crimped end
                Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.34f, 0.14f, 0.10f, new Color(235, 235, 240)); // cap
                Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.02f, 0.16f, 0.16f, new Color(250, 240, 220));
                break;
            case ItemType.Adrenaline:
                Bar(spriteBatch, pixel, origin, a, scale, -0.04f, 0f, 0.46f, 0.15f, new Color(226, 232, 238)); // barrel
                Bar(spriteBatch, pixel, origin, a, scale, -0.08f, 0f, 0.28f, 0.09f, new Color(214, 50, 50)); // the dose
                Bar(spriteBatch, pixel, origin, a, scale, -0.30f, 0f, 0.07f, 0.28f, new Color(160, 166, 176)); // plunger flange
                Bar(spriteBatch, pixel, origin, a, scale, -0.38f, 0f, 0.14f, 0.08f, new Color(160, 166, 176)); // plunger
                Bar(spriteBatch, pixel, origin, a, scale, 0.26f, 0f, 0.20f, 0.03f, new Color(200, 204, 212)); // needle
                Bar(spriteBatch, pixel, origin, a, scale, 0.18f, 0f, 0.07f, 0.24f, new Color(160, 166, 176)); // flange
                break;
            case ItemType.Defibrillator:
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.04f, 0.70f, 0.42f, new Color(232, 196, 60)); // case
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.20f, 0.70f, 0.06f, new Color(150, 120, 30));
                Bar(spriteBatch, pixel, origin, a, scale, -0.16f, -0.02f, 0.22f, 0.22f, new Color(38, 40, 46)); // screen
                Bar(spriteBatch, pixel, origin, a, scale, -0.16f, -0.02f, 0.14f, 0.03f, new Color(80, 230, 110)); // pulse line
                Bar(spriteBatch, pixel, origin, a, scale, 0.18f, -0.02f, 0.20f, 0.20f, new Color(212, 56, 48)); // shock button
                Bar(spriteBatch, pixel, origin, a, scale, -0.24f, -0.28f, 0.14f, 0.12f, new Color(70, 72, 80)); // paddle
                Bar(spriteBatch, pixel, origin, a, scale, 0.24f, -0.28f, 0.14f, 0.12f, new Color(70, 72, 80));
                break;
            default: // SurgeryKit
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.06f, 0.78f, 0.52f, new Color(60, 64, 74)); // case
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.28f, 0.78f, 0.05f, new Color(40, 44, 52));
                Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.28f, 0.28f, 0.08f, new Color(90, 94, 106)); // clasp
                Bar(spriteBatch, pixel, origin, a, scale, -0.10f, 0.04f, 0.40f, 0.05f, new Color(214, 220, 228)); // scalpel blade
                Bar(spriteBatch, pixel, origin, a, scale, 0.18f, 0.04f, 0.20f, 0.09f, new Color(150, 154, 164)); // handle
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.04f, 0.14f, 0.14f, new Color(210, 50, 50)); // the red cross of medicine
                Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.04f, 0.04f, 0.14f, new Color(236, 236, 240));
                break;
        }
        return true;
    }
}
