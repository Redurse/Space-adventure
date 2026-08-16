namespace SpaceAdventure.Shared.Model;

// One required connection in the base wiring topology (game_design.md section 1): either a
// "магистральный провод" (Distribution -> Junction) or a "провод-отвод" (Junction -> Device).
// This is static topology, not runtime state - see WireLinkState (Protocol) for what's actually
// damaged/backed-up right now.
public sealed record WireLink(string Id, string FromNodeId, string ToNodeId, PowerSystemId System);
