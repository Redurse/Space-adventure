using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Door open/close (game_design.md Phase 3, M16) — click any door to toggle it, no proximity
// re-check server-side (same trusted-client convention as WireLinkInteractId/PowerSystemIndex:
// the client only ever sends this when it detected a nearby click). Ids are unique across Doors
// and AirlockOuterDoors, so one dictionary covers both.
public sealed partial class World
{
    // Populated in World's constructor (World.cs) from the real Ship instance, not a field
    // initializer here - partial-class field initializer ordering across files isn't guaranteed,
    // and Ship itself is what this depends on.
    private readonly Dictionary<string, bool> _doorOpen = new();

    public bool IsDoorOpen(string doorId) => _doorOpen.TryGetValue(doorId, out var open) && open;

    public void ToggleDoor(string doorId)
    {
        if (_doorOpen.ContainsKey(doorId))
            _doorOpen[doorId] = !_doorOpen[doorId];
    }

    private IReadOnlyList<DoorState> CreateDoorStates() =>
        _doorOpen.Select(kv => new DoorState(kv.Key, kv.Value)).ToArray();
}
