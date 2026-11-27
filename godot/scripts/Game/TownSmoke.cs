using System;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// La fumée d'une cheminée dans la scène d'une ville : des bouffées qui naissent au haut de la cheminée, montent en
/// grossissant, penchent avec le vent et s'effacent. Dessinée en pixels d'art (des carrés de <see cref="Ui.ArtScale"/>),
/// chaque bouffée posée sur la grille : elle monte pixel par pixel, comme le reste de la scène.
/// </summary>
public partial class TownSmoke : Control
{
    private const float Period = 0.5f;   // une bouffée toutes les demi-secondes
    private const float Life = 5f;       // le temps qu'elle met à monter et s'effacer
    private const float Rise = 38f;      // sa hauteur au bout de sa vie, en pixels d'art
    private const float Drift = 12f;     // le vent la pousse vers la droite
    private const int Puffs = 48;        // les bouffées se répètent toutes les 24 secondes, comme le reste de la scène
    private static readonly Color Light = new(0.66f, 0.64f, 0.71f), Mid = new(0.5f, 0.48f, 0.56f), Dark = new(0.34f, 0.32f, 0.4f);

    private readonly int _seed;
    private float _time;

    /// <param name="seed">pour que deux cheminées ne fument pas au même rythme</param>
    public TownSmoke(int seed)
    {
        _seed = seed;
        _time = Noise(seed, 1) * Life;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // des plus vieilles (en haut, derrière) aux plus jeunes, qui sortent de la cheminée
        int last = (int)(_time / Period);
        for (int i = last - (int)(Life / Period); i <= last; i++)
        {
            float age = _time - i * Period;
            if (age < 0 || age >= Life)
                continue;
            float p = age / Life;
            int puff = ((i % Puffs) + Puffs) % Puffs;
            float rise = Rise * (1 - MathF.Pow(1 - p, 1.6f));
            float sway = Drift * p * p + 1.5f * MathF.Sin(age * 1.4f + Noise(_seed, puff) * 6);
            float radius = 1.5f + 6f * MathF.Sqrt(p) + Noise(_seed + 7, puff) * 0.8f;
            float alpha = MathF.Min(1, age / 0.5f) * MathF.Pow(1 - p, 1.1f) * 0.9f;
            Puff(new Vector2(MathF.Round(sway), MathF.Round(-rise)), radius, alpha, _seed * 131 + puff);
        }
    }

    /// <summary>Une bouffée bosselée, plus claire en haut (la lune), plus sombre en bas, le bord léger.</summary>
    private void Puff(Vector2 at, float radius, float alpha, int seed)
    {
        if (alpha <= 0.02f)
            return;
        int n = Math.Max(1, (int)MathF.Round(radius));
        for (int y = -n; y <= n; y++)
        for (int x = -n; x <= n; x++)
        {
            float d = (float)(x * x + y * y) / (n * n) + (Noise(seed, x * 17 + y * 5) - 0.5f) * 0.35f;
            if (d >= 1)
                continue;
            var c = y < -n * 0.3f || x > n * 0.4f ? Light : y > n * 0.35f ? Dark : Mid;
            DrawRect(new Rect2((at + new Vector2(x, y)) * ArtScale, new Vector2(ArtScale, ArtScale)),
                c with { A = alpha * (d > 0.6f ? 0.5f : 1) });
        }
    }

    /// <summary>Un nombre entre 0 et 1, toujours le même pour les mêmes entrées.</summary>
    private static float Noise(int seed, int n)
    {
        uint h = (uint)(seed * 374761393 + n * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }
}
