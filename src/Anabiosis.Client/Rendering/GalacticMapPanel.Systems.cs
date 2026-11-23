using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;
using Anabiosis.Shared.War;

namespace Anabiosis.Client.Rendering;

// The hyperlanes, the star systems with what stands in them, the ships travelling the lanes, and the names.
public sealed partial class GalacticMapPanel
{
    // From this zoom on a system shows what is built in it, not just who holds it; from this one its fleet as well.
    private const float IconZoom = 0.3f;
    private const float FleetZoom = 0.55f;

    private static readonly Color StarColor = new(255, 238, 205);
    private static readonly Color FreeLaneColor = new(70, 88, 118);

    private bool[] _reachable = Array.Empty<bool>();
    private IReadOnlyList<WarMoverState>? _moversShown;
    private float _moversSeenAt;

    private bool InView(Vector2 point, float margin) =>
        point.X >= _view.X - margin && point.X <= _view.Right + margin && point.Y >= _view.Y - margin && point.Y <= _view.Bottom + margin;

    private static Vector2 Center(StarSystemSummary system, Vector2 origin, float scale) =>
        origin + new Vector2(system.GalaxyX, system.GalaxyY) * scale;

    private static float Hash01(int a, int b)
    {
        unchecked
        {
            var n = a * 374761393 + b * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
        }
    }

    // ---- hyperlanes: each half in the colour of the realm it starts in ----

    private void DrawLanes(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, float scale, bool hasWar)
    {
        if (!hasWar || snapshot.WarLanes is not { } lanes)
            return;
        var systems = snapshot.StarSystems;
        var nodes = snapshot.WarNodes!;
        // Thin and quiet, as in Stellaris: a free lane is a grey hair, a realm lane is a little brighter in its colour.
        var thickness = scale < 0.9f ? 1f : 1.4f;
        foreach (var lane in lanes)
        {
            var a = Center(systems[lane.A], origin, scale);
            var b = Center(systems[lane.B], origin, scale);
            if (!InView(a, 80f) && !InView(b, 80f))
                continue;
            var middle = (a + b) / 2f;
            var ownerA = nodes[lane.A].Owner;
            var ownerB = nodes[lane.B].Owner;
            var colorA = ownerA == 0 ? FreeLaneColor * 0.38f : Color.Lerp(OwnerColor(ownerA), Color.White, 0.35f) * 0.62f;
            var colorB = ownerB == 0 ? FreeLaneColor * 0.38f : Color.Lerp(OwnerColor(ownerB), Color.White, 0.35f) * 0.62f;
            DrawSoftLine(spriteBatch, a, middle, colorA, thickness * 2.4f);
            DrawSoftLine(spriteBatch, middle, b, colorB, thickness * 2.4f);

            // Traffic: a small light slowly runs along every lane that belongs to someone, one way or the other.
            if (scale >= 0.22f && (ownerA != 0 || ownerB != 0) && Vector2.DistanceSquared(a, b) > 40f * 40f)
            {
                var phase = (_time * 0.05f + Hash01(lane.A, lane.B)) % 1f;
                if (Hash01(lane.B, lane.A) < 0.5f)
                    phase = 1f - phase;
                var spot = Vector2.Lerp(a, b, phase);
                var owner = ownerA != 0 ? ownerA : ownerB;
                var light = Color.Lerp(OwnerColor(owner), Color.White, 0.5f);
                spriteBatch.Draw(_glow, spot, null, MapArt.Additive(light, 0.75f), 0f, new Vector2(64f, 64f), 7f / 128f * 2f, SpriteEffects.None, 0f);
            }
        }
    }

