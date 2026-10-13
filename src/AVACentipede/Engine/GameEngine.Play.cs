using AVACentipede.Models;

namespace AVACentipede.Engine;

public sealed partial class GameEngine
{
    public const int ChainLength = 12;

    private double _spiderClock;
    private double _scorpionClock;
    private double _headClock;
    private double _restoreClock;

    /// <summary>Pixels a segment moves each step: steady, then fast on alternate waves.</summary>
    public int CentipedeSpeed => Wave <= 2 || Wave % 2 == 1 ? 1 : 2;

    /// <summary>How many separate heads join a wave's centipede (the chain is that much shorter).</summary>
    public static int ExtraHeads(int wave) => Math.Min(Math.Max(0, wave - 1), 6);

    private void NextWave()
    {
        Wave++;
        StartCentipede();
        Play(Sfx.WaveStart);
    }

    /// <summary>A centipede along the top row, plus the wave's separate heads; the shooter back home.</summary>
    private void StartCentipede()
    {
        Segments.Clear();
        Flea = null;
        Spider = null;
        Scorpion = null;
        Shot = null;
        Phase = Phase.Playing;
        PhaseTime = 0;
        ShooterAlive = true;
        ShooterX = Field.Width / 2.0;
        ShooterY = Field.Height - 8;
        _spiderClock = 3;
        _scorpionClock = 12;
        _headClock = 0;

        int extra = ExtraHeads(Wave);
        int length = ChainLength - extra;
        int dir = Wave % 2 == 1 ? 1 : -1;
        int headCol = dir > 0 ? 14 + length / 2 : 15 - length / 2;
        Segment? ahead = null;
        for (int i = 0; i < length; i++)
        {
            int col = headCol - dir * i;
            var s = new Segment { X = (int)Field.CenterX(col), Y = (int)Field.CenterY(0), Dir = dir, Prev = ahead };
            Segments.Add(s);
            ahead = s;
        }
        for (int i = 0; i < extra; i++)
        {
            int col = _rng.Next(Field.Cols);
            Segments.Add(new Segment { X = (int)Field.CenterX(col), Y = (int)Field.CenterY(0), Dir = _rng.Next(2) == 0 ? -1 : 1 });
        }
        Beat(true);
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        switch (Phase)
        {
            case Phase.Playing:
                MoveShooter(dt, c);
                // Fire held fires again the moment the shot is spent, as on the arcade.
                if (c.Fire && Shot is null && ShooterAlive)
                {
                    Shot = new Vec(ShooterX, ShooterY - 6);
                    Play(Sfx.Fire);
                }
                MoveShot(dt);
                for (int i = 0; i < CentipedeSpeed; i++) MoveSegments();
                MoveFlea(dt);
                MoveSpider(dt);
                MoveScorpion(dt);
                SpawnCreatures(dt);
                CatchShooter();
                if (Phase == Phase.Playing && Segments.Count == 0) NextWave();
                break;
            case Phase.Dying:
                if (PhaseTime > 1.4)
                {
                    Phase = Phase.Restoring;
                    PhaseTime = 0;
                    _restoreClock = 0;
                    Flea = null;
                    Spider = null;
                    Scorpion = null;
                }
                break;
            case Phase.Restoring:
                _restoreClock -= dt;
                if (_restoreClock <= 0)
                {
                    _restoreClock = 0.06;
                    if (!RestoreOne())
                    {
                        if (Lives <= 0)
                        {
                            GameOver();
                            return;
                        }
                        // The same wave again, from the top.
                        StartCentipede();
                    }
                }
                break;
        }
    }

    // ----- the shooter ------------------------------------------------------------------------

    private void MoveShooter(double dt, Controls c)
    {
        if (!ShooterAlive) return;
        double vx, vy;
        if (c.Aim is { } aim)
        {
            // The mouse, like a trackball: head for the pointer, quickly but not instantly.
            var d = new Vec(aim.X - ShooterX, aim.Y - ShooterY);
            double len = d.Length;
            double step = Math.Min(len, MouseSpeed * dt);
            (vx, vy) = len < 0.01 ? (0, 0) : (d.X / len * step / dt, d.Y / len * step / dt);
        }
        else
        {
            vx = c.Dx * ShooterSpeed;
            vy = c.Dy * ShooterSpeed;
        }
        double nx = Math.Clamp(ShooterX + vx * dt, 4, Field.Width - 4);
        if (!ShooterBlocked(nx, ShooterY)) ShooterX = nx;
        double ny = Math.Clamp(ShooterY + vy * dt, Field.ZoneTop * Field.Cell + 4, Field.Height - 4);
        if (!ShooterBlocked(ShooterX, ny)) ShooterY = ny;
    }

