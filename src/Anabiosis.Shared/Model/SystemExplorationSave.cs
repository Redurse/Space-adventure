namespace Anabiosis.Shared.Model;

// One system's exploration as stored in a save (World.Exploration.cs): the fog bitset (FogGrid.cs) and
// which hidden places are sonar signals, identified, or already looted. The places themselves are not
// stored - they are re-rolled from SaveGame.WorldSeed, which gives the same ones back.
public sealed record SystemExplorationSave(
    string SystemId, byte[] FogBits, IReadOnlyList<string> SignalIds, IReadOnlyList<string> IdentifiedIds, IReadOnlyList<string> LootedIds,
    // Salvage already carried off a wreck (World.Wrecks.cs). Null in a save from before wrecks existed.
    IReadOnlyList<string>? TakenLootIds = null);