    // How far the ship own jump reaches from where it is.
    private void DrawJumpRange(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, float zoom, float scale)
    {
        var systems = snapshot.StarSystems;
        _reachable = new bool[systems.Count];
        var currentIndex = -1;
        for (var i = 0; i < systems.Count; i++)
            if (systems[i].Id == snapshot.CurrentSystemId)
                currentIndex = i;
        if (currentIndex < 0)
            return;
        for (var i = 0; i < systems.Count; i++)
            _reachable[i] = i != currentIndex && Distance(systems[i], systems[currentIndex]) <= GalaxyMap.WarpJumpRadius;

        var center = Center(systems[currentIndex], origin, scale);
        var radius = GalaxyMap.WarpJumpRadius * scale;
        const int dashes = 72;
        for (var i = 0; i < dashes; i++)
            HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius, i * 360f / dashes, (i + 0.5f) * 360f / dashes, new Color(90, 190, 200) * 0.45f, 4, 1.2f);
    }

    // ---- the systems ----

    private void DrawSystems(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, float zoom, float scale, bool hasWar)
    {
        var systems = snapshot.StarSystems;
        var radius = Math.Clamp(1.4f + zoom * 2.4f, 1.8f, 4.4f);
        for (var i = 0; i < systems.Count; i++)
        {
            var system = systems[i];
            var center = Center(system, origin, scale);
            if (!InView(center, 40f))
                continue;

            var node = hasWar ? snapshot.WarNodes![i] : null;
            var owner = node?.Owner ?? (byte)0;
            var isCurrent = system.Id == snapshot.CurrentSystemId;
            var isSelected = system.Id == SelectedSystemId;

            // The star: a small bright dot in a faint light. A held one takes a little of its realm colour; the realm itself
            // is shown by the territory, not by rings.
            var ownTint = StarTint(i);
            var starColor = owner != 0 ? Color.Lerp(ownTint, OwnerColor(owner), 0.22f) : ownTint;
            var starRadius = radius * (0.85f + 0.4f * Hash01(i, 5));
            spriteBatch.Draw(_glow, center, null, MapArt.Additive(starColor, owner != 0 ? 0.6f : 0.38f), 0f, new Vector2(64f, 64f), starRadius * 4.6f / 128f * 2f, SpriteEffects.None, 0f);
            HudIcons.FillCircle(spriteBatch, _pixel, center, starRadius, Color.Lerp(starColor, Color.White, 0.45f));

            if (hasWar && node is not null && zoom >= IconZoom)
                DrawInstallations(spriteBatch, i, node, center, radius, zoom);
            if (hasWar && node is not null)
                DrawBattleMarks(spriteBatch, node, center, radius, zoom);

            if (snapshot.CampaignTargetSystemId is { } objectiveSystem && system.Id == objectiveSystem)
            {
                // The place the story wants the crew at: a slow pulsing blue ring with four ticks.
                var pulse = 0.5f + 0.5f * MathF.Sin(_time * 3f);
                HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius + 16f + 3f * pulse, 0f, 360f, new Color(120, 200, 255) * (0.9f - 0.3f * pulse), 36, 1.8f);
                for (var k = 0; k < 4; k++)
                {
                    var angle = k * MathF.PI / 2f;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    HudIcons.DrawLine(spriteBatch, _pixel, center + direction * (radius + 22f), center + direction * (radius + 29f), new Color(170, 225, 255), 2f);
                }
            }
            if (i < _reachable.Length && _reachable[i])
                HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius + 8f, 0f, 360f, Color.Gold * 0.55f, 24, 1.2f);

            if (isCurrent)
                DrawYouAreHere(spriteBatch, center, radius);
            if (isSelected)
                DrawSelection(spriteBatch, center, radius + 12f);
            else if (i == _hover)
                HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius + 10f, 0f, 360f, Color.White * 0.5f, 28, 1.2f);
        }
    }

    // Where the player ship is: a pulsing green ring and a small arrowhead above the star.
    private void DrawYouAreHere(SpriteBatch spriteBatch, Vector2 center, float radius)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin(_time * 3f);
        var green = new Color(96, 236, 130);
        HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius + 6f + 3f * pulse, 0f, 360f, green * (0.9f - 0.4f * pulse), 32, 1.6f);
        var tip = center + new Vector2(0f, -(radius + 8f));
        Primitives.FillPolygon(spriteBatch, _pixel, tip, new[] { tip, tip + new Vector2(-5f, -9f), tip + new Vector2(5f, -9f) }, green);
    }

    // The selected system: four brackets turning slowly round it.
    private void DrawSelection(SpriteBatch spriteBatch, Vector2 center, float radius)
    {
        var turn = _time * 0.6f;
        for (var k = 0; k < 4; k++)
        {
            var from = turn + k * MathF.PI / 2f - 0.32f;
            HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius, from * 180f / MathF.PI, (from + 0.64f) * 180f / MathF.PI, Color.White * 0.95f, 6, 1.8f);
        }
    }

    // ---- ships on the lanes ----

    private void DrawMovers(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, float scale)
    {
        if (snapshot.WarMovers is not { } movers || snapshot.WarClock is not { } clock)
            return;
        var systems = snapshot.StarSystems;

        // The list is rebuilt a few times a second; between rebuilds each ship is carried along its lane at its own speed.
        if (!ReferenceEquals(movers, _moversShown))
        {
            _moversShown = movers;
            _moversSeenAt = _time;
        }
        var elapsed = clock.SpeedLevel > 0 ? Math.Clamp(_time - _moversSeenAt, 0f, 0.6f) : 0f;
        var showStanding = scale >= 0.4f;

        foreach (var ship in movers)
        {
            if (ship.From < 0 || ship.From >= systems.Count || ship.To < 0 || ship.To >= systems.Count)
                continue;
            var from = Center(systems[ship.From], origin, scale);
            var color = OwnerColor(ship.Owner);
            var isOutposter = ship.Class == (byte)ShipClass.Outposter;

            if (ship.From == ship.To)
            {
                if (!showStanding || !InView(from, 20f))
                    continue;
                var spot = from + new Vector2(MathF.Cos(ship.Id * 2.4f), MathF.Sin(ship.Id * 2.4f)) * (9f + Hash01(ship.Id, 5) * 8f);
                spriteBatch.Draw(_pixel, new Rectangle((int)spot.X - 1, (int)spot.Y - 1, 2, 2), color * 0.7f);
                continue;
            }

            var to = Center(systems[ship.To], origin, scale);
            var fraction = Math.Clamp(ship.Fraction + elapsed * clock.SpeedLevel * ship.Speed / Math.Max(1f, ship.LaneLength), 0f, 1f);
            var position = Vector2.Lerp(from, to, fraction);
            if (!InView(position, 20f))
                continue;
            var direction = to - from;
            if (direction.LengthSquared() > 0.01f)
                direction.Normalize();

            var trail = Math.Min(Vector2.Distance(from, position), isOutposter ? 22f : 16f);
            HudIcons.DrawLine(spriteBatch, _pixel, position - direction * trail, position, color * 0.55f, 1.4f);
            var side = new Vector2(-direction.Y, direction.X);
            var size = isOutposter ? 4.6f : 3.4f;
            if (isOutposter)
                Primitives.FillPolygon(spriteBatch, _pixel, position, new[] { position + direction * size, position - direction * size + side * size * 0.8f, position - direction * size - side * size * 0.8f }, Color.Lerp(color, Color.White, 0.55f));
            else
                Primitives.FillPolygon(spriteBatch, _pixel, position, new[] { position + direction * size, position + side * size * 0.6f, position - direction * size, position - side * size * 0.6f }, Color.Lerp(color, Color.White, 0.35f));
        }
    }

    // ---- warships ----

    private IReadOnlyList<WarFleetState>? _fleetsShown;
    private float _fleetsSeenAt;

    // Fleets: a chevron with its size. Standing ones sit beside their star, the rest are carried along the lane like movers.
    private void DrawFleets(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, float scale, float zoom)
    {
        if (snapshot.WarFleets is not { } fleets || snapshot.WarClock is not { } clock)
            return;
        var systems = snapshot.StarSystems;
        if (!ReferenceEquals(fleets, _fleetsShown))
        {
            _fleetsShown = fleets;
            _fleetsSeenAt = _time;
        }
        var elapsed = clock.SpeedLevel > 0 ? Math.Clamp(_time - _fleetsSeenAt, 0f, 0.6f) : 0f;

        // Fleets standing in one system are one mark per realm, with their supply added up: Stellaris stacks them too.
        var standing = new Dictionary<(int Node, byte Owner), (int Supply, bool Fighting, byte Task)>();
        foreach (var fleet in fleets)
        {
            if (fleet.From < 0 || fleet.From >= systems.Count || fleet.To < 0 || fleet.To >= systems.Count)
                continue;
            if (fleet.From == fleet.To)
            {
                var key = (fleet.From, fleet.Owner);
                standing.TryGetValue(key, out var sum);
                standing[key] = (sum.Supply + fleet.Supply, sum.Fighting || fleet.Fighting, Math.Max(sum.Task, fleet.Task));
                continue;
            }
            var color = OwnerColor(fleet.Owner);
            var from = Center(systems[fleet.From], origin, scale);
            var size = Math.Clamp(3.6f + MathF.Log10(Math.Max(1, fleet.Supply)) * 2.2f, 4f, 11f);
            Vector2 position;
            Vector2 direction;
            {
                var to = Center(systems[fleet.To], origin, scale);
                var fraction = Math.Clamp(fleet.Fraction + elapsed * clock.SpeedLevel * fleet.Speed / Math.Max(1f, fleet.LaneLength), 0f, 1f);
                position = Vector2.Lerp(from, to, fraction);
                direction = to - from;
                if (direction.LengthSquared() > 0.01f)
                    direction.Normalize();
                var trail = Math.Min(Vector2.Distance(from, position), 26f);
                HudIcons.DrawLine(spriteBatch, _pixel, position - direction * trail, position, color * 0.7f, 2f);
            }
            if (!InView(position, 30f))
                continue;

            var side = new Vector2(-direction.Y, direction.X);
            var hull = new[] { position + direction * size, position - direction * size * 0.8f + side * size * 0.8f, position - direction * size * 0.3f, position - direction * size * 0.8f - side * size * 0.8f };
            var fill = Color.Lerp(color, Color.White, 0.25f);
            if (fleet.Fighting)
                fill = Color.Lerp(fill, new Color(255, 120, 90), 0.5f + 0.5f * MathF.Sin(_time * 10f));
            Primitives.FillPolygon(spriteBatch, _pixel, position, hull, fill);
            Primitives.StrokePolygon(spriteBatch, _pixel, hull, Color.Black * 0.7f, 1f);
            // Withdrawing and striking fleets are marked, so the war can be read at a glance.
            if (fleet.Task == 2)
                HudIcons.DrawRingArc(spriteBatch, _pixel, position, size + 3f, 0f, 360f, new Color(120, 200, 255) * 0.8f, 16, 1f);
            else if (fleet.Task == 3 || fleet.Task == 4)
                HudIcons.DrawRingArc(spriteBatch, _pixel, position, size + 3f, 0f, 360f, new Color(255, 110, 90) * 0.8f, 16, 1f);
            if (zoom >= FleetZoom || fleet.Supply >= 100)
                spriteBatch.DrawString(_labelFont, fleet.Supply.ToString(), position + new Vector2(size + 3f, -5f), Color.White * 0.9f, 0f, Vector2.Zero, _smallLabelScale, SpriteEffects.None, 0f);
        }

        foreach (var ((node, owner), group) in standing)
        {
            // The Union stands to the right of its star and the virus to the left, so two fleets in one system do not overlap.
            var position = Center(systems[node], origin, scale) + new Vector2(owner == 2 ? -15f : 15f, 11f);
            if (!InView(position, 30f))
                continue;
            var color = OwnerColor(owner);
            var size = Math.Clamp(3.6f + MathF.Log10(Math.Max(1, group.Supply)) * 2.2f, 4f, 11f);
            var fill = Color.Lerp(color, Color.White, 0.25f);
            if (group.Fighting)
                fill = Color.Lerp(fill, new Color(255, 120, 90), 0.5f + 0.5f * MathF.Sin(_time * 10f));
            DrawChevron(spriteBatch, position, new Vector2(0f, -1f), size, fill);
            if (group.Task == 2)
                HudIcons.DrawRingArc(spriteBatch, _pixel, position, size + 3f, 0f, 360f, new Color(120, 200, 255) * 0.8f, 16, 1f);
            else if (group.Task == 3 || group.Task == 4)
                HudIcons.DrawRingArc(spriteBatch, _pixel, position, size + 3f, 0f, 360f, new Color(255, 110, 90) * 0.8f, 16, 1f);
            if (zoom >= FleetZoom || group.Supply >= 100)
                spriteBatch.DrawString(_labelFont, group.Supply.ToString(), position + new Vector2(size + 3f, -5f), Color.White * 0.9f, 0f, Vector2.Zero, _smallLabelScale, SpriteEffects.None, 0f);
        }
    }

    private WarMotherState? _motherShown;
    private float _motherSeenAt;

    // The mother ship: a big golden hull in a halo, carried along its lane between updates, with its armour as an arc.
    private void DrawMother(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, float scale)
    {
        if (snapshot.WarMother is not { Alive: true } mother || snapshot.WarClock is not { } clock)
            return;
        var systems = snapshot.StarSystems;
        if (mother.From < 0 || mother.From >= systems.Count || mother.To < 0 || mother.To >= systems.Count)
            return;
        if (!ReferenceEquals(mother, _motherShown))
        {
            _motherShown = mother;
            _motherSeenAt = _time;
        }
        var from = Center(systems[mother.From], origin, scale);
        var position = from;
        var direction = new Vector2(1f, 0f);
        if (mother.From != mother.To)
        {
            var to = Center(systems[mother.To], origin, scale);
            var elapsed = clock.SpeedLevel > 0 ? Math.Clamp(_time - _motherSeenAt, 0f, 0.6f) : 0f;
            var fraction = Math.Clamp(mother.Fraction + elapsed * clock.SpeedLevel * mother.Speed / Math.Max(1f, mother.LaneLength), 0f, 1f);
            position = Vector2.Lerp(from, to, fraction);
            direction = to - from;
            if (direction.LengthSquared() > 0.01f)
                direction.Normalize();
        }
        else
        {
            position += new Vector2(0f, -26f);
        }
        if (!InView(position, 40f))
            return;

        var gold = new Color(255, 210, 90);
        var pulse = 0.5f + 0.5f * MathF.Sin(_time * 2.2f);
        spriteBatch.Draw(_glow, position, null, MapArt.Additive(gold, 0.45f + 0.2f * pulse), 0f, new Vector2(64f, 64f), 42f / 128f * 2f, SpriteEffects.None, 0f);
        if (mother.Fighting)
            spriteBatch.Draw(_glow, position, null, MapArt.Additive(new Color(255, 80, 60), 0.7f), 0f, new Vector2(64f, 64f), 60f / 128f * 2f, SpriteEffects.None, 0f);
        var side = new Vector2(-direction.Y, direction.X);
        const float length = 13f;
        const float width = 5f;
        var hull = new[]
        {
            position + direction * length,
            position + direction * length * 0.3f + side * width,
            position - direction * length * 0.9f + side * width * 0.8f,
            position - direction * length,
            position - direction * length * 0.9f - side * width * 0.8f,
            position + direction * length * 0.3f - side * width,
        };
        Primitives.FillPolygon(spriteBatch, _pixel, position, hull, Color.Lerp(gold, Color.White, 0.35f));
        Primitives.StrokePolygon(spriteBatch, _pixel, hull, Color.Black * 0.8f, 1.2f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, position, 17f, -90f, -90f + 360f * mother.Hp / 100f, mother.Hp > 50 ? new Color(120, 230, 150) : new Color(255, 120, 90), 28, 1.8f);
        spriteBatch.DrawString(_labelFont, "МАТКА", position + new Vector2(20f, -7f) + new Vector2(1f, 1f), Color.Black * 0.8f, 0f, Vector2.Zero, _labelScale, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_labelFont, "МАТКА", position + new Vector2(20f, -7f), gold, 0f, Vector2.Zero, _labelScale, SpriteEffects.None, 0f);
    }

    // A fight in the system: red sparks around the star. Fortifications worn down and a system being taken show as arcs.
    private void DrawBattleMarks(SpriteBatch spriteBatch, WarNodeState node, Vector2 center, float radius, float zoom)
    {
        if (node.Battle)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(_time * 9f);
            spriteBatch.Draw(_glow, center, null, MapArt.Additive(new Color(255, 80, 60), 0.5f + 0.4f * pulse), 0f, new Vector2(64f, 64f), (radius * 9f + 6f * pulse) / 128f * 2f, SpriteEffects.None, 0f);
            for (var k = 0; k < 4; k++)
            {
                var angle = _time * 4f + k * MathF.PI / 2f;
                var spark = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius + 12f + 3f * pulse);
                HudIcons.DrawLine(spriteBatch, _pixel, spark, spark + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4f, new Color(255, 200, 140), 1.5f);
            }
        }
        // Worn fortifications are only worth a mark when zoomed in, or when more than half is gone.
        if (node.Owner != 0 && node.Fort < 100 && (zoom >= 0.6f || node.Fort < 50 || node.Battle))
        {
            // What is left of the fortifications, as an arc that shrinks.
            var sweep = 360f * node.Fort / 100f;
            HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius + 17f, -90f, -90f + sweep, new Color(120, 230, 150) * 0.85f, 24, 1.6f);
        }
        if (node.Occupation > 0)
            HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius + 20f, -90f, -90f + 360f * node.Occupation / 100f, new Color(255, 190, 70), 24, 2f);
    }

    // ---- names ----

    private void DrawSystemLabels(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, float zoom, float scale, bool hasWar)
    {
        var systems = snapshot.StarSystems;
        var placed = new List<Rectangle>();
        // The names that matter most go first, so they win the room when two collide.
        var order = Enumerable.Range(0, systems.Count)
            .OrderByDescending(i => systems[i].Id == snapshot.CurrentSystemId ? 4 : systems[i].Id == SelectedSystemId ? 3 : i == _hover ? 2 : hasWar && snapshot.WarNodes![i].Planet != 0 ? 1 : 0);
        foreach (var i in order)
        {
            var system = systems[i];
            var isCurrent = system.Id == snapshot.CurrentSystemId;
            var isSelected = system.Id == SelectedSystemId;
            var node = hasWar ? snapshot.WarNodes![i] : null;
            var always = isCurrent || isSelected || i == _hover;
            var important = always || (node is { Planet: not 0 } && zoom >= 0.38f);
            // Names fade in as the map is zoomed in, so they do not pop: colonies first, then every system.
            var fade = always ? 1f : important ? MapArt.Smooth(0.38f, 0.48f, zoom) : MapArt.Smooth(0.5f, 0.62f, zoom);
            if (fade < 0.03f)
                continue;
            var center = Center(system, origin, scale);
            if (!InView(center, 10f))
                continue;

            var text = isCurrent ? system.Name + "  (вы здесь)" : system.Name;
            var size = _labelFont.MeasureString(text) * _labelScale;
            var position = new Vector2(center.X - size.X / 2f, center.Y + 14f);
            var rect = new Rectangle((int)position.X - 2, (int)position.Y - 1, (int)size.X + 4, (int)size.Y + 2);
            for (var attempt = 0; attempt < 5 && placed.Any(r => r.Intersects(rect)); attempt++)
            {
                position.Y += size.Y + 2f;
                rect.Y += (int)size.Y + 2;
            }
            if (!important && placed.Any(r => r.Intersects(rect)))
                continue;
            placed.Add(rect);

            var color = isCurrent ? new Color(120, 240, 150) : node is { Owner: not 0 } ? Color.Lerp(OwnerColor(node.Owner), Color.White, 0.55f) : new Color(188, 200, 218);
            spriteBatch.DrawString(_labelFont, text, position + new Vector2(1f, 1f), Color.Black * (0.8f * fade), 0f, Vector2.Zero, _labelScale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_labelFont, text, position, color * fade, 0f, Vector2.Zero, _labelScale, SpriteEffects.None, 0f);
        }
    }
}