    /// <summary>Mushrooms are solid to the shooter.</summary>
    private bool ShooterBlocked(double x, double y)
    {
        for (double dx = -3; dx <= 3; dx += 3)
            for (double dy = -3; dy <= 3; dy += 3)
                if (MushroomAt(Field.ColOf(x + dx), Field.RowOf(y + dy))) return true;
        return false;
    }

    private void MoveShot(double dt)
    {
        if (Shot is not { } shot) return;
        // In small steps, so it can't skip past anything.
        double travel = ShotSpeed * dt;
        int steps = (int)Math.Ceiling(travel / 2);
        for (int i = 0; i < steps; i++)
        {
            shot = new Vec(shot.X, shot.Y - travel / steps);
            if (shot.Y < 0) { Shot = null; return; }
            if (HitSomething(shot)) { Shot = null; return; }
        }
        Shot = shot;
    }

    /// <summary>Whatever the shot touches first takes the hit.</summary>
    private bool HitSomething(Vec p)
    {
        foreach (var s in Segments)
        {
            if (s.Dead || Math.Abs(s.X - p.X) > 4 || Math.Abs(s.Y - p.Y) > 4) continue;
            KillSegment(s);
            return true;
        }
        if (Flea is { Dead: false } f && Math.Abs(f.X - p.X) < 5 && Math.Abs(f.Y - p.Y) < 5)
        {
            f.Hits++;
            if (f.Hits >= 2)
            {
                f.Dead = true;
                AddScore(200);
                Burst(new Vec(f.X, f.Y));
                Play(Sfx.FleaHit);
            }
            else f.Speed *= 2.2; // the first hit only makes it angry
            return true;
        }
        if (Spider is { Dead: false } sp && Math.Abs(sp.X - p.X) < 8 && Math.Abs(sp.Y - p.Y) < 5)
        {
            sp.Dead = true;
            double gap = Math.Abs(sp.Y - ShooterY);
            int points = gap < 16 ? 900 : gap < 40 ? 600 : 300;
            AddScore(points);
            Burst(new Vec(sp.X, sp.Y));
            Popup(new Vec(sp.X, sp.Y), points);
            Play(Sfx.SpiderDie);
            return true;
        }
        if (Scorpion is { Dead: false } sc && Math.Abs(sc.X - p.X) < 8 && Math.Abs(sc.Y - p.Y) < 5)
        {
            sc.Dead = true;
            AddScore(1000);
            Burst(new Vec(sc.X, sc.Y));
            Popup(new Vec(sc.X, sc.Y), 1000);
            Play(Sfx.ScorpionDie);
            return true;
        }
        int col = Field.ColOf(p.X), row = Field.RowOf(p.Y);
        if (MushroomAt(col, row))
        {
            Mushrooms[col, row]--;
            if (Mushrooms[col, row] == 0)
            {
                Poisoned[col, row] = false;
                AddScore(1);
                Play(Sfx.MushroomGone);
            }
            else Play(Sfx.MushroomHit);
            return true;
        }
        return false;
    }

    /// <summary>A segment shot: points, a mushroom where it was, and the segment behind becomes a head.</summary>
    internal void KillSegment(Segment s)
    {
        bool head = s.IsHead;
        s.Dead = true;
        foreach (var behind in Segments.Where(x => x.Prev == s)) behind.Prev = null;
        AddScore(head ? 100 : 10);
        Burst(new Vec(s.X, s.Y));
        Play(head ? Sfx.HeadHit : Sfx.SegmentHit);
        Plant(s.Col, s.Row);
        Segments.Remove(s);
        if (Segments.Count == 0) Beat(false);
    }

    // ----- the centipede ----------------------------------------------------------------------

    /// <summary>Every segment moves one pixel by the same rules.</summary>
    private void MoveSegments()
    {
        foreach (var s in Segments) MoveSegment(s);
    }

