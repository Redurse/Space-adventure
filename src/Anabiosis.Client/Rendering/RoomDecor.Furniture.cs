using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// A single small prop per compartment, tucked into whichever corner the real gameplay blocks
// (turrets, consoles, breaker panels) tend to leave alone - furniture that's beaten to the punch
// by an actual fixture just draws underneath it (DrawRoomFloor runs before those), which is a
// safe failure: a partly-hidden chair, never a torn-looking one. Matched on the same id
// substrings RoomDecor.Accent already keys its colour off, so a room's furniture and its paint
// always agree on what kind of compartment it is.
public static partial class RoomDecor
{
    public static void DrawFurniture(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, string roomId, Color accent)
    {
        var corner = new Vector2(rect.Right - 34, rect.Bottom - 30);
        switch (roomId)
        {
            case var id when id.Contains("cockpit") || id.Contains("bridge"):
                DrawChair(spriteBatch, pixel, corner, accent);
                break;
            // Shipwright's office folded in here too (M49 follow-up) - a hull-fitting workshop
            // reads the same as an armory's tool bench.
            case var id when id.Contains("armory") || id.Contains("weapon") || id.Contains("shipwright") ||
                             id.Contains("foundry") || id.Contains("drydock") || id.Contains("outfitting") ||
                             id.Contains("fitting") || id.Contains("refinery") || id.Contains("salvage"):
                DrawWorkbench(spriteBatch, pixel, corner, accent);
                break;
            case var id when id.Contains("reactor") || id.Contains("engine"):
                DrawPipeRun(spriteBatch, pixel, rect, accent);
                break;
            case var id when id.Contains("quarters") || id.Contains("crew") || id.Contains("bunkroom") || id.Contains("barracks"):
                DrawBunk(spriteBatch, pixel, corner, accent);
                break;
            // Greenhouse rides along here too - the same "small rack of tanks/plants" silhouette
            // reads as either racked oxygen tanks or racked plant trays.
            case var id when id.Contains("life") || id.Contains("oxygen") || id.Contains("med") || id.Contains("greenhouse"):
                DrawTankRack(spriteBatch, pixel, corner, accent);
                break;
            case var id when id.Contains("cargo") || id.Contains("storage") || id.Contains("hold") ||
                             id.Contains("warehouse") || id.Contains("vault") || id.Contains("munitions"):
                DrawCrateStack(spriteBatch, pixel, corner, accent);
                break;
            case var id when id.Contains("shield") || id.Contains("command"):
                DrawCapacitorBank(spriteBatch, pixel, corner, accent);
                break;
            // Everything below is new for M49's procedural stations (Station.Procedural.cs) - room
            // flavors that have no ship-side equivalent to piggyback on, so they never matched
            // anything above and drew no furniture at all.
            case var id when id.Contains("trade") || id.Contains("cantina") || id.Contains("lounge") || id.Contains("brokerage"):
                DrawMarketStall(spriteBatch, pixel, corner, accent);
                break;
            case var id when id.Contains("administrator") || id.Contains("recruiting") || id.Contains("archive"):
                DrawOfficeDesk(spriteBatch, pixel, corner, accent);
                break;
            case var id when id.Contains("laboratory") || id.Contains("observatory"):
                DrawScienceBench(spriteBatch, pixel, corner, accent);
                break;
            case var id when id.Contains("security") || id.Contains("training") || id.Contains("brig") || id.Contains("radar"):
                DrawSecurityRack(spriteBatch, pixel, corner, accent);
                break;
        }
    }

