using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVACentipede.Engine;
using AVACentipede.Models;

namespace AVACentipede.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVACentipede/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Dim = Brush(0xFF8A8A96);

    private static Point P(double x, double y) => new(FieldLeft + x * Scale, y * Scale);

    private Sprites.Palette Colours => Engine.Palette;

    private void DrawScreen(DrawingContext dc)
    {
        var field = new Rect(FieldLeft, 0, Field.Width * Scale, Field.Height * Scale);
        using (dc.PushClip(field))
        {
            DrawMushrooms(dc);
            switch (Engine.State)
            {
                case GameState.Title:
                    DrawTitle(dc);
                    break;
                case GameState.HighScores:
                    DrawHighScores(dc);
                    break;
                default:
                    DrawPlay(dc);
                    break;
            }
        }
        dc.DrawRectangle(null, new Pen(Brush(0xFF22223A), 2), field);
        DrawPanels(dc);
    }

    private int Frame => (int)(_clock * 8) % 2;

    private void DrawMushrooms(DrawingContext dc)
    {
        var g = Engine;
        var pal = Colours;
        var poison = new Sprites.Palette(0xFFB050F0, 0xFFFFE14A, 0xFF7A3AB0);
        for (int col = 0; col < Field.Cols; col++)
            for (int row = 0; row < Field.Rows; row++)
            {
                int hp = g.Mushrooms[col, row];
                if (hp <= 0) continue;
                DrawSprite(dc, Sprites.Mushroom[hp - 1], col * Field.Cell, row * Field.Cell, g.Poisoned[col, row] ? poison : pal);
            }
    }

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        var pal = Colours;
        foreach (var s in g.Segments)
        {
            var frames = s.IsHead ? Sprites.Head : Sprites.Body;
            DrawSprite(dc, frames[(Frame + (s.X / Field.Cell)) % 2], s.X - 4, s.Y - 4, pal, flip: s.Dir < 0);
        }
        if (g.Flea is { Dead: false } f) DrawSprite(dc, Sprites.Flea[Frame], f.X - 4, f.Y - 4, pal);
        if (g.Spider is { Dead: false } sp) DrawSprite(dc, Sprites.Spider[(int)(sp.Clock * 6) % 2], sp.X - 6, sp.Y - 4, pal);
        if (g.Scorpion is { Dead: false } sc) DrawSprite(dc, Sprites.Scorpion[Frame], sc.X - 8, sc.Y - 4, pal, flip: sc.Dir < 0);
        if (g.ShooterAlive && g.Phase == Phase.Playing)
            DrawSprite(dc, Sprites.Shooter, g.ShooterX - 3.5, g.ShooterY - 4, pal);
        if (g.Shot is { } shot) DrawSprite(dc, Sprites.Shot, shot.X - 0.5, shot.Y - 2, pal);

        foreach (var fx in g.Effects)
        {
            switch (fx.Kind)
            {
                case EffectKind.Burst:
                    var frames = Sprites.Burst;
                    int i = Math.Min(frames.Length - 1, (int)(fx.Age / fx.Life * frames.Length));
                    DrawSprite(dc, frames[i], fx.Pos.X - 4, fx.Pos.Y - 4, pal);
                    break;
                case EffectKind.Score:
                    Text(dc, fx.Text, P(fx.Pos.X, fx.Pos.Y - 4 - fx.Age * 8), 30, White, center: true);
                    break;
            }
        }

        switch (g.State)
        {
            case GameState.Paused:
                Text(dc, "PAUSED", P(Field.Width / 2.0, 110), 56, White, center: true);
                Text(dc, "ESC to quit, any other key to resume", P(Field.Width / 2.0, 126), 26, Dim, center: true);
                break;
            case GameState.Attract:
                Text(dc, "DEMONSTRATION", P(Field.Width / 2.0, 120), 34, Red, center: true);
                if (_clock % 1 < 0.6) Text(dc, "PRESS FIRE TO PLAY", P(Field.Width / 2.0, 132), 30, White, center: true);
                break;
            case GameState.GameOver:
                Text(dc, "GAME OVER", P(Field.Width / 2.0, 120), 60, Red, center: true);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
        }
        if (g.Phase == Phase.Restoring && g.State is GameState.Playing or GameState.Attract)
            Text(dc, "MENDING THE GARDEN", P(Field.Width / 2.0, 120), 34, Yellow, center: true);
    }

    /// <summary>Draws a sprite with its top-left at a playfield point; A, B and C take the wave's colours.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y, Sprites.Palette pal, bool flip = false)
    {
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[flip ? s.Width - 1 - c : c];
                if (ch == '.') continue;
                uint argb = ch switch
                {
                    'A' => pal.A,
                    'B' => pal.B,
                    'C' => pal.C,
                    _ => Sprites.Fixed.GetValueOrDefault(ch, 0xFFFF00FF),
                };
                var a = P(x + c, y + r);
                dc.FillRectangle(Brush(argb), new Rect(a.X, a.Y, Scale + 0.3, Scale + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
