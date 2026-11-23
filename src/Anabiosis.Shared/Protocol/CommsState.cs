namespace Anabiosis.Shared.Protocol;

// The ship's radio link with a station (World.Comms.cs), the way Cosmoteer's comms work: a station in range is hailed from the comms console's map and, once the line is
// open, its services - trade, crew hire, the job board - work without docking. null LinkedPointId = no line open.
public sealed record CommsState(
    string? LinkedPointId,
    string? LinkedName,
    // What the station on the line offers (a living trader, recruiter, administrator on it).
    bool HasTrader,
    bool HasRecruiter,
    bool HasAdministrator,
    // How far a station can be and still be hailed, in field units.
    float Range,
    // The last word from the other end ("HAIL REJECTED" and the like), shown for a few seconds.
    string? Notice);
