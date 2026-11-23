using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The galactic map, drawn the way Stellaris draws its galaxy, and worked the same way: a dark nebula-lit sky, star systems
// joined by hyperlanes, the realms coloured over the sky with soft fills and bright borders, ships moving along the lanes,
// a bar across the top with the date and the speed of time, and a panel for whichever system is selected. It is a full
// takeover of the screen, opened from anywhere with M. The war (Anabiosis.Shared/War) is what it shows: who holds which
// system, what stands there, and the freighters and outposters on their way. The player can still jump from here:
// select a system within reach and press the jump button.
//
// Split by what it draws: the sky (.Backdrop), the realms (.Territory), lanes and systems and ships (.Systems),
// the bar, the system panel and the legend (.Hud).
public sealed partial class GalacticMapPanel
{
    public const float PixelsPerUnit = 1.4f;
    public const float MinZoom = 0.03f; // far enough out to take in a galaxy of four thousand stars at once
    public const float MaxZoom = 3f;
    // How close, in pixels, the pointer has to be to a system to pick it.
    private const float PickRadius = 13f;
    private const float TopBarHeight = 34f;

    private readonly GraphicsDevice _device;
    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private readonly Rectangle _view;
    private readonly Texture2D _glow;
    private readonly Texture2D _flare;
    private readonly Texture2D _softCircle;
    private readonly Texture2D _softLine;

