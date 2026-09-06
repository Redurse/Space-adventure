namespace Anabiosis.Shared.Model;

// The navigation console's own toggle switch (World.Scanner.cs, M48 follow-up - "переключением
// рычажка... либо лучевой либо круговой"): Directional is the original narrow cone at full range,
// Circular trades range for coverage - it pulses all the way around instead of along one bearing,
// at half World.Scanner.cs's own ScannerRangeUnits.
public enum ScannerMode
{
    Directional,
    Circular,
}
