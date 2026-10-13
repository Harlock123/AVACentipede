using Avalonia;
using Avalonia.Media;
using AVACentipede.Engine;
using AVACentipede.Models;

namespace AVACentipede.Views;

public sealed partial class GameCanvas
{
    private static readonly double RightPanel = FieldLeft + Field.Width * Scale;

    /// <summary>The panels either side: the scores on the left; the wave, the spare shooters and the keys on the right.</summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        double lx = FieldLeft / 2, rx = RightPanel + (ScreenWidth - RightPanel) / 2;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;
        var pal = Colours;

        Text(dc, "HIGH SCORE", new Point(lx, 60), 30, Red, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString(), new Point(lx, 92), 34, White, center: true);
        Text(dc, "SCORE", new Point(lx, 160), 30, Red, center: true);
        Text(dc, (playing ? g.Score : 0).ToString(), new Point(lx, 192), 34, White, center: true);

        if (playing)
        {
            Text(dc, "WAVE", new Point(rx, 60), 30, Red, center: true);
            Text(dc, g.Wave.ToString(), new Point(rx, 92), 34, White, center: true);
            for (int i = 0; i < Math.Min(g.Lives, 8); i++)
                DrawPanelSprite(dc, Sprites.Shooter, rx - 42 + (i % 4) * 28, 170 + (i / 4) * 34, pal);
        }
        Text(dc, "AVACENTIPEDE", new Point(rx, 540), 24, Dim, center: true);
        Text(dc, "Mouse, arrows", new Point(rx, 570), 20, Dim, center: true);
        Text(dc, "or WASD move", new Point(rx, 590), 20, Dim, center: true);
        Text(dc, "Space or click fires", new Point(rx, 610), 20, Dim, center: true);
        Text(dc, "M mute  P pause", new Point(rx, 640), 20, Dim, center: true);
        Text(dc, "Ctrl+S screenshot", new Point(rx, 660), 20, Dim, center: true);
    }

    private static void DrawPanelSprite(DrawingContext dc, Sprites.Sprite s, double cx, double cy, Sprites.Palette pal)
    {
        const double px = 3;
        for (int r = 0; r < s.Height; r++)
            for (int c = 0; c < s.Width; c++)
            {
                char ch = s.Rows[r][c];
                if (ch == '.') continue;
                uint argb = ch switch { 'A' => pal.A, 'B' => pal.B, 'C' => pal.C, _ => Sprites.Fixed.GetValueOrDefault(ch, 0xFFFF00FF) };
                dc.FillRectangle(Brush(argb), new Rect(cx - s.Width * px / 2 + c * px, cy - s.Height * px / 2 + r * px, px + 0.2, px + 0.2));
            }
    }

    private static void Panel(DrawingContext dc, double left, double top, double right, double bottom) =>
        dc.FillRectangle(Brush(0xE0000000), new Rect(P(left, top), P(right, bottom)));

    private void DrawTitle(DrawingContext dc)
    {
        var pal = Colours;
        Panel(dc, 12, 18, 228, 232);
        Text(dc, "AVACENTIPEDE", P(120, 36), 80, Brush(pal.A), center: true);
        Text(dc, "AFTER THE 1980 ARCADE CLASSIC", P(120, 52), 26, Dim, center: true);
        Row(Sprites.Head[0], 74, "HEAD", "100");
        Row(Sprites.Body[0], 88, "BODY SEGMENT", "10");
        Row(Sprites.Flea[0], 102, "FLEA (TWO HITS)", "200");
        Row(Sprites.Spider[0], 116, "SPIDER, CLOSE IN", "300 600 900");
        Row(Sprites.Scorpion[0], 130, "SCORPION", "1000");
        Row(Sprites.Mushroom[3], 144, "MUSHROOM (FOUR HITS)", "1");
        Text(dc, "SHOT SEGMENTS TURN INTO MUSHROOMS.", P(120, 162), 26, White, center: true);
        Text(dc, "POISONED MUSHROOMS SEND HEADS DIVING.", P(120, 172), 26, White, center: true);
        Text(dc, $"BONUS SHOOTER EVERY {GameEngine.ExtraLifeEvery}", P(120, 186), 28, Yellow, center: true);
        if (_clock % 1 < 0.65)
            Text(dc, "PRESS FIRE, CLICK OR ENTER TO START", P(120, 206), 30, White, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(120, 224), 22, Dim, center: true);

        void Row(Sprites.Sprite s, double y, string name, string points)
        {
            DrawSprite(dc, s, 46 - s.Width / 2.0, y - 4, pal);
            Text(dc, name, P(62, y), 28, White);
            Text(dc, points, P(214, y), 28, White, right: true);
        }
    }

    private void DrawHighScores(DrawingContext dc)
    {
        Panel(dc, 30, 18, 210, 226);
        Text(dc, "BEST GARDENERS", P(120, 32), 54, Yellow, center: true);
        Text(dc, "RANK    SCORE   NAME  WAVE", P(120, 48), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Brush(Colours.A) : White;
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}  {e.Stage,3}", P(120, 64 + i * 14), 32, brush, center: true);
        }
        if (_clock % 1 < 0.65) Text(dc, "PRESS FIRE TO PLAY", P(120, 216), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        Panel(dc, 20, 96, 220, 186);
        Text(dc, "A NEW HIGH SCORE!", P(120, 108), 44, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(120, 124), 40, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(108 + i * 12, 148);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 70, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 16, at.Y + 32, 32, 5));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(120, 176), 22, Dim, center: true);
    }
}
