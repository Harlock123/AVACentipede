namespace AVACentipede.Models;

/// <summary>
/// The game's pixel art, this game's own drawings. A, B and C stand for the wave's palette (each wave
/// has new colours); the other letters are fixed colours.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>A wave's colours: main, accent, and a third for stems and legs.</summary>
    public sealed record Palette(uint A, uint B, uint C);

    /// <summary>Each wave takes the next of these.</summary>
    public static readonly Palette[] Waves =
    [
        new(0xFF38D96B, 0xFFFF3D7F, 0xFFFFD84A), // green and pink
        new(0xFFFF8A1E, 0xFF3CC8FF, 0xFFF4F0E6), // orange and sky
        new(0xFFB070FF, 0xFFFFE14A, 0xFF5BE07A), // violet and gold
        new(0xFF3C8CFF, 0xFFFF5A3C, 0xFFFFD84A), // blue and red
        new(0xFFFFE14A, 0xFF38D96B, 0xFFFF7AD0), // yellow and green
        new(0xFFFF4FA0, 0xFF4FF0E0, 0xFFF4F0E6), // rose and aqua
        new(0xFF6CF06C, 0xFFB070FF, 0xFFFF8A1E), // lime and violet
        new(0xFFF4F0E6, 0xFFFF3D3D, 0xFF3CC8FF), // white and red
    ];

    public static readonly IReadOnlyDictionary<char, uint> Fixed = new Dictionary<char, uint>
    {
        ['w'] = 0xFFF4F0E6, // white
        ['r'] = 0xFFFF3D3D, // red
        ['y'] = 0xFFFFE14A, // yellow
        ['o'] = 0xFFFF8A1E, // orange
        ['p'] = 0xFFB050F0, // poison purple
        ['k'] = 0xFF9A9AA6, // grey
        ['c'] = 0xFF4FD7FF, // cyan
        ['g'] = 0xFF5BE07A, // green
        ['m'] = 0xFFFF4FD8, // magenta
    };

    /// <summary>A mushroom shrinks with each hit: four hits, four sizes.</summary>
    public static readonly Sprite[] Mushroom =
    [
        S("..AAAA..", ".AAAAAA.", "AABAABAA", "AAAAAAAA", "........", "........", "........", "........"),
        S("..AAAA..", ".AAAAAA.", "AABAABAA", "AAAAAAAA", "...CC...", "........", "........", "........"),
        S("..AAAA..", ".AAAAAA.", "AABAABAA", "AAAAAAAA", "...CC...", "...CC...", "........", "........"),
        S("..AAAA..", ".AAAAAA.", "AABAABAA", "AAAAAAAA", "...CC...", "...CC...", "..CCCC..", "........"),
    ];

    /// <summary>The head: eyes, antennae and legs. Two frames.</summary>
    public static readonly Sprite[] Head =
    [
        S(".C....C.", "..AAAA..", ".AAAAAA.", "ABBAABBA", "AAAAAAAA", ".AAAAAA.", "..AAAA..", ".C.CC.C."),
        S("C......C", ".CAAAAC.", ".AAAAAA.", "ABBAABBA", "AAAAAAAA", ".AAAAAA.", "..AAAA..", "C..CC..C"),
    ];

    public static readonly Sprite[] Body =
    [
        S("........", "..AAAA..", ".AAAAAA.", "AAAAAAAA", "AAAAAAAA", ".AAAAAA.", "..AAAA..", ".C....C."),
        S("........", "..AAAA..", ".AAAAAA.", "AAAAAAAA", "AAAAAAAA", ".AAAAAA.", "C.AAAA.C", "........"),
    ];

    /// <summary>The shooter: a little wand-bearer peering up from the bottom of the garden.</summary>
    public static readonly Sprite Shooter = S(
        "...w...",
        "..wcw..",
        "..wcw..",
        ".wcccw.",
        ".wcccw.",
        "wcyyycw",
        "wwwwwww",
        ".ww.ww.");

    public static readonly Sprite Shot = S("y", "y", "w", "w");

    /// <summary>The flea: drops straight down, sowing mushrooms.</summary>
    public static readonly Sprite[] Flea =
    [
        S("..mm....", ".mmmm...", "mmwmmm..", "mmmmmmm.", ".mmmmmmm", "..m.m.m.", ".m.m.m..", "m......."),
        S("..mm....", ".mmmm...", "mmwmmm..", "mmmmmmm.", ".mmmmmmm", ".m.m.m..", "..m.m.m.", "...m...."),
    ];

    /// <summary>The spider: eight legs and a bouncing gait.</summary>
    public static readonly Sprite[] Spider =
    [
        S("y..y....y..y....", ".y..y..y..y.....", "..yy.ww.yy......", "...wwrrww.......", "..yy.ww.yy......", ".y..y..y..y.....", "y..........y....", "................"),
        S("................", "y..y....y..y....", ".y.yy..yy.y.....", "...wwrrww.......", "..y.yww.y.y.....", ".y..y..y..y.....", "..y......y......", "................"),
    ];

    /// <summary>The scorpion: crosses the upper field, poisoning the mushrooms it passes.</summary>
    public static readonly Sprite[] Scorpion =
    [
        S("..........ooo...", ".........o...o..", "....ooooo....o..", "..ooooooooo.o...", ".o.ooooooo.oo...", "o.o.o.o.o.......", ".......r........", "................"),
        S("..........ooo...", ".........o...o..", "....ooooo....o..", "..ooooooooo.o...", ".o.ooooooo.oo...", ".o.o.o.o.o......", ".......r........", "................"),
    ];

    /// <summary>A burst, for anything shot.</summary>
    public static readonly Sprite[] Burst =
    [
        S("........", "...ww...", "..wyyw..", ".wyrryw.", ".wyrryw.", "..wyyw..", "...ww...", "........"),
        S("w..w..w.", ".y.y.y..", "..yry...", "wyr.ryw.", "..yry...", ".y.y.y..", "w..w..w.", "........"),
        S("o......o", "...o....", ".o...o..", "...r....", "..o...o.", "o.......", "...o..o.", "........"),
    ];
}
