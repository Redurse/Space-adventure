namespace SpaceAdventure.Shared.Protocol;

// M62 - "строительство в полёте и в бою": a room under construction isn't part of Ship.Rooms yet
// (not walkable, not airtight, not powered - World.ShipBuilding.cs's own StepRoomBuilds only ever
// folds it into a real CustomShipDefinition once the timer completes), so it needs its own small
// snapshot record rather than riding along in the Rooms list. ShipRenderer draws it as a
// translucent "ghost" outline with a progress readout - the client-facing half of the plan's own
// "не проходима, не герметична, не запитана" description.
public sealed record PendingRoomBuildState(string Id, string Name, float X, float Y, float Width, float Height, float ProgressFraction);