    // A seat with a backrest, angled slightly - reads as "someone sits here" without needing a
    // whole console model.
    private static void DrawChair(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, Color accent)
    {
        var seat = new Rectangle((int)corner.X - 12, (int)corner.Y, 22, 16);
        var back = new Rectangle(seat.X - 2, seat.Y - 16, 6, 18);
        spriteBatch.Draw(pixel, seat, new Color(52, 56, 64));
        spriteBatch.Draw(pixel, back, new Color(52, 56, 64));
        spriteBatch.Draw(pixel, new Rectangle(back.X, back.Y, back.Width, 3), accent * 0.7f);
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, seat, Color.Black * 0.35f, 1);
    }

    // A table with a few small tool silhouettes laid on it.
    private static void DrawWorkbench(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, Color accent)
    {
        var bench = new Rectangle((int)corner.X - 24, (int)corner.Y - 4, 32, 18);
        spriteBatch.Draw(pixel, bench, new Color(58, 54, 48));
        spriteBatch.Draw(pixel, new Rectangle(bench.X, bench.Y, bench.Width, 3), accent * 0.65f);
        spriteBatch.Draw(pixel, new Rectangle(bench.X, bench.Bottom - 4, 4, 4), Color.Black * 0.5f);
        spriteBatch.Draw(pixel, new Rectangle(bench.Right - 4, bench.Bottom - 4, 4, 4), Color.Black * 0.5f);
        spriteBatch.Draw(pixel, new Rectangle(bench.X + 6, bench.Y - 3, 10, 3), new Color(150, 150, 155));
        spriteBatch.Draw(pixel, new Rectangle(bench.X + 19, bench.Y - 2, 6, 2), accent);
    }

    // A vertical service pipe with rivets, plus a vent grate - the kind of exposed plumbing a
    // reactor deck actually runs.
    private static void DrawPipeRun(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent)
    {
        var pipe = new Rectangle(rect.X + 10, rect.Y + 14, 8, rect.Height - 28);
        spriteBatch.Draw(pixel, pipe, new Color(60, 64, 70));
        spriteBatch.Draw(pixel, new Rectangle(pipe.X, pipe.Y, 2, pipe.Height), Color.White * 0.12f);
        for (var y = pipe.Y + 6; y < pipe.Bottom - 4; y += 22)
            spriteBatch.Draw(pixel, new Rectangle(pipe.X - 1, y, pipe.Width + 2, 3), accent * 0.5f);

        var vent = new Rectangle(rect.Right - 30, rect.Y + 12, 18, 24);
        spriteBatch.Draw(pixel, vent, new Color(40, 42, 47));
        for (var y = vent.Y + 3; y < vent.Bottom - 2; y += 5)
            spriteBatch.Draw(pixel, new Rectangle(vent.X + 2, y, vent.Width - 4, 2), Color.Black * 0.5f);
    }

    // Stacked bunks - two beds, one over the other.
    private static void DrawBunk(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, Color accent)
    {
        var bunk = new Rectangle((int)corner.X - 30, (int)corner.Y - 20, 34, 36);
        spriteBatch.Draw(pixel, new Rectangle(bunk.X, bunk.Y, bunk.Width, 3), new Color(80, 84, 92));
        spriteBatch.Draw(pixel, new Rectangle(bunk.X, bunk.Y, 3, bunk.Height), new Color(80, 84, 92));
        spriteBatch.Draw(pixel, new Rectangle(bunk.X + 3, bunk.Y + 4, bunk.Width - 6, 12), Color.Lerp(accent, Color.White, 0.3f) * 0.7f);
        spriteBatch.Draw(pixel, new Rectangle(bunk.X + 3, bunk.Y + 20, bunk.Width - 6, 12), Color.Lerp(accent, Color.White, 0.3f) * 0.7f);
        spriteBatch.Draw(pixel, new Rectangle(bunk.X + 3, bunk.Y + 15, bunk.Width - 6, 2), Color.Black * 0.35f);
    }

    // A small rack of tanks/plants - life support's own equipment, standing in for both the
    // med-bay and greenhouse readings that id substring covers.
    private static void DrawTankRack(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, Color accent)
    {
        for (var i = 0; i < 3; i++)
        {
            var tank = new Rectangle((int)corner.X - 28 + i * 10, (int)corner.Y - 4, 7, 22);
            spriteBatch.Draw(pixel, tank, new Color(70, 78, 74));
            spriteBatch.Draw(pixel, new Rectangle(tank.X, tank.Y, tank.Width, 4), Color.Lerp(accent, Color.White, 0.4f) * 0.8f);
        }
    }

    // A short stack of crates - the same silhouette StationRenderer's own crates use, at half
    // their size since this is background dressing, not something to click.
    private static void DrawCrateStack(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, Color accent)
    {
        var bottom = new Rectangle((int)corner.X - 26, (int)corner.Y, 24, 20);
        var top = new Rectangle((int)corner.X - 20, (int)corner.Y - 16, 16, 15);
        foreach (var box in new[] { bottom, top })
        {
            spriteBatch.Draw(pixel, box, new Color(96, 74, 50));
            ShipRenderer.DrawRectOutline(spriteBatch, pixel, box, accent * 0.6f, 1);
        }
    }

    // A bank of small glowing capacitor cells - the shield bay's own hardware, distinct from the
    // reactor hall's pipework. Doubles for a command centre's own bank of status electronics.
    private static void DrawCapacitorBank(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, Color accent)
    {
        var housing = new Rectangle((int)corner.X - 30, (int)corner.Y - 8, 30, 26);
        spriteBatch.Draw(pixel, housing, new Color(46, 52, 58));
        for (var i = 0; i < 3; i++)
        {
            var cell = new Rectangle(housing.X + 3 + i * 9, housing.Y + 4, 6, housing.Height - 8);
            spriteBatch.Draw(pixel, cell, Color.Black * 0.4f);
            spriteBatch.Draw(pixel, new Rectangle(cell.X, cell.Bottom - 6, cell.Width, 6), Color.Lerp(accent, Color.White, 0.3f) * 0.8f);
        }
    }

    // A shop counter with a signboard and a row of goods laid out on top - trade/cantina/lounge/
    // brokerage's own furniture (M49 follow-up), none of which had a ship-side equivalent to
    // piggyback on.
    private static void DrawMarketStall(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, Color accent)
    {
        var counter = new Rectangle((int)corner.X - 34, (int)corner.Y - 4, 34, 20);
        spriteBatch.Draw(pixel, counter, new Color(72, 60, 46));
        spriteBatch.Draw(pixel, new Rectangle(counter.X, counter.Y - 6, counter.Width, 6), accent * 0.75f);
        for (var i = 0; i < 4; i++)
            spriteBatch.Draw(pixel, new Rectangle(counter.X + 3 + i * 8, counter.Y + 3, 5, 5), Color.Lerp(accent, Color.White, 0.4f) * 0.8f);
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, counter, Color.Black * 0.35f, 1);
    }

    // A desk with a monitor propped on it - administrator/recruiting/archive's own furniture.
    private static void DrawOfficeDesk(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, Color accent)
    {
        var desk = new Rectangle((int)corner.X - 30, (int)corner.Y, 30, 14);
        spriteBatch.Draw(pixel, desk, new Color(60, 58, 62));
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, desk, Color.Black * 0.35f, 1);
        var monitor = new Rectangle(desk.X + 4, desk.Y - 14, 14, 12);
        spriteBatch.Draw(pixel, monitor, new Color(30, 32, 36));
        spriteBatch.Draw(pixel, new Rectangle(monitor.X + 2, monitor.Y + 2, monitor.Width - 4, monitor.Height - 4), accent * 0.7f);
    }

    // A bench with a row of glowing sample vials - laboratory/observatory's own furniture.
    private static void DrawScienceBench(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, Color accent)
    {
        var bench = new Rectangle((int)corner.X - 32, (int)corner.Y - 2, 32, 16);
        spriteBatch.Draw(pixel, bench, new Color(50, 54, 58));
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, bench, Color.Black * 0.35f, 1);
        for (var i = 0; i < 3; i++)
        {
            var vial = new Rectangle(bench.X + 4 + i * 9, bench.Y - 10, 5, 10);
            spriteBatch.Draw(pixel, vial, Color.Lerp(accent, Color.White, 0.3f) * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(vial.X, vial.Y, vial.Width, 3), Color.White * 0.5f);
        }
    }

    // A wall rack of holstered rifles - security/training/brig/radar's own furniture.
    private static void DrawSecurityRack(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, Color accent)
    {
        var rack = new Rectangle((int)corner.X - 30, (int)corner.Y - 22, 30, 30);
        spriteBatch.Draw(pixel, rack, new Color(42, 44, 48));
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, rack, accent * 0.6f, 1);
        for (var i = 0; i < 3; i++)
            spriteBatch.Draw(pixel, new Rectangle(rack.X + 4 + i * 8, rack.Y + 3, 3, rack.Height - 6), new Color(30, 30, 32));
    }
}