    // A thin antialiased line: the soft strip stretched and turned along it, `width` across in all (the solid core is a third).
    private void DrawSoftLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color, float width)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length < 0.3f)
            return;
        spriteBatch.Draw(_softLine, from, null, color, MathF.Atan2(delta.Y, delta.X), new Vector2(0f, 8f), new Vector2(length / 4f, width / 16f), SpriteEffects.None, 0f);
    }

    // What was picked with a click, and what the pointer is over right now (set again every Draw).
    public string? SelectedSystemId { get; private set; }
    private int _hover = -1;
    private Vector2? _pointer;
    private float _time;

    // The labels on the map (names of systems, fleet sizes) and the realm titles have fonts of their own, baked at the size they
    // are drawn at so they stay sharp; with none given the debug font is scaled down to stand in for them.
    private readonly SpriteFont _labelFont;
    private readonly SpriteFont _titleFont;
    private readonly float _labelScale;
    private readonly float _smallLabelScale;
    private readonly float _titleUnit;

    public GalacticMapPanel(GraphicsDevice graphicsDevice, SpriteFont font, Rectangle view, SpriteFont? labelFont = null, SpriteFont? titleFont = null)
    {
        _device = graphicsDevice;
        _font = font;
        _labelFont = labelFont ?? font;
        _titleFont = titleFont ?? font;
        _labelScale = labelFont is null ? 0.46f : 1f;
        _smallLabelScale = labelFont is null ? 0.4f : 0.85f;
        _titleUnit = titleFont is null ? 1f : 0.55f;
        _view = view;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _glow = MapArt.Glow(graphicsDevice);
        _flare = MapArt.Flare(graphicsDevice);
        _softCircle = HudIcons.CreateSoftCircleTexture(graphicsDevice);
        _softLine = MapArt.SoftLine(graphicsDevice);
        BuildBackdrop();
    }

    // Where the map's origin (the top-left corner of the box around every system) lands on screen.
    public static Vector2 ComputeOrigin(Vector2 panelOrigin, IReadOnlyList<StarSystemSummary> systems, float zoom, Vector2 panOffset)
    {
        if (systems.Count == 0)
            return panelOrigin + panOffset;

        var minX = systems.Min(s => s.GalaxyX);
        var minY = systems.Min(s => s.GalaxyY);
        return panelOrigin + panOffset - new Vector2(minX, minY) * PixelsPerUnit * zoom;
    }

    public static Vector2 NodeCenter(StarSystemSummary system, Vector2 mapOrigin, float zoom) =>
        mapOrigin + new Vector2(system.GalaxyX, system.GalaxyY) * PixelsPerUnit * zoom;

    // The zoom and pan that put the whole galaxy on screen, centred in the area below the top bar.
    public (float Zoom, Vector2 Pan) FitView(IReadOnlyList<StarSystemSummary> systems)
    {
        if (systems.Count == 0)
            return (1f, Vector2.Zero);
        var minX = systems.Min(s => s.GalaxyX);
        var maxX = systems.Max(s => s.GalaxyX);
        var minY = systems.Min(s => s.GalaxyY);
        var maxY = systems.Max(s => s.GalaxyY);
        var width = Math.Max(1f, maxX - minX);
        var height = Math.Max(1f, maxY - minY);
        var zoom = Math.Clamp(MathF.Min((_view.Width - 40f) / (width * PixelsPerUnit), (_view.Height - TopBarHeight - 30f) / (height * PixelsPerUnit)), MinZoom, MaxZoom);
        var areaCenter = new Vector2(_view.Center.X, _view.Y + TopBarHeight + (_view.Height - TopBarHeight) / 2f);
        var boxCenter = new Vector2(minX + width / 2f, minY + height / 2f) * PixelsPerUnit * zoom;
        var origin = areaCenter - boxCenter;
        return (zoom, origin - new Vector2(_view.X, _view.Y) + new Vector2(minX, minY) * PixelsPerUnit * zoom);
    }

    // Zooms about the point under the pointer, the way a map should: that point stays where it is.
    public void ZoomAt(IReadOnlyList<StarSystemSummary> systems, Vector2 panelOrigin, ref float zoom, ref Vector2 pan, Vector2 pointer, float wheelSteps)
    {
        var newZoom = Math.Clamp(zoom * MathF.Pow(1.1f, wheelSteps), MinZoom, MaxZoom);
        if (systems.Count == 0 || MathF.Abs(newZoom - zoom) < 1e-5f)
        {
            zoom = newZoom;
            return;
        }
        var origin = ComputeOrigin(panelOrigin, systems, zoom, pan);
        var under = (pointer - origin) / (PixelsPerUnit * zoom);
        var minX = systems.Min(s => s.GalaxyX);
        var minY = systems.Min(s => s.GalaxyY);
        var newOrigin = pointer - under * PixelsPerUnit * newZoom;
        pan = newOrigin - panelOrigin + new Vector2(minX, minY) * PixelsPerUnit * newZoom;
        zoom = newZoom;
    }

    // What a click on the map did, for the game to act on.
    public readonly record struct MapClick(bool Consumed, string? WarpToSystemId, int? SetWarSpeed);

    public MapClick HandleClick(WorldSnapshot snapshot, Vector2 mouse, Vector2 panelOrigin, float zoom, Vector2 panOffset)
    {
        if (!_view.Contains(new Point((int)mouse.X, (int)mouse.Y)))
            return new MapClick(false, null, null);

        for (var i = 0; i < SpeedLevels.Length; i++)
            if (SpeedButtonRect(i).Contains(new Point((int)mouse.X, (int)mouse.Y)))
                return new MapClick(true, null, SpeedLevels[i]);

        if (SelectedSystemId is { } selectedId && JumpButtonRect().Contains(new Point((int)mouse.X, (int)mouse.Y))
            && snapshot.StarSystems.FirstOrDefault(s => s.Id == selectedId) is { } target && CanJumpTo(snapshot, target))
            return new MapClick(true, target.Id, null);

        if (InfoPanelRect().Contains(new Point((int)mouse.X, (int)mouse.Y)) && SelectedSystemId is not null)
            return new MapClick(true, null, null);
        if (mouse.Y < _view.Y + TopBarHeight)
            return new MapClick(true, null, null);

        var origin = ComputeOrigin(panelOrigin, snapshot.StarSystems, zoom, panOffset);
        var picked = PickSystem(snapshot.StarSystems, origin, zoom, mouse);
        SelectedSystemId = picked >= 0 ? snapshot.StarSystems[picked].Id : null;
        return new MapClick(true, null, null);
    }

    private static int PickSystem(IReadOnlyList<StarSystemSummary> systems, Vector2 origin, float zoom, Vector2 point)
    {
        var best = -1;
        var bestDistance = PickRadius;
        for (var i = 0; i < systems.Count; i++)
        {
            var distance = Vector2.Distance(NodeCenter(systems[i], origin, zoom), point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    private static float Distance(StarSystemSummary a, StarSystemSummary b) =>
        MathF.Sqrt(MathF.Pow(a.GalaxyX - b.GalaxyX, 2) + MathF.Pow(a.GalaxyY - b.GalaxyY, 2));

    // Set by the game: the jump film, whose mark rides on this map while it plays.
    public JumpAnimation? Jump { get; set; }

    private static bool CanJumpTo(WorldSnapshot snapshot, StarSystemSummary target)
    {
        var current = snapshot.StarSystems.FirstOrDefault(s => s.Id == snapshot.CurrentSystemId);
        return current is not null && target.Id != current.Id && Distance(target, current) <= GalaxyMap.WarpJumpRadius
            && snapshot.CanWarpNow && snapshot.WarpCooldownRemaining <= 0f && snapshot.HyperiumAboard >= GalaxyMap.HyperiumCostForDistance(Distance(target, current));
    }

    // The war as the snapshot carries it, or null while there is none (the tutorial, a test world).
    private static bool HasWar(WorldSnapshot snapshot) =>
        snapshot.WarNodes is { } nodes && nodes.Count == snapshot.StarSystems.Count && snapshot.WarClock is not null;

    private void DrawScene(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, float zoom, Vector2 panOffset,
        float totalSeconds = 0f, Vector2? pointer = null)
    {
        var systems = snapshot.StarSystems;
        _pointer = pointer;
        _time = totalSeconds;
        var origin = ComputeOrigin(panelOrigin, systems, zoom, panOffset);
        var scale = PixelsPerUnit * zoom;
        var hasWar = HasWar(snapshot);
        _hover = pointer is { } p && _view.Contains(new Point((int)p.X, (int)p.Y)) ? PickSystem(systems, origin, zoom, p) : -1;

        DrawBackdrop(spriteBatch, panOffset);
        if (systems.Count > 0)
            DrawHaze(spriteBatch, systems, origin, scale);
        if (hasWar)
            DrawTerritory(spriteBatch, systems, snapshot.WarNodes!, origin, scale);
        DrawLanes(spriteBatch, snapshot, origin, scale, hasWar);
        DrawJumpRange(spriteBatch, snapshot, origin, zoom, scale);
        DrawSystems(spriteBatch, snapshot, origin, zoom, scale, hasWar);
        Jump?.DrawOnMap(spriteBatch, _pixel, snapshot, system => NodeCenter(system, origin, zoom));
        if (hasWar)
        {
            DrawMovers(spriteBatch, snapshot, origin, scale);
            DrawFleets(spriteBatch, snapshot, origin, scale, zoom);
            DrawMother(spriteBatch, snapshot, origin, scale);
            DrawRealmNames(spriteBatch, snapshot, origin, zoom, scale);
        }
        DrawSystemLabels(spriteBatch, snapshot, origin, zoom, scale, hasWar);
        DrawHud(spriteBatch, snapshot, hasWar);
    }
}
