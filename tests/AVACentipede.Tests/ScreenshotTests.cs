using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVACentipede.Engine;
using AVACentipede.Models;
using AVACentipede.Views;

namespace AVACentipede.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set CENTIPEDE_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cent-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("CENTIPEDE_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVACentipede-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private static void Play(GameEngine g, AttractPilot pilot, int steps)
    {
        for (int i = 0; i < steps && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
    }

    [AvaloniaFact]
    public void The_title_and_the_first_wave()
    {
        var (w, c, g) = Open();
        g.Idle(60);
        Shot(w, c, "title");
        g.StartGame();
        Play(g, new AttractPilot(2), 60 * 4);
        Assert.NotEmpty(g.Segments);
        Shot(w, c, "wave1");
        w.Close();
    }

    [AvaloniaFact]
    public void The_spider_the_flea_and_the_scorpion()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame();
        g.JumpToWave(3);
        var pilot = new AttractPilot(3);
        Play(g, pilot, 60 * 6);
        g.PutSpider(60, 222);
        g.PutFlea(150, 120);
        g.PutScorpion(100, 60, 1);
        Play(g, pilot, 10);
        Shot(w, c, "creatures");
        w.Close();
    }

    [AvaloniaFact]
    public void A_later_wave_in_new_colours_with_poisoned_mushrooms()
    {
        var (w, c, g) = Open(seed: 6);
        g.StartGame();
        g.JumpToWave(5);
        // Poison a few mushrooms in the upper field.
        int done = 0;
        for (int col = 0; col < Field.Cols && done < 6; col++)
            for (int row = 4; row < 18 && done < 6; row++)
                if (g.Mushrooms[col, row] > 0 && (col + row) % 3 == 0) { g.Poisoned[col, row] = true; done++; }
        Play(g, new AttractPilot(4), 60 * 7);
        Shot(w, c, "wave5");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_initials_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 * 5 && g.Score < 6000; i++) g.Step(pilot.Decide(g));
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        Shot(w, c, "game-over");
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Type('A');
        g.Initials.Type('V');
        Shot(w, c, "initials");
        g.Initials.Type('A');
        g.Idle(5);
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
