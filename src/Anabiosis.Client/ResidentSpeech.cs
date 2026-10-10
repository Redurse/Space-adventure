using System.Linq;
using System;
using System.Collections.Generic;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// What the walking station residents say: a line floats over their head when you click on one, and now
// and then one greets you as you pass. Entirely client-side - it is flavour, so nothing about it needs the
// server (or the save) to know. The bubbles go through StationRenderer / ShipRenderer.DrawChatBubble, the
// same speech bubble the crew's chat already uses.
public sealed class ResidentSpeech
{
    private const double BubbleSeconds = 4.0;
    private const double FadeSeconds = 0.6;
    private const double GreetRadius = 2.4;
    private const double GreetCooldownSeconds = 45.0;
    private const double GreetChance = 0.4;

    private static readonly string[] CivilianLines =
    {
        "Добрый день, пилот.",
        "Говорят, на дальних поясах опять неспокойно.",
        "Не видели моего кота? Серый такой, полосатый.",
        "Топливо опять подорожало...",
        "Хорошая у вас посудина.",
        "Я тут уже третий год, привык.",
        "Тише, у стен есть уши.",
        "Если ищете работу - загляните к администратору.",
        "Слышали про предтеч? Говорят, это всё сказки.",
        "У нас тут спокойно. Пока что.",
        "Механик здесь - золотые руки.",
        "Не толкайтесь, тут и так тесно.",
        "На станции вода дороже топлива, представляете?",
        "Вы с корабля? Привезли что-нибудь интересное?",
    };

    private static readonly string[] GuardLines =
    {
        "Не нарушайте порядок.",
        "Проходите, не задерживайтесь.",
        "Тут всё под контролем.",
        "Воровство на станции карается. Помните об этом.",
        "Идите своей дорогой, пилот.",
        "Патруль. Всё спокойно.",
    };

    private readonly Dictionary<string, (string Text, double Until)> _lines = new();
    private readonly Dictionary<string, double> _nextGreetAt = new();
    private readonly Dictionary<string, string> _lastLine = new();
    private readonly Random _random = new();
    private double _now;

    // The speech bubbles to draw right now, keyed by resident id: the text and how opaque it still is.
    public IReadOnlyDictionary<string, (string Text, float Alpha)> Bubbles()
    {
        var result = new Dictionary<string, (string, float)>();
        foreach (var (id, line) in _lines)
        {
            var remaining = line.Until - _now;
            if (remaining <= 0)
                continue;
            result[id] = (line.Text, (float)Math.Clamp(remaining / FadeSeconds, 0.0, 1.0));
        }
        return result;
    }

    // Called every frame: keeps the clock and lets residents greet whoever walks up to them.
    public void Update(WorldSnapshot? snapshot, CharacterState? me, double nowSeconds)
    {
        _now = nowSeconds;
        if (_lines.Count > 0)
            foreach (var id in _lines.Where(kv => kv.Value.Until < _now - 1.0).Select(kv => kv.Key).ToList())
                _lines.Remove(id);

        if (snapshot?.Station.Residents is not { } residents || me is not { OnStation: true, Health: > 0f })
            return;
        var mine = new Vec2(me.X, me.Y);
        foreach (var resident in residents)
        {
            if ((new Vec2(resident.X, resident.Y) - mine).Length() > GreetRadius)
                continue;
            if (_lines.TryGetValue(resident.Id, out var current) && current.Until > _now)
                continue; // already talking
            if (_nextGreetAt.TryGetValue(resident.Id, out var next) && next > _now)
                continue;
            // Roll once per approach, then stay quiet for a while whether or not they spoke up.
            _nextGreetAt[resident.Id] = _now + GreetCooldownSeconds;
            if (_random.NextDouble() < GreetChance)
                Say(resident);
        }
    }

    public void Say(StationResidentState resident)
    {
        var pool = resident.Role == ResidentRole.Guard ? GuardLines : CivilianLines;
        var line = pool[_random.Next(pool.Length)];
        if (_lastLine.TryGetValue(resident.Id, out var previous) && previous == line)
            line = pool[(Array.IndexOf(pool, line) + 1) % pool.Length]; // never the same line twice running
        _lastLine[resident.Id] = line;
        _lines[resident.Id] = (line, _now + BubbleSeconds);
    }
}
