using System;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Des chauves-souris dans la scène d'une ville (<see cref="HoMM.Core.TownScene.Bats"/>) : chacune vole en boucle
/// autour d'un point, en 8 ou 12 secondes, bat des ailes et plane par moments. Dessinées en pixels d'art, sombres, le
/// haut des ailes éclairé par la lune. Tout tourne en un temps qui divise 24 secondes, comme le reste de la scène.
/// </summary>
public partial class TownBats : Control
{
    private static readonly Color Body = new(0.2f, 0.16f, 0.27f), Rim = new(0.4f, 0.36f, 0.5f);
    // les ailes : levées, à plat, baissées (X : le corps, R : le bord éclairé) ; une grande et une petite, plus loin
    private static readonly string[][] Big =
    {
        new[] { "R.....R", ".X...X.", "..XRX..", "...X..." },
        new[] { ".......", "RRXRXRR", "X.XXX.X", "...X..." },
        new[] { ".......", "..RRR..", ".XXXXX.", "X..X..X" },
    };
    private static readonly string[][] Small =
    {
        new[] { "R...R", ".XRX.", "..X.." },
        new[] { ".....", "RRXRR", "..X.." },
        new[] { ".....", ".RRR.", "X.X.X" },
    };
    private static readonly int[] Flap = { 0, 1, 2, 1 };  // un battement : 4 dixièmes de seconde

    private readonly int[] _group;
    private float _time;

    /// <param name="group">[x, y, rayon en x, rayon en y, nombre], en pixels d'art</param>
    public TownBats(int[] group)
    {
        _group = group;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var (cx, cy, rx, ry, count) = (_group[0], _group[1], _group[2], _group[3], _group[4]);
        for (int k = 0; k < count; k++)
        {
            // une sur deux tourne dans l'autre sens, plus vite ; une boucle de travers, jamais tout à fait une ellipse
            float period = k % 2 == 0 ? 12 : 8, turn = k % 2 == 0 ? 1 : -1;
            float a = turn * _time * MathF.Tau / period + k * 2.4f + cx;
            float x = cx + rx * MathF.Cos(a) + rx * 0.18f * MathF.Sin(3 * a + k);
            float y = cy + ry * MathF.Sin(a) + ry * 0.4f * MathF.Sin(2 * a + k * 1.7f);
            // elle plane un moment à chaque tour, ailes à plat ; sinon elle bat des ailes
            bool glide = MathF.Sin(a + k) > 0.75f;
            int frame = glide ? 1 : Flap[((int)MathF.Floor(_time * 10) + k * 3) % Flap.Length];
            // la plus petite, plus loin, sur les boucles larges
            var shape = (rx > 30 && k % 3 == 2 ? Small : Big)[frame];
            Draw(shape, MathF.Round(x) - shape[0].Length / 2, MathF.Round(y) - (frame == 2 ? 2 : 1));
        }
    }

    private void Draw(string[] shape, float x0, float y0)
    {
        for (int y = 0; y < shape.Length; y++)
        for (int x = 0; x < shape[y].Length; x++)
            if (shape[y][x] != '.')
                DrawRect(new Rect2(new Vector2(x0 + x, y0 + y) * ArtScale, new Vector2(ArtScale, ArtScale)),
                    shape[y][x] == 'R' ? Rim : Body);
    }
}
