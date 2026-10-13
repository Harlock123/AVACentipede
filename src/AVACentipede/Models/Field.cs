namespace AVACentipede.Models;

/// <summary>A 2D point or vector (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>
/// The playfield: 30 columns by 32 rows of 8-pixel cells, portrait like the arcade screen. Mushrooms
/// sit on cells; the centipede moves from cell to cell. The bottom six rows are the player's zone.
/// </summary>
public static class Field
{
    public const int Cell = 8;
    public const int Cols = 30;
    public const int Rows = 32;
    public const int Width = Cols * Cell;
    public const int Height = Rows * Cell;
    /// <summary>The first row of the player's zone, where the shooter can move.</summary>
    public const int ZoneTop = 26;
    public const double Step = 1.0 / 60;

    public static int ColOf(double x) => (int)Math.Floor(x / Cell);
    public static int RowOf(double y) => (int)Math.Floor(y / Cell);
    public static double CenterX(int col) => col * Cell + Cell / 2.0;
    public static double CenterY(int row) => row * Cell + Cell / 2.0;
    public static bool InCols(int col) => col is >= 0 and < Cols;
    public static bool InRows(int row) => row is >= 0 and < Rows;
}
