using AVACentipede.Engine;
using AVACentipede.Models;

namespace AVACentipede.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cent-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private GameEngine NewGame(int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        return g;
    }

    /// <summary>An empty garden with one segment in it, to watch it move.</summary>
    private static Segment Alone(GameEngine g, int col, int row, int dir)
    {
        Array.Clear(g.Mushrooms);
        g.Segments.Clear();
        var s = new Segment { X = (int)Field.CenterX(col), Y = (int)Field.CenterY(row), Dir = dir };
        g.Segments.Add(s);
        return s;
    }

    private static void Steps(GameEngine g, int n)
    {
        for (int i = 0; i < n; i++) g.Step(default);
    }

    [Fact]
    public void The_first_wave_is_a_twelve_segment_centipede_with_one_head()
    {
        var g = NewGame();
        Assert.Equal(12, g.Segments.Count);
        Assert.Single(g.Segments, s => s.IsHead);
        Assert.All(g.Segments, s => Assert.Equal(0, s.Row));
        Assert.True(g.Mushrooms.Cast<int>().Count(h => h > 0) > 20);
    }

    [Fact]
    public void Later_waves_bring_separate_heads()
    {
        Assert.Equal(0, GameEngine.ExtraHeads(1));
        Assert.Equal(1, GameEngine.ExtraHeads(2));
        Assert.Equal(6, GameEngine.ExtraHeads(20));
        var g = NewGame();
        g.JumpToWave(4);
        Assert.Equal(12, g.Segments.Count);
        Assert.Equal(4, g.Segments.Count(s => s.IsHead));
    }

    [Fact]
    public void A_segment_turns_down_and_back_at_a_mushroom()
    {
        var g = NewGame();
        var s = Alone(g, 10, 5, 1);
        g.Mushrooms[13, 5] = 4;
        Steps(g, 8 * 4);
        // It stopped short of the mushroom, dropped a row, and now heads left.
        Assert.Equal(6, s.Row);
        Assert.Equal(-1, s.Dir);
        Assert.True(s.Col <= 12);
    }

    [Fact]
    public void A_segment_turns_at_the_wall()
    {
        var g = NewGame();
        var s = Alone(g, Field.Cols - 2, 3, 1);
        Steps(g, 8 * 4);
        Assert.Equal(4, s.Row);
        Assert.Equal(-1, s.Dir);
    }

    [Fact]
    public void At_the_bottom_a_segment_works_back_up_through_the_zone()
    {
        var g = NewGame();
        var s = Alone(g, 5, Field.Rows - 1, 1);
        s.VDir = 1;
        Assert.Equal(Field.Rows - 1, s.Row);
        // Run it into the wall: it must go up now.
        s.X = (int)Field.CenterX(Field.Cols - 1);
        Steps(g, 8 * 2);
        Assert.Equal(Field.Rows - 2, s.Row);
        Assert.Equal(-1, s.VDir);
    }

    [Fact]
    public void A_poisoned_mushroom_sends_a_head_diving_to_the_bottom()
    {
        var g = NewGame();
        var s = Alone(g, 10, 4, 1);
        g.Mushrooms[12, 4] = 4;
        g.Poisoned[12, 4] = true;
        Steps(g, 8 * 30);
        Assert.True(s.Row >= Field.Rows - 2, $"row {s.Row}");
    }

    [Fact]
    public void Shooting_a_segment_leaves_a_mushroom_and_the_one_behind_becomes_a_head()
    {
        var g = NewGame();
        Array.Clear(g.Mushrooms);
        // Let it crawl down off the top row (nothing grows on the top row, so the next centipede can always come in).
        for (int i = 0; i < 60 * 30 && g.Segments.Any(s => s.Row < 3); i++) g.Step(default);
        var body = g.Segments.First(s => !s.IsHead && g.Segments.Any(b => b.Prev == s));
        var behind = g.Segments.Single(b => b.Prev == body);
        int col = body.Col, row = body.Row;
        int before = g.Score;
        g.KillSegment(body);
        Assert.Equal(before + 10, g.Score);
        Assert.Equal(4, g.Mushrooms[col, row]);
        Assert.True(behind.IsHead);
        Assert.Equal(2, g.Segments.Count(s => s.IsHead));
        var head = g.Segments.First(s => s.IsHead);
        g.KillSegment(head);
        Assert.Equal(before + 110, g.Score);
    }

    [Fact]
    public void Mushrooms_take_four_shots()
    {
        var g = NewGame();
        Array.Clear(g.Mushrooms);
        g.Segments.Clear();
        g.Segments.Add(new Segment { X = 4, Y = 4 }); // keep the wave going, out of the way
        int col = Field.ColOf(g.ShooterX);
        g.Mushrooms[col, 20] = 4;
        int fired = 0;
        for (int i = 0; i < 600 && g.Mushrooms[col, 20] > 0; i++)
        {
            g.Step(new Controls(0, 0, true));
            fired++;
        }
        Assert.Equal(0, g.Mushrooms[col, 20]);
        Assert.Equal(1, g.Score);
    }

    [Fact]
    public void The_spider_is_worth_more_the_closer_it_is()
    {
        foreach (var (dy, points) in new[] { (8.0, 900), (30.0, 600), (60.0, 300) })
        {
            var g = NewGame();
            Array.Clear(g.Mushrooms);
            g.Segments.Clear();
            g.Segments.Add(new Segment { X = 4, Y = 4 });
            g.PutSpider(g.ShooterX, g.ShooterY - dy);
            g.Spider!.Vx = 0;
            g.Spider.Vy = 0;
            for (int i = 0; i < 60 && g.Spider is not null; i++) g.Step(new Controls(0, 0, true));
            Assert.Equal(points, g.Score);
        }
    }

    [Fact]
    public void The_flea_takes_two_hits_and_sows_mushrooms()
    {
        var g = NewGame();
        Array.Clear(g.Mushrooms);
        g.Segments.Clear();
        g.Segments.Add(new Segment { X = 4, Y = 4 });
        g.PutFlea(Field.CenterX(3), 0);
        for (int i = 0; i < 200 && g.Flea is not null; i++) g.Step(default);
        Assert.True(g.Mushrooms.Cast<int>().Count(h => h > 0) > 0, "the flea sowed nothing");

        var g2 = NewGame();
        Array.Clear(g2.Mushrooms);
        g2.Segments.Clear();
        g2.Segments.Add(new Segment { X = 4, Y = 4 });
        g2.PutFlea(g2.ShooterX, 100);
        g2.Flea!.Speed = 0;
        for (int i = 0; i < 120 && g2.Flea is not null; i++) g2.Step(new Controls(0, 0, true));
        Assert.Equal(200, g2.Score);
    }

    [Fact]
    public void The_scorpion_poisons_the_mushrooms_it_crosses()
    {
        var g = NewGame();
        Array.Clear(g.Mushrooms);
        g.Mushrooms[10, 8] = 4;
        g.Mushrooms[20, 8] = 4;
        g.PutScorpion(-8, Field.CenterY(8), 1);
        for (int i = 0; i < 60 * 8 && g.Scorpion is not null; i++) g.Step(default);
        Assert.True(g.Poisoned[10, 8] && g.Poisoned[20, 8]);
    }

    [Fact]
    public void Losing_the_shooter_mends_the_garden_for_five_points_a_mushroom()
    {
        var g = NewGame();
        Array.Clear(g.Mushrooms);
        g.Mushrooms[3, 3] = 2;
        g.Mushrooms[4, 4] = 4;
        g.Poisoned[4, 4] = true;
        g.Mushrooms[5, 5] = 4;
        int lives = g.Lives;
        g.PutSpider(g.ShooterX, g.ShooterY);
        g.Step(default);
        Assert.False(g.ShooterAlive);
        Assert.Equal(lives - 1, g.Lives);
        for (int i = 0; i < 60 * 5 && !(g.ShooterAlive && g.Phase == Phase.Playing); i++) g.Step(default);
        Assert.Equal(4, g.Mushrooms[3, 3]);
        Assert.False(g.Poisoned[4, 4]);
        Assert.Equal(10, g.Score);
        Assert.Equal(12, g.Segments.Count); // the wave starts again
    }

    [Fact]
    public void A_bonus_shooter_every_twelve_thousand()
    {
        var g = NewGame();
        int lives = g.Lives;
        g.AddScore(11999);
        Assert.Equal(lives, g.Lives);
        g.AddScore(1);
        Assert.Equal(lives + 1, g.Lives);
    }

    [Fact]
    public void The_shooter_stays_in_its_zone_and_mushrooms_stop_it()
    {
        var g = NewGame();
        Array.Clear(g.Mushrooms);
        for (int i = 0; i < 300; i++) g.Step(new Controls(0, -1, false));
        Assert.True(g.ShooterY >= Field.ZoneTop * Field.Cell + 4 - 0.01);
        double x = g.ShooterX;
        int col = Field.ColOf(x) + 2, row = Field.RowOf(g.ShooterY);
        g.Mushrooms[col, row] = 4;
        for (int i = 0; i < 120; i++) g.Step(new Controls(1, 0, false));
        Assert.True(g.ShooterX < Field.CenterX(col) - 4, $"walked into the mushroom: {g.ShooterX}");
    }

    [Fact]
    public void The_mouse_steers_the_shooter_towards_the_pointer()
    {
        var g = NewGame();
        Array.Clear(g.Mushrooms);
        var target = new Vec(40, Field.Height - 20);
        for (int i = 0; i < 120; i++) g.Step(new Controls(0, 0, false, target));
        Assert.InRange(g.ShooterX, 38, 42);
        Assert.InRange(g.ShooterY, Field.Height - 22, Field.Height - 18);
    }

    [Fact]
    public void Clearing_the_centipede_starts_the_next_wave_in_new_colours()
    {
        var g = NewGame();
        var colours = g.Palette;
        foreach (var s in g.Segments.ToList()) g.KillSegment(s);
        g.Step(default);
        Assert.Equal(2, g.Wave);
        Assert.NotEqual(colours, g.Palette);
        Assert.Equal(12, g.Segments.Count);
    }

    [Fact]
    public void The_demonstration_pilot_plays_and_the_game_ends_in_the_table()
    {
        var g = NewGame(seed: 3);
        var pilot = new AttractPilot(3);
        for (int i = 0; i < 60 * 60 * 3 && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
        Assert.True(g.Wave >= 3, $"reached wave {g.Wave}");
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Finish();
        g.Idle(2);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(g.Score, HighScoreTable.Load(_dir).TopScore);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = NewGame(seed: 21);
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Step(pilot.Decide(g));
            return $"{g.Wave} {g.Score} {g.Lives} {g.Segments.Count} {g.Mushrooms.Cast<int>().Sum()}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 5)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.WaveStart, heard);
        Assert.Contains(Sfx.BeatStart, heard);
        Assert.Contains(Sfx.Fire, heard);
        g.Idle((int)(60 * (GameEngine.AttractSeconds + 1)));
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(Sfx.BeatStop, heard.Last(s => s is Sfx.BeatStart or Sfx.BeatStop));
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        string[] files = ["Fire", "MushroomHit", "MushroomGone", "SegmentHit", "HeadHit", "Flea", "FleaHit", "Spider", "SpiderDie",
            "Scorpion", "ScorpionDie", "PlayerDie", "Restore", "ExtraLife", "WaveStart", "GameOver", "TitleTune", "Beat"];
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
    }

    [Fact]
    public void Every_sprite_is_well_formed()
    {
        Assert.Equal(4, Sprites.Mushroom.Length);
        Assert.All(Sprites.Mushroom, s => Assert.Equal(8, s.Width));
        Assert.Equal(8, Sprites.Waves.Length);
    }
}
