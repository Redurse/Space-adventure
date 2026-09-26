using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Held items, characters, nameplates and chat bubbles, plus a couple of generic outline-drawing helpers - split out of ShipRenderer.cs to keep that file to its own topic.
public sealed partial class ShipRenderer
{
    // Out in front of the body along the way the character is facing - far enough out that a
    // held-item icon doesn't overlap it, the same distance a tool's flame is now drawn from
    // (DrawWeldingFlame/DrawCuttingFlame in ShipRenderer.Draw and FieldRenderer.Draw), so the beam
    // reads as coming out of the tool in hand rather than out of the character's chest.
    internal static Vector2 HeldToolOffset(Vector2 facing)
    {
        if (facing.LengthSquared() < 0.01f)
            facing = new Vector2(1f, 0f);
        else
            facing = Vector2.Normalize(facing);
        return facing * (CharacterDiameter * PixelsPerUnit / 2f + 10f);
    }

    internal static IReadOnlyList<ItemType> HeldItemTypes(InventoryState? inventory) =>
        inventory is null
            ? Array.Empty<ItemType>()
            : inventory.HeldMainSlotIndices.Select(i => inventory.MainSlots[i]).OfType<ItemType>().ToArray();

    // A held tool/item reads as a small coloured chip out in front of the body - the same colour and
    // 1-2 letter label InventoryPanel already shows it by in a slot, just without the slot frame
    // (this project has no item sprites to actually put in someone's hand). Two held items (a
    // two-handed tool's "both hands" or two one-handed ones) sit side by side rather than stacked.
    // internal + static, pixel/font passed explicitly, so FieldRenderer's own (simpler) EVA
    // DrawCharacter draws the exact same icon for a suited crewmate holding a cutter outside.
    private const int HeldIconSize = 30;

    internal static void DrawHeldItems(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font,
        IReadOnlyList<ItemType> held, Vector2 center, Vector2 facing)
    {
        var facingAngle = MathF.Atan2(facing.Y, facing.X);
        for (var i = 0; i < held.Count; i++)
            DrawHeldItemIcon(spriteBatch, pixel, font, held[i], HeldItemIconRect(held, i, center, facing), facingAngle);
    }

    // The chip rect a given held item's icon actually gets drawn into - broken out so the flame's
    // own origin (GetHeldToolMuzzle below) can be computed from this exact rect/rotation instead of
    // a separately-tuned guess that only approximately lines up with the texture.
    private static Rectangle HeldItemIconRect(IReadOnlyList<ItemType> held, int index, Vector2 center, Vector2 facing)
    {
        var offset = HeldToolOffset(facing);
        var side = new Vector2(-offset.Y, offset.X);
        if (side.LengthSquared() > 0.01f)
            side.Normalize();
        var lateral = held.Count <= 1 ? 0f : (index == 0 ? -1f : 1f) * (HeldIconSize * 0.65f);
        var pos = center + offset + side * lateral;
        return new Rectangle((int)pos.X - HeldIconSize / 2, (int)pos.Y - HeldIconSize / 2, HeldIconSize, HeldIconSize);
    }

    private static bool IsTopDownGunTool(ItemType item) => item is ItemType.WeldingTool or ItemType.Cutter;

    // Where a currently-held Cutter/WeldingTool's own drawn muzzle sits, so FieldRenderer's flame can
    // start exactly there instead of a generic offset off the character's centre - null if the
    // character isn't holding one (or is holding one but it's not the item this frame's Cutting/
    // Welding flag is actually about, which can't happen: only one of each is ever equipped at once).
    internal static Vector2? GetHeldToolMuzzle(ItemType tool, InventoryState? inventory, Vector2 center, Vector2 facing)
    {
        var held = HeldItemTypes(inventory);
        var index = -1;
        for (var i = 0; i < held.Count; i++)
            if (held[i] == tool) { index = i; break; }
        if (index < 0)
            return null;

        var rect = HeldItemIconRect(held, index, center, facing);
        var facingAngle = MathF.Atan2(facing.Y, facing.X);
        var rectCenter = new Vector2(rect.Center.X, rect.Center.Y);
        return ItemIcons.GetTopDownMuzzleWorldPosition(rectCenter, facingAngle, HeldIconSize);
    }