    private void MoveSegment(Segment s)
    {
        if (s.Dropping)
        {
            s.Y += s.VDir;
            if (s.Y == s.TargetY)
            {
                s.Dropping = false;
                if (s.Diving)
                {
                    // Poisoned: straight on down to the bottom, then carry on as before.
                    if (s.Row < Field.Rows - 1) StartDrop(s);
                    else
                    {
                        s.Diving = false;
                        s.VDir = -1;
                        s.Dir = -s.Dir;
                    }
                }
                else s.Dir = -s.Dir;
            }
            return;
        }
        bool atCentre = ((s.X - Field.Cell / 2) % Field.Cell + Field.Cell) % Field.Cell == 0;
        if (atCentre)
        {
            int col = s.Col, row = s.Row, next = col + s.Dir;
            bool outside = !Field.InCols(col);
            bool wall = !outside && !Field.InCols(next);
            bool mushroom = MushroomAt(next, row);
            if (wall || mushroom)
            {
                if (mushroom && Poisoned[next, row]) s.Diving = true;
                StartDrop(s);
                return;
            }
        }
        s.X += s.Dir;
    }

    /// <summary>Down a row (or up, working back through the player's zone).</summary>
    private static void StartDrop(Segment s)
    {
        int row = s.Row;
        if (s.Diving) s.VDir = 1;
        int target = row + s.VDir;
        if (target >= Field.Rows) { s.VDir = -1; target = row - 1; }
        else if (s.VDir < 0 && target < Field.ZoneTop) { s.VDir = 1; target = row + 1; }
        s.TargetY = (int)Field.CenterY(target);
        s.Dropping = true;
    }

    // ----- the flea, the spider, the scorpion ------------------------------------------------

    private void SpawnCreatures(double dt)
    {
        // The flea drops in when the player's zone runs short of mushrooms (from the second wave).
        int fleaThreshold = Math.Min(5 + Wave / 2, 9);
        if (Wave >= 2 && Flea is null && Segments.Count > 0 && MushroomsInZone() < fleaThreshold && _rng.NextDouble() < 0.01)
        {
            Flea = new Flea { X = Field.CenterX(_rng.Next(Field.Cols)), Y = -4 };
            Play(Sfx.Flea);
        }
        _spiderClock -= dt;
        if (Spider is null && _spiderClock <= 0)
        {
            bool left = _rng.Next(2) == 0;
            Spider = new Spider
            {
                X = left ? -8 : Field.Width + 8,
                Y = (Field.ZoneTop - 2) * Field.Cell + _rng.NextDouble() * 40,
                Vx = (left ? 1 : -1) * (40 + Math.Min(40, Wave * 4)),
                Vy = 90,
            };
            Play(Sfx.Spider);
        }
        _scorpionClock -= dt;
        if (Wave >= 3 && Scorpion is null && _scorpionClock <= 0)
        {
            _scorpionClock = 14 + _rng.NextDouble() * 10;
            bool left = _rng.Next(2) == 0;
            int row = 2 + _rng.Next(Field.ZoneTop - 8);
            Scorpion = new Scorpion { X = left ? -8 : Field.Width + 8, Y = Field.CenterY(row), Dir = left ? 1 : -1, Speed = 45 + Math.Min(40, Wave * 3) };
            Play(Sfx.Scorpion);
        }
        // Once the centipede has reached the bottom, extra heads crawl in from the sides.
        if (Segments.Any(s => s.Row >= Field.ZoneTop))
        {
            _headClock += dt;
            if (_headClock > Math.Max(3, 8 - Wave * 0.3))
            {
                _headClock = 0;
                bool left = _rng.Next(2) == 0;
                int row = Field.ZoneTop + _rng.Next(3);
                Segments.Add(new Segment
                {
                    X = left ? -(int)(Field.Cell / 2.0) : Field.Width + Field.Cell / 2,
                    Y = (int)Field.CenterY(row),
                    Dir = left ? 1 : -1,
                    VDir = 1,
                });
            }
        }
    }

    private void MoveFlea(double dt)
    {
        if (Flea is not { } f) return;
        if (f.Dead) { Flea = null; return; }
        f.Y += f.Speed * dt;
        int row = Field.RowOf(f.Y);
        if (row != f.LastRow && Field.InRows(row))
        {
            f.LastRow = row;
            // It sows mushrooms as it falls.
            int col = Field.ColOf(f.X);
            if (row > 0 && row < Field.Rows - 2 && !MushroomAt(col, row) && _rng.NextDouble() < 0.28) Plant(col, row);
        }
        if (f.Y > Field.Height + 8) Flea = null;
    }

