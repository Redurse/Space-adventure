namespace Anabiosis.Shared.Model;

// The fog of war over one system's local field (direct user request: "я хочу сделать туман войны... и
// рассеивается при сканировании сонаром"): a coarse square grid of explored / unexplored cells. Coarse on
// purpose - 128x128 over a field several thousand units wide is 2 KB as a bitset, cheap to keep per
// system, to save, and to send; the client blurs it into a soft edge when drawing (FogOverlay).
// A cell is revealed once and stays revealed.
public sealed class FogGrid
{
    public const int Size = 128;

    private readonly byte[] _bits;

    public float FieldSize { get; }
    public float CellSize => FieldSize / Size;

    public FogGrid(float fieldSize, byte[]? bits = null)
    {
        FieldSize = fieldSize;
        _bits = bits is { Length: Size * Size / 8 } ? (byte[])bits.Clone() : new byte[Size * Size / 8];
    }

    /// A copy of the bitset (row-major, bit x of row y = cell (x, y), least significant bit first).
    public byte[] ToBits() => (byte[])_bits.Clone();

    public static bool IsRevealed(byte[] bits, int cellX, int cellY) =>
        cellX >= 0 && cellY >= 0 && cellX < Size && cellY < Size && (bits[(cellY * Size + cellX) >> 3] & (1 << (cellX & 7))) != 0;

    public bool IsRevealed(Vec2 position) =>
        IsRevealed(_bits, (int)Math.Floor(position.X / CellSize), (int)Math.Floor(position.Y / CellSize));

    /// Reveals every cell whose centre is within <paramref name="radius"/>. True if anything changed.
    public bool RevealCircle(Vec2 center, float radius) => Reveal(center, radius, _ => true);

    /// Reveals the cells inside a cone from <paramref name="origin"/> (the sonar's sweep). True if anything changed.
    public bool RevealCone(Vec2 origin, float bearingDegrees, float halfAngleDegrees, float range)
    {
        // The cone's own first cell is always the ship's - without this a narrow cone starts out
        // thinner than one cell and reveals nothing for the first few hundred units.
        var slack = CellSize * 0.75f;
        return Reveal(origin, range, toCell =>
        {
            var distance = toCell.Length();
            if (distance < slack)
                return true;
            var bearing = Math.Atan2(toCell.Y, toCell.X) * (180.0 / Math.PI);
            var off = Math.Abs(((bearing - bearingDegrees) % 360.0 + 540.0) % 360.0 - 180.0);
            // widened by the angle one cell subtends at this distance, so the cone has no stair-step gaps
            return off <= halfAngleDegrees + Math.Atan2(slack, distance) * (180.0 / Math.PI);
        });
    }

    private bool Reveal(Vec2 center, float radius, Func<Vec2, bool> accept)
    {
        var cell = CellSize;
        var minX = Math.Max(0, (int)Math.Floor((center.X - radius) / cell));
        var maxX = Math.Min(Size - 1, (int)Math.Floor((center.X + radius) / cell));
        var minY = Math.Max(0, (int)Math.Floor((center.Y - radius) / cell));
        var maxY = Math.Min(Size - 1, (int)Math.Floor((center.Y + radius) / cell));
        var changed = false;
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var index = (y * Size + x) >> 3;
            var mask = (byte)(1 << (x & 7));
            if ((_bits[index] & mask) != 0)
                continue;
            var toCell = new Vec2((x + 0.5) * cell, (y + 0.5) * cell) - center;
            if (toCell.Length() > radius || !accept(toCell))
                continue;
            _bits[index] |= mask;
            changed = true;
        }
        return changed;
    }
}
