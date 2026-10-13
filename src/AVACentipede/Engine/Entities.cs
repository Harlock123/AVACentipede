using AVACentipede.Models;

namespace AVACentipede.Engine;

/// <summary>
/// One segment of a centipede. Every segment follows the same rules (along the row; at a wall or a
/// mushroom, down a row and turn), so a chain that starts in line stays in line; shoot one and the
/// segment behind it becomes a head. Positions are whole pixels so the grid checks are exact.
/// </summary>
public sealed class Segment
{
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>+1 heading right, −1 left.</summary>
    public int Dir { get; set; } = 1;
    /// <summary>+1 working down the screen, −1 back up (once it has reached the bottom).</summary>
    public int VDir { get; set; } = 1;
    public bool Dropping { get; set; }
    public int TargetY { get; set; }
    /// <summary>Poisoned: diving straight down to the bottom.</summary>
    public bool Diving { get; set; }
    /// <summary>The segment ahead of this one (null: this one is a head).</summary>
    public Segment? Prev { get; set; }
    public bool Dead { get; set; }
    public bool IsHead => Prev is null || Prev.Dead;
    public int Col => Field.ColOf(X);
    public int Row => Field.RowOf(Y);
}

public sealed class Flea
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Speed { get; set; } = 130;
    public int Hits { get; set; }
    public int LastRow { get; set; } = -1;
    public bool Dead { get; set; }
}

public sealed class Spider
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public double Clock { get; set; }
    public bool Dead { get; set; }
}

public sealed class Scorpion
{
    public double X { get; set; }
    public double Y { get; set; }
    public int Dir { get; set; } = 1;
    public double Speed { get; set; } = 50;
    public bool Dead { get; set; }
}

public enum EffectKind { Burst, Score }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 0.4;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>
/// The controls for one step: a direction (keys or stick), or a point to move towards (the mouse,
/// like the arcade's trackball), and fire (held fires again as soon as the shot is spent).
/// </summary>
public record struct Controls(int Dx, int Dy, bool Fire, Vec? Aim = null);
