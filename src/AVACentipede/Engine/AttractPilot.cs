using AVACentipede.Models;

namespace AVACentipede.Engine;

/// <summary>
/// The computer player for attract mode and the tests: it slides under the lowest segment and keeps
/// firing, and dodges the spider, the flea and any segment that gets close.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private double _wobble;

    public Controls Decide(GameEngine g)
    {
        if (!g.ShooterAlive) return default;
        double x = g.ShooterX, y = g.ShooterY;
        _wobble += Field.Step;

        // Danger at a spot: segments close by, the spider, a flea falling in that column.
        double Danger(double px, double py)
        {
            double d = 0;
            foreach (var s in g.Segments)
            {
                double gap = Math.Abs(s.X - px) + Math.Abs(s.Y - py);
                if (gap < 28) d += 28 - gap;
            }
            if (g.Spider is { Dead: false } sp)
            {
                double gap = Math.Abs(sp.X - px) + Math.Abs(sp.Y - py);
                if (gap < 44) d += (44 - gap) * 1.5;
            }
            if (g.Flea is { Dead: false } f && Math.Abs(f.X - px) < 10 && f.Y < py) d += 30;
            return d;
        }

        // Target: under the lowest segment, else the spider's column, else the middle.
        var target = g.Segments.OrderByDescending(s => s.Y).FirstOrDefault();
        double tx = target?.X ?? g.Spider?.X ?? Field.Width / 2.0;
        double ty = Field.Height - 6;

        // Pick the best of a few nearby spots.
        double bestX = x, bestY = y, best = double.MaxValue;
        foreach (var (dx, dy) in new[] { (0, 0), (-10, 0), (10, 0), (-20, 0), (20, 0), (0, -10), (0, 10), (-10, -10), (10, -10) })
        {
            double px = Math.Clamp(x + dx, 4, Field.Width - 4), py = Math.Clamp(y + dy, Field.ZoneTop * Field.Cell + 4, Field.Height - 4);
            double score = Danger(px, py) * 3 + Math.Abs(px - tx) * 0.2 + Math.Abs(py - ty) * 0.05;
            if (score < best) { best = score; bestX = px; bestY = py; }
        }
        int mx = bestX < x - 1 ? -1 : bestX > x + 1 ? 1 : 0;
        int my = bestY < y - 1 ? -1 : bestY > y + 1 ? 1 : 0;
        // Keep shooting: segments, mushrooms in the way, everything earns points.
        bool fire = _rng.NextDouble() < 0.95;
        return new Controls(mx, my, fire);
    }
}
