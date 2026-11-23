using System;
using Microsoft.Xna.Framework;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// The purchasable wiring parts: all small electronics of one family, so one shared template - a
// green circuit board with gold contacts and a black chip - told apart by the part's short code
// silk-screened on the chip and a category colour (gates blue, timing purple, relay gold, sensors
// teal with a sensor dome, actuators orange).
public static partial class ItemSkin
{
    private static void LogicPart(ComponentKind kind)
    {
        var (accent, sensor) = kind switch
        {
            ComponentKind.GateAnd or ComponentKind.GateOr or ComponentKind.GateNot or ComponentKind.GateXor
                or ComponentKind.Comparator or ComponentKind.SignalCheck or ComponentKind.Math or ComponentKind.Function or ComponentKind.Regex => (new Color(110, 160, 255), false),
            ComponentKind.Timer or ComponentKind.Memory or ComponentKind.Delay or ComponentKind.Oscillator => (new Color(190, 130, 250), false),
            ComponentKind.Relay or ComponentKind.Button or ComponentKind.Keyboard => (new Color(250, 206, 80), false),
            ComponentKind.OxygenSensor or ComponentKind.BreachSensor or ComponentKind.PowerLossSensor or ComponentKind.MotionSensor
                => (new Color(90, 220, 200), true),
            _ => (new Color(250, 150, 60), false),
        };

        Box(4f, 6f, 24f, 20f, new Mat(new Color(40, 112, 66), 0.35f, 0.05f), 1.6f);       // board
        for (var y = 9f; y <= 23f; y += 3.5f)
        {
            Box(2.2f, y, 3f, 1.6f, Brass, 0.3f);                                           // edge contacts
            Box(26.8f, y, 3f, 1.6f, Brass, 0.3f);
        }
        Line(6f, 8f, 12f, 8f, new Color(150, 210, 150), 0.5f);                            // traces
        Line(20f, 24f, 26f, 24f, new Color(150, 210, 150), 0.5f);
        Box(8.5f, 10f, 15f, sensor ? 8f : 11f, Blackened, 0.8f);                          // chip
        Solid(8.5f, 10f, 1.4f, 1.4f, accent);                                             // pin-1 mark
        DrawCode(ComponentDefinitions.ShortLabel(kind), 16f, sensor ? 12f : 13.5f, accent);
        if (sensor)
        {
            Ball(16f, 22f, 3.4f, new Mat(new Color(220, 236, 240), 0.9f, 0f));            // sensor dome
            Glow(16f, 22f, 3f, accent, 0.5f);
        }
        else
            Dot(24.6f, 23f, accent, 1f, 0.9f);                                             // status LED
    }

    // A tiny silk-screen font, 5 rows high - just the letters the parts' short codes use. Most are
    // 3 wide; M and W need 5 to not read as H and U.
    private static readonly System.Collections.Generic.Dictionary<char, string> Glyphs = new()
    {
        ['A'] = "010101111101101", ['N'] = "10011101101110011001", ['D'] = "110101101101110", ['O'] = "111101101101111",
        ['R'] = "110101110101101", ['T'] = "111010010010010", ['X'] = "101101010101101", ['I'] = "111010010010111",
        ['E'] = "111100110100111", ['L'] = "100100100100111", ['2'] = "111001111100111",
        ['S'] = "111100111001111", ['B'] = "110101110101110", ['P'] = "110101110100100",
        ['M'] = "1000111011101011000110001", ['W'] = "1000110001101011101110001",
        ['F'] = "111100110100100", ['Z'] = "111001010100111", ['C'] = "111100100100111", ['U'] = "101101101101111",
    };

    private static void DrawCode(string code, float cx, float top, Color color)
    {
        var columns = 0;
        foreach (var ch in code)
            columns += (Glyphs.TryGetValue(ch, out var g) ? g.Length / 5 : 3) + 1;
        columns -= 1;
        var cell = MathF.Min(1.15f, 14f / columns);
        var x = cx - columns * cell / 2f;
        foreach (var ch in code)
        {
            if (!Glyphs.TryGetValue(ch, out var bits))
            {
                x += 4 * cell;
                continue;
            }
            var w = bits.Length / 5;
            for (var row = 0; row < 5; row++)
            for (var col = 0; col < w; col++)
                if (bits[row * w + col] == '1')
                    Solid(x + col * cell, top + row * cell, cell, cell, color);
            x += (w + 1) * cell;
        }
    }
}