    private static void DrawHeldItemIcon(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, ItemType item, Rectangle rect, float rotation)
    {
        // Every held item reads as the thing itself, in the character's hand, with nothing drawn
        // around it - no backdrop chip, no hand glyphs (those still make sense in the hotbar, an
        // abstract "this slot is equipped" square, but not once the item has its own recognizable
        // silhouette to look at directly).
        if (IsTopDownGunTool(item))
        {
            // The cutter/welder get their own top-down silhouette (the same angle the character
            // itself is seen from) rather than the side-view hotbar icon.
            var rectCenter = new Vector2(rect.Center.X, rect.Center.Y);
            ItemIcons.DrawGunToolTopDown(spriteBatch, pixel, rectCenter, rotation, HeldIconSize, item);
            return;
        }

        const int margin = 2;
        if (ItemIcons.HasIcon(item))
        {
            // Turns to point where the character is actually facing, rather than always reading the
            // same way on screen - the same angle GetHeldToolMuzzle uses, so a gun tool's texture and
            // its flame never drift out of alignment with each other.
            ItemIcons.Draw(spriteBatch, pixel, item, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2), rotation);
            return;
        }

        var pos = new Vector2(rect.Center.X, rect.Center.Y);
        spriteBatch.Draw(pixel, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2), InventoryPanel.ItemColor(item));
        var label = ItemDefinitions.ShortLabel(item);
        if (label.Length == 0)
            return;
        var textSize = font.MeasureString(label) * 0.4f;
        spriteBatch.DrawString(font, label, pos - textSize / 2f, Color.White, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
    }

    // A rounded top-down read (shoulders around a head, not a flat square stack) rather than a bare
    // helmet-on-a-box - no sprite sheet, but a hip capsule, a shoulder capsule and a head circle
    // built from the same rect+HudIcons.FillCircle rounding every tool icon already uses reads as an
    // actual person from this camera height. FacingX/Y offsets the shoulders/head forward and picks
    // which side the (unlit) hip capsule trails on, plus a small bright nose on the head, so a
    // standing-still character still visibly has a front and a back.
    internal void DrawCharacter(SpriteBatch spriteBatch, CharacterState character, Vector2 origin,
        (string Text, float Alpha)? chatBubble = null)
    {
        var size = (int)(CharacterDiameter * PixelsPerUnit);
        var center = new Vector2(origin.X + (float)character.X * PixelsPerUnit, origin.Y + (float)character.Y * PixelsPerUnit);
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);

        var facing = new Vector2(character.FacingX, character.FacingY);
        if (facing.LengthSquared() > 0.01f)
            facing.Normalize();
        else
            facing = new Vector2(1f, 0f); // idle characters still need a direction to hold a tool toward

        // Hired crew (World.Recruiting.cs) reads as a body of a different colour, not another
        // anonymous crewmate - the point of hiring one is knowing it's there and doing its job.
        var bodyColor = character.IsBot ? new Color(70, 110, 150) : new Color(196, 78, 44);
        // The accent is the shoulder patch on a uniform - the one place a crewman carries a colour
        // that is not the cloth itself.
        var accent = character.IsBot ? new Color(150, 200, 235) : new Color(226, 186, 70);
        // Standing, anchored at the feet, so the body goes up the screen from where the crewman
        // actually is. The world stays top-down and the person does not - the same mix SS13 and
        // Rimworld use, and the reason the figure finally has arms and legs you can see.
        _crewSkin.Draw(spriteBatch, new Vector2(center.X, center.Y + size * 0.30f),
            CharacterHeight * PixelsPerUnit, bodyColor, accent, character.WearingSuit, facing);

        DrawHeldItems(spriteBatch, _pixel, _font, HeldItemTypes(character.Inventory), center, facing);

        if (character.CarryingAmmoCrate)
        {
            const int crateSize = 8;
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - crateSize / 2, rect.Top - crateSize / 2, crateSize, crateSize), Color.SaddleBrown);
        }

        if (character.SuitActionRemaining > 0)
            spriteBatch.DrawString(_font, "...", new Vector2(rect.X, rect.Bottom + 2), Color.CadetBlue, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        if (chatBubble is { } bubble)
            DrawChatBubble(spriteBatch, bubble.Text, bubble.Alpha, new Vector2(center.X, rect.Y - 44));
    }

    // Every crew nameplate, in one later pass over the whole snapshot - deliberately NOT part of
    // DrawCharacter/DrawCharacters above. Those draw inside the lit scene batch (captured for
    // ScenePost, multiplied by the room-lighting/sight mask); a nameplate that shares that batch
    // goes dark right along with whatever real shadow the character happens to be standing in - a
    // once-subtle seam that the darker post-Barotrauma-pass PoweredFloor made an actual bug report
    // ("Eisenhorn (Капитан)" fading into a wall's own cast shadow). Direct user request: a
    // nameplate's whole job is identifying who this is, so it must read the same whether they are
    // standing in a lit room or a dark one - the caller draws this call site AFTER ScenePost.Present
    // (Game1.cs), once the lighting multiply has already been applied to everything else.
    public void DrawCharacterLabels(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, Matrix sceneTransform)
    {
        spriteBatch.Begin(transformMatrix: sceneTransform);
        // Same filter as DrawCharacters (ShipRenderer.cs) - an outside character's nameplate is
        // drawn by FieldRenderer instead, not here (see ShipRenderer.cs's own doc comment on why
        // this can't be left implicit).
        // Direct user request ("модель игрока полностью пропадала") - a floating nameplate over an
        // invisible body would look broken, so it goes with the body (ShipRenderer.cs's own
        // DrawCharacters doc comment).
        foreach (var character in snapshot.Characters.Where(c => !ShipLocalFrame.InFieldSpace(c) && c.Health > 0f))
            DrawCharacterLabel(spriteBatch, character, origin);
        spriteBatch.End();
    }

    private void DrawCharacterLabel(SpriteBatch spriteBatch, CharacterState character, Vector2 origin)
    {
        var size = (int)(CharacterDiameter * PixelsPerUnit);
        var center = new Vector2(origin.X + (float)character.X * PixelsPerUnit, origin.Y + (float)character.Y * PixelsPerUnit);
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);

        // The crew panel's role picker (CrewPanel.GetOwnRoleIconRect) is the only way a live
        // player's Role ever gets set - drawing the same glyph HudIcons.DrawRoleGlyph already
        // gives CrewPanel/InfoPanel rows here is what makes that choice visible in the ship view
        // itself, for a bot's fixed Role too since both read from the same field.
        //
        // Drawn before the nameplate below (not after, as this used to be) - a long name can extend
        // far enough to pass under this glyph's own position, and the plate the nameplate now has
        // needs to be the last thing painted there so it always wins, not whichever happened to be
        // drawn most recently.
        if (character.Role is { } headRole)
            HudIcons.DrawRoleGlyph(spriteBatch, _pixel, new Vector2(center.X, rect.Y - 26), 0.5f,
                character.IsBot ? Color.LightSkyBlue : Color.White, headRole);

        // A small dot for every other player, not just the speaker's own local HUD label
        // ("ГОВОРИТ"/"РАЦИЯ", Game1.cs) - direct user request so the crew can see who's transmitting
        // without everyone having to announce it in text chat first. Opposite side from the role
        // glyph so the two never sit on top of each other.
        if (character.IsSpeaking)
            HudIcons.FillCircle(spriteBatch, _pixel, new Vector2(center.X + 16, rect.Y - 26), 4f, Color.LightGreen);

        // A human crewmate reads the same way a hired bot does - name floating over the head,
        // always on, not just when hovered - so telling a crew of several players apart doesn't
        // depend on remembering whose colour is whose. Once they've picked their own Role from
        // CrewPanel, it's shown in the name label too, the same way a bot's already is above.
        //
        // Backed by the same opaque plate every other floating label in this file uses
        // (DrawLabelBacking) - previously bare text, so standing near a device (whose own label
        // already has a backing) or another crewmate produced two sets of glyphs painted straight
        // over each other, unreadable regardless of which one was technically drawn last.
        if (character.IsBot && character.Role is { } role)
        {
            var text = $"{character.BotName} ({CrewRoles.Name(role)})";
            var position = new Vector2(rect.X - 10, rect.Y - 14);
            DrawLabelBacking(spriteBatch, text, position, 0.45f);
            spriteBatch.DrawString(_font, text, position, Color.LightSkyBlue, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }
        else if (!character.IsBot && character.Nickname is { Length: > 0 } nickname)
        {
            var text = character.Role is { } playerRole ? $"{nickname} ({CrewRoles.Name(playerRole)})" : nickname;
            var position = new Vector2(rect.X - 10, rect.Y - 14);
            DrawLabelBacking(spriteBatch, text, position, 0.45f);
            spriteBatch.DrawString(_font, text, position, Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }
    }

    // A speech bubble above the sender (direct user request, "как в Баротравме", ChatBubbleTracker) -
    // positioned further above the head than the permanent nameplate/role glyph so the two never
    // overlap. A bubble is a brief announcement, not the full chat log, so long text is truncated.
    private void DrawChatBubble(SpriteBatch spriteBatch, string text, float alpha, Vector2 anchorBottomCenter)
    {
        const int maxChars = 40;
        if (text.Length > maxChars)
            text = text[..maxChars] + "…";

        const float scale = 0.45f;
        var size = _font.MeasureString(text) * scale;
        var padding = new Vector2(6, 4);
        var boxSize = size + padding * 2;
        var boxOrigin = new Vector2(anchorBottomCenter.X - boxSize.X / 2f, anchorBottomCenter.Y - boxSize.Y);

        spriteBatch.Draw(_pixel, new Rectangle((int)boxOrigin.X, (int)boxOrigin.Y, (int)boxSize.X, (int)boxSize.Y), Color.Black * (0.6f * alpha));
        spriteBatch.DrawString(_font, text, boxOrigin + padding, Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness) =>
        DrawRectOutline(spriteBatch, _pixel, rect, color, thickness);

    // Traces the reference art's own actual panel silhouette (visible up close in reactor.png) -
    // chamfered corners PLUS a stepped notch cut into the middle of each straight edge, not a plain
    // rectangle/octagon/rounded-rect. For a device whose face is already fully drawn by something
    // else (a room's own reference art) and only needs its own interactive footprint traced, not a
    // second housing drawn on top of the picture.
    private void DrawComplexReactorOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, float thickness)
    {
        var chamfer = Math.Max(3, Math.Min(rect.Width, rect.Height) / 6);
        var notchDepth = Math.Max(2, chamfer / 2);
        var notchWidth = Math.Max(4, chamfer);
        var cx = rect.Center.X;
        var cy = rect.Center.Y;

        Span<Vector2> vertices = stackalloc Vector2[]
        {
            // Top edge: left chamfer -> centred notch (dips down into the panel) -> right chamfer.
            new(rect.X + chamfer, rect.Y),
            new(cx - notchWidth / 2f, rect.Y), new(cx - notchWidth / 2f, rect.Y + notchDepth),
            new(cx + notchWidth / 2f, rect.Y + notchDepth), new(cx + notchWidth / 2f, rect.Y),
            new(rect.Right - chamfer, rect.Y),
            // Top-right chamfer, then right edge with its own centred notch (dips left).
            new(rect.Right, rect.Y + chamfer),
            new(rect.Right, cy - notchWidth / 2f), new(rect.Right - notchDepth, cy - notchWidth / 2f),
            new(rect.Right - notchDepth, cy + notchWidth / 2f), new(rect.Right, cy + notchWidth / 2f),
            new(rect.Right, rect.Bottom - chamfer),
            // Bottom-right chamfer, then bottom edge with its own centred notch (dips up).
            new(rect.Right - chamfer, rect.Bottom),
            new(cx + notchWidth / 2f, rect.Bottom), new(cx + notchWidth / 2f, rect.Bottom - notchDepth),
            new(cx - notchWidth / 2f, rect.Bottom - notchDepth), new(cx - notchWidth / 2f, rect.Bottom),
            new(rect.X + chamfer, rect.Bottom),
            // Bottom-left chamfer, then left edge with its own centred notch (dips right).
            new(rect.X, rect.Bottom - chamfer),
            new(rect.X, cy + notchWidth / 2f), new(rect.X + notchDepth, cy + notchWidth / 2f),
            new(rect.X + notchDepth, cy - notchWidth / 2f), new(rect.X, cy - notchWidth / 2f),
            new(rect.X, rect.Y + chamfer),
        };
        for (var i = 0; i < vertices.Length; i++)
            HudIcons.DrawLine(spriteBatch, _pixel, vertices[i], vertices[(i + 1) % vertices.Length], color, thickness);
    }

    internal static void DrawRectOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
