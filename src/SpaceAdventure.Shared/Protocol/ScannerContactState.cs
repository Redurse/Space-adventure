using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// One persistent NPC hull a particular player's own scanner sweep has actually picked up
// (World.Scanner.cs, M44) - frozen at wherever it was standing the moment it was last swept, not
// its live position, the same "last known point" a real passive sensor would report rather than a
// live radar. Attached to that player's own CharacterState (ScannerContacts) rather than broadcast
// globally - two crew scanning the same system can have found different hulls, or the same hull at
// different stale positions, and each should only ever see their own results
// (game_design.md/M44 - "показывает их на экране самого учёного").
public sealed record ScannerContactState(
    string Id,
    NpcShipKind Kind,
    FactionId FactionId,
    float X,
    float Y,
    float RotationDegrees,
    long DetectedAtTick);
