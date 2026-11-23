namespace Anabiosis.Shared.Model;

// Outposts: the stations called "Аванпост ...", "Форпост ..." in GalaxyMap. Their layout is the ordinary procedural one - the author puts the
// finished outposts together out of the outpost compartments in the catalog (CompartmentType.Outpost) - but every one of them has a shipwright,
// so a ship can be built (and a compartment taken down) at any outpost.
public sealed partial class Station
{
    public static bool IsOutpostPoint(GalaxyPoint point) =>
        point.Kind == GalaxyPointKind.Station
        && (point.Name.Contains("аванпост", StringComparison.OrdinalIgnoreCase) || point.Name.Contains("форпост", StringComparison.OrdinalIgnoreCase));

    // The one place that builds the station of a point, so the server and the map's schematic always agree.
    // The start station is the drawn outpost (Station.OutpostStart.cs); every other station is the procedural ring.
    public static Station CreateFor(GalaxyPoint point, Vec2 connectorAnchor, EdgeSide airlockSide = EdgeSide.Right) =>
        point.Id == StartStationPointId
            ? CreateOutpostStart(point.Id, connectorAnchor, airlockSide)
            : CreateProcedural(point.Id, point.StationKind, connectorAnchor, airlockSide, shipwrightOnHand: IsOutpostPoint(point));
}
