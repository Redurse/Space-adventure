using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Shared.Networking;

// Sends a snapshot as "what changed since the last one I wrote on this socket". Most of a snapshot (the hull, the station, the asteroid field,
// the wiring, the star map) is the same bytes tick after tick, and it was most of the frame; here every large property is compared, as JSON bytes,
// with what this connection last wrote, and left out when identical, with its path listed in ServerMessage.Unchanged.
//
// Why this is safe where the old "send once per connection" was not (WorldSnapshot.cs's doc comment): the comparison is against the last frame
// actually WRITTEN to the stream (the writer thread, in order), and the reader (TcpClientConnection's read loop) rebuilds every frame in order before
// the "latest wins" mailbox ever sees it. A snapshot that is skipped on the way (the server ticking faster than the socket drains) was never "last
// written", so nothing is ever omitted relative to a frame the client did not get. TCP keeps the order; a new connection starts with a new encoder
// and sends everything.
//
// One level of nesting is understood (a Station/Wiring/Field/EnemyShip snapshot is itself compared property by property), because those composite
// records change every tick in their small dynamic parts while their big parts stay the same.
internal static class SnapshotDeltaShape
{
    // Properties shorter than this are cheaper to resend than to name.
    public const int MinOmitBytes = 64;

    public static readonly HashSet<Type> NestedTypes = new()
    {
        typeof(StationSnapshot), typeof(WiringSnapshot), typeof(AsteroidFieldSnapshot), typeof(EnemyShipSnapshot),
    };

    // Big lists the server builds once and then hands out unchanged until something really changes (a new array each time): while the very
    // same object comes round again it is known to be the same bytes, and need not be serialised just to find that out. (4000 star
    // summaries are half a megabyte of JSON; doing that every tick only to compare it was the largest cost of a frame.)
    public static readonly HashSet<string> ReplacedWhenChanged = new() { "StarSystems", "WarLanes", "WarNodes", "WarMovers", "WarFleets" };

    private static readonly Dictionary<Type, PropertyInfo[]> Cache = new();

    public static PropertyInfo[] PropertiesOf(Type type)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(type, out var properties))
                Cache[type] = properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
                    .ToArray();
            return properties;
        }
    }
}

public sealed class SnapshotDeltaEncoder
{
    private readonly Dictionary<string, byte[]> _last = new();
    private readonly Dictionary<string, object> _lastObjects = new();

    // The whole ServerMessage frame body (UTF-8 JSON) for a snapshot, with unchanged big properties left out.
    public byte[] Encode(WorldSnapshot snapshot)
    {
        var unchanged = new List<string>();
        using var stream = new MemoryStream(32 * 1024);
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("Kind", (int)ServerMessageKind.Snapshot);
            writer.WritePropertyName("Snapshot");
            WriteObject(writer, snapshot, string.Empty, unchanged);
            writer.WritePropertyName("Unchanged");
            writer.WriteStartArray();
            foreach (var path in unchanged)
                writer.WriteStringValue(path);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private void WriteObject(Utf8JsonWriter writer, object value, string path, List<string> unchanged)
    {
        writer.WriteStartObject();
        foreach (var property in SnapshotDeltaShape.PropertiesOf(value.GetType()))
        {
            var propertyValue = property.GetValue(value);
            var name = path + property.Name;
            if (propertyValue is null)
            {
                writer.WriteNull(property.Name);
                continue;
            }

            if (SnapshotDeltaShape.NestedTypes.Contains(property.PropertyType))
            {
                writer.WritePropertyName(property.Name);
                WriteObject(writer, propertyValue, name + ".", unchanged);
                continue;
            }

            if (property.PropertyType.IsValueType)
            {
                writer.WritePropertyName(property.Name);
                JsonSerializer.Serialize(writer, propertyValue, property.PropertyType, Wire.Options);
                continue;
            }

            var byReference = path.Length == 0 && SnapshotDeltaShape.ReplacedWhenChanged.Contains(property.Name);
            if (byReference && _lastObjects.TryGetValue(name, out var lastObject) && ReferenceEquals(lastObject, propertyValue)
                && _last.TryGetValue(name, out var lastBytes) && lastBytes.Length >= SnapshotDeltaShape.MinOmitBytes)
            {
                unchanged.Add(name);
                continue;
            }
            var bytes = JsonSerializer.SerializeToUtf8Bytes(propertyValue, property.PropertyType, Wire.Options);
            if (byReference)
                _lastObjects[name] = propertyValue;
            if (bytes.Length >= SnapshotDeltaShape.MinOmitBytes && _last.TryGetValue(name, out var previous) && previous.AsSpan().SequenceEqual(bytes))
            {
                unchanged.Add(name);
                continue;
            }
            _last[name] = bytes;
            writer.WritePropertyName(property.Name);
            writer.WriteRawValue(bytes, skipInputValidation: true);
        }
        writer.WriteEndObject();
    }
}

public sealed class SnapshotDeltaDecoder
{
    private WorldSnapshot? _last;

    // Fills the omitted properties of an incoming snapshot from the previous full one. Call for every frame, in arrival order.
    public WorldSnapshot Decode(WorldSnapshot partial, IReadOnlyList<string>? unchanged)
    {
        if (unchanged is { Count: > 0 })
        {
            if (_last is null)
                throw new InvalidDataException("кадр ссылается на предыдущее состояние, которого нет");
            foreach (var path in unchanged)
                Copy(partial, _last, path.Split('.'), 0);
        }
        _last = partial;
        return partial;
    }

    private static void Copy(object target, object source, string[] path, int index)
    {
        var property = target.GetType().GetProperty(path[index])
            ?? throw new InvalidDataException($"неизвестное свойство {path[index]}");
        if (index < path.Length - 1)
        {
            var nestedTarget = property.GetValue(target);
            var nestedSource = property.GetValue(source);
            if (nestedTarget is null || nestedSource is null)
                throw new InvalidDataException($"нет вложенного состояния {path[index]}");
            Copy(nestedTarget, nestedSource, path, index + 1);
            return;
        }
        property.SetValue(target, property.GetValue(source));
    }
}