    /// <summary>The spider bounces down and up through the player's zone, eating mushrooms, and leaves by the far side.</summary>
    private void MoveSpider(double dt)
    {
        if (Spider is not { } s) return;
        if (s.Dead)
        {
            Spider = null;
            _spiderClock = 3 + _rng.NextDouble() * 4;
            return;
        }
        s.Clock += dt;
        s.X += s.Vx * dt;
        s.Y += s.Vy * dt;
        double top = (Field.ZoneTop - 4) * Field.Cell, bottom = Field.Height - 4;
        if (s.Y > bottom) { s.Y = bottom; s.Vy = -Math.Abs(s.Vy); }
        if (s.Y < top) { s.Y = top; s.Vy = Math.Abs(s.Vy); }
        // Now and then it pauses its sideways creep, or changes its bounce.
        if (_rng.NextDouble() < 0.01) s.Vy = Math.Sign(s.Vy) * (60 + _rng.NextDouble() * 70);
        int col = Field.ColOf(s.X), row = Field.RowOf(s.Y);
        if (MushroomAt(col, row) && _rng.NextDouble() < 0.06)
        {
            Mushrooms[col, row] = 0;
            Poisoned[col, row] = false;
        }
        if (s.X < -16 || s.X > Field.Width + 16)
        {
            Spider = null;
            _spiderClock = 2 + _rng.NextDouble() * 5;
        }
    }

    private void MoveScorpion(double dt)
    {
        if (Scorpion is not { } s) return;
        if (s.Dead) { Scorpion = null; return; }
        s.X += s.Dir * s.Speed * dt;
        int col = Field.ColOf(s.X), row = Field.RowOf(s.Y);
        if (MushroomAt(col, row)) Poisoned[col, row] = true;
        if (s.X < -16 || s.X > Field.Width + 16) Scorpion = null;
    }

    // ----- losing the shooter -----------------------------------------------------------------

    private void CatchShooter()
    {
        if (!ShooterAlive) return;
        bool Near(double x, double y, double rx, double ry) => Math.Abs(x - ShooterX) < rx && Math.Abs(y - ShooterY) < ry;
        bool caught = Segments.Any(s => Near(s.X, s.Y, 6, 6))
            || Flea is { Dead: false } f && Near(f.X, f.Y, 6, 6)
            || Spider is { Dead: false } sp && Near(sp.X, sp.Y, 9, 6);
        if (!caught) return;
        ShooterAlive = false;
        Shot = null;
        Lives--;
        Burst(new Vec(ShooterX, ShooterY));
        Play(Sfx.PlayerDie);
        Beat(false);
        Phase = Phase.Dying;
        PhaseTime = 0;
        LossLog.Add($"wave {Wave}: caught at ({ShooterX:0}, {ShooterY:0})");
    }

    /// <summary>How each shooter was lost (for tuning and tests).</summary>
    internal List<string> LossLog { get; } = [];

    /// <summary>Mends one damaged or poisoned mushroom (5 points); false when there are none left.</summary>
    private bool RestoreOne()
    {
        for (int row = 0; row < Field.Rows; row++)
            for (int col = 0; col < Field.Cols; col++)
            {
                if (Mushrooms[col, row] is > 0 and < 4 || Mushrooms[col, row] > 0 && Poisoned[col, row])
                {
                    Mushrooms[col, row] = 4;
                    Poisoned[col, row] = false;
                    AddScore(5);
                    Play(Sfx.Restore);
                    return true;
                }
            }
        return false;
    }

    // ----- hooks for tests and screenshots -----------------------------------------------------

    internal void JumpToWave(int wave)
    {
        Wave = wave - 1;
        NextWave();
    }

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Step(default);
    }

    internal void PutSpider(double x, double y) => Spider = new Spider { X = x, Y = y, Vx = 50, Vy = 80 };
    internal void PutFlea(double x, double y) => Flea = new Flea { X = x, Y = y };
    internal void PutScorpion(double x, double y, int dir) => Scorpion = new Scorpion { X = x, Y = y, Dir = dir };
}
