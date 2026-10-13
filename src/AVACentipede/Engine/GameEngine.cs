using AVACentipede.Models;

namespace AVACentipede.Engine;

public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

/// <summary>What is happening within a game.</summary>
public enum Phase
{
    Playing,
    /// <summary>The shooter is hit: a moment's pause.</summary>
    Dying,
    /// <summary>Damaged and poisoned mushrooms are mended, five points each, one by one.</summary>
    Restoring,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Fire, MushroomHit, MushroomGone, SegmentHit, HeadHit, Flea, FleaHit, Spider, SpiderDie, Scorpion,
    ScorpionDie, PlayerDie, Restore, ExtraLife, WaveStart, GameOver, TitleTune, BeatStart, BeatStop,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and the controls; it
/// runs in fixed 60 Hz steps, so it plays the same at any frame rate and a seed always plays the
/// same game.
/// </summary>
public sealed partial class GameEngine
{
    public const int StartingLives = 3;
    public const int ExtraLifeEvery = 12000;
    public const double TitleIdleSeconds = 15;
    public const double AttractSeconds = 45;
    public const double HighScoreSeconds = 9;
    public const double ShooterSpeed = 140;
    public const double MouseSpeed = 230;
    public const double ShotSpeed = 420;

    private readonly Random _rng;
    private double _accumulator;
    private bool _startPrev;
    private bool _titleTunePending = true;
    private bool _beat;

    public GameEngine(int seed = 1980, HighScoreTable? table = null)
    {
        _rng = new Random(seed);
        HighScores = table ?? HighScoreTable.Load();
        PlantField();
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Playing;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    public int NewEntryIndex { get; private set; } = -1;
    public AttractPilot? Pilot { get; private set; }
    public bool Silent { get; set; }

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Wave { get; private set; }
    private int _nextExtraLife;

    /// <summary>Mushroom hit points by cell (0: none; 1-4: a mushroom, smaller as it's shot).</summary>
    public int[,] Mushrooms { get; } = new int[Field.Cols, Field.Rows];
    /// <summary>Mushrooms the scorpion has poisoned.</summary>
    public bool[,] Poisoned { get; } = new bool[Field.Cols, Field.Rows];

    public List<Segment> Segments { get; } = [];
    public Flea? Flea { get; private set; }
    public Spider? Spider { get; private set; }
    public Scorpion? Scorpion { get; private set; }
    public List<Effect> Effects { get; } = [];

    public double ShooterX { get; private set; } = Field.Width / 2.0;
    public double ShooterY { get; private set; } = Field.Height - 8;
    public bool ShooterAlive { get; private set; } = true;
    /// <summary>The shot in flight (one at a time).</summary>
    public Vec? Shot { get; private set; }

    public Sprites.Palette Palette => Sprites.Waves[(Math.Max(1, Wave) - 1) % Sprites.Waves.Length];

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    public void Update(double dt, Controls controls, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Step(controls, start);
        }
    }

    /// <summary>One 1/60 s step.</summary>
    public void Step(Controls c, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        UpdateEffects(dt);

        switch (State)
        {
            case GameState.Title:
                if (_titleTunePending) { _titleTunePending = false; Play(Sfx.TitleTune); }
                if (startPressed || (c.Fire && StateTime > 0.6)) StartGame();
                else if (StateTime > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Simulate(dt, Pilot!.Decide(this));
                if (startPressed) { StartGame(); break; }
                if (State == GameState.Attract && StateTime > AttractSeconds) ShowHighScores(-1);
                break;
            case GameState.Playing:
                Simulate(dt, c);
                break;
            case GameState.GameOver:
                if (StateTime > 3)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, Wave);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                if (startPressed || (c.Fire && StateTime > 1)) StartGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    public void StartGame()
    {
        Pilot = null;
        Begin(GameState.Playing);
    }

    private void StartAttract()
    {
        Pilot = new AttractPilot(_rng.Next());
        Begin(GameState.Attract);
    }

    private void Begin(GameState state)
    {
        State = state;
        StateTime = 0;
        Score = 0;
        Lives = StartingLives;
        _nextExtraLife = ExtraLifeEvery;
        Wave = 0;
        NewEntryIndex = -1;
        Effects.Clear();
        PlantField();
        NextWave();
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        Pilot = null;
        _titleTunePending = true;
        ClearCreatures();
        Effects.Clear();
        PlantField();
        Wave = 0;
        Beat(false);
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
        Pilot = null;
        ClearCreatures();
        Beat(false);
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
        Beat(false);
    }

    public void Resume()
    {
        if (State == GameState.Paused) State = GameState.Playing;
    }

    private void GameOver()
    {
        Beat(false);
        if (State == GameState.Attract)
        {
            ShowHighScores(-1);
            return;
        }
        Play(Sfx.GameOver);
        State = GameState.GameOver;
        StateTime = 0;
    }

    internal void AddScore(int points)
    {
        Score += points;
        while (Score >= _nextExtraLife)
        {
            Lives++;
            _nextExtraLife += ExtraLifeEvery;
            Play(Sfx.ExtraLife);
        }
    }

    /// <summary>The marching beat plays while a centipede is on the field.</summary>
    private void Beat(bool on)
    {
        if (on == _beat) return;
        _beat = on;
        Play(on ? Sfx.BeatStart : Sfx.BeatStop);
    }

    private void ClearCreatures()
    {
        Segments.Clear();
        Flea = null;
        Spider = null;
        Scorpion = null;
        Shot = null;
    }

    private void UpdateEffects(double dt)
    {
        foreach (var e in Effects) e.Age += dt;
        Effects.RemoveAll(e => e.Done);
    }

    private void Burst(Vec at) => Effects.Add(new Effect { Kind = EffectKind.Burst, Pos = at, Life = 0.35 });

    private void Popup(Vec at, int points) =>
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at, Life = 0.9, Text = points.ToString() });

    // ----- the mushroom field -------------------------------------------------------------------

    /// <summary>A fresh field: mushrooms scattered down the garden, fewer in the player's zone.</summary>
    private void PlantField()
    {
        Array.Clear(Mushrooms);
        Array.Clear(Poisoned);
        for (int col = 0; col < Field.Cols; col++)
            for (int row = 1; row < Field.Rows - 2; row++)
            {
                double chance = row >= Field.ZoneTop ? 0.035 : 0.07;
                if (_rng.NextDouble() < chance) Mushrooms[col, row] = 4;
            }
    }

    public bool MushroomAt(int col, int row) =>
        Field.InCols(col) && Field.InRows(row) && Mushrooms[col, row] > 0;

    public int MushroomsInZone()
    {
        int n = 0;
        for (int col = 0; col < Field.Cols; col++)
            for (int row = Field.ZoneTop; row < Field.Rows; row++)
                if (Mushrooms[col, row] > 0) n++;
        return n;
    }

    /// <summary>Plants a mushroom (where a segment was shot, or where a flea drops one).</summary>
    private void Plant(int col, int row)
    {
        if (!Field.InCols(col) || !Field.InRows(row) || row == 0) return;
        Mushrooms[col, row] = 4;
        Poisoned[col, row] = false;
    }
}
