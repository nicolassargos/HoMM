using System;
using System.Collections.Generic;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>Ce qui bouge : objets, chemin prévu, héros. Redessiné à chaque image.</summary>
public partial class ActorLayer : Node2D
{
    public GameState State { get; set; } = null!;

    /// <summary>La case sous la souris : le panneau la décrit (elle n'est plus encadrée sur la carte).</summary>
    public Cell? Hovered { get; set; }
    private double _time;

    // Pixel art : les images agrandies restent nettes
    public override void _Ready() => TextureFilter = TextureFilterEnum.Nearest;

    /// <summary>
    /// Développement (« --hero-walk ») : le héros choisi fait sans cesse le tour d'un octogone, un pas dans chacune des
    /// 8 directions (montré seulement : les règles ne le déplacent pas).
    /// </summary>
    public bool WalkPreview { get; set; }

    private static readonly Cell[] Octagon = { new(0, -1), new(1, -1), new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1) };

    public override void _Process(double delta)
    {
        _time += delta;
        if (WalkPreview && State.CurrentHero is { } walker)
        {
            var at = walker.Position;
            for (int i = 0; i < (int)(_time / MapRenderer.HeroStepTime) % Octagon.Length; i++)
                at = new Cell(at.X + Octagon[i].X, at.Y + Octagon[i].Y);
            ShownAt[walker] = at;
        }
        QueueRedraw();
    }

    /// <summary>
    /// Où montrer un héros adverse pendant que ses déplacements sont rejoués (il a déjà fini son tour dans les règles) ;
    /// les autres héros sont montrés là où ils sont.
    /// </summary>
    public Dictionary<Hero, Cell> ShownAt { get; } = new();

    /// <summary>
    /// Pour chaque héros, son dernier pas : d'où, vers où, quand il a commencé, où il regarde, et le
    /// nombre de pas de suite (la marche fait un cycle d'images en deux pas) ; <c>Ahead</c> : un pas commencé avant que
    /// les règles ne déplacent le héros (<see cref="BeginStep"/>).
    /// </summary>
    private readonly Dictionary<Hero, (Cell From, Cell To, double Since, MapRenderer.HeroFacing Facing, int Steps, bool Ahead)> _motion = new();

    /// <summary>
    /// Le héros du joueur commence un pas vers <paramref name="to"/> : il y glisse pendant <see cref="MapRenderer.HeroStepTime"/>,
    /// et les règles ne l'y mettent qu'à la fin (une rencontre arrive quand il y est).
    /// </summary>
    public void BeginStep(Hero hero, Cell to) => Step(hero, to, ahead: true);

    private void Step(Hero hero, Cell to, bool ahead)
    {
        var at = ShownAt.TryGetValue(hero, out var shown) ? shown : hero.Position;
        var (from, left, steps) = (at, false, 0);
        // (un saut de plus d'une case depuis le dernier pas, un portail : la marche repart d'où il est)
        if (_motion.TryGetValue(hero, out var motion) && Math.Abs(motion.To.X - at.X) <= 1 && Math.Abs(motion.To.Y - at.Y) <= 1)
        {
            from = motion.To;
            left = motion.Facing.Left;
            // une marche qui continue garde son cycle d'images
            steps = _time - motion.Since < MapRenderer.HeroStepTime * 1.5 ? motion.Steps + 1 : 0;
        }
        left = to.X < from.X || (to.X == from.X && left);
        _motion[hero] = (from, to, _time, new(Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y), left), steps, ahead);
    }

    /// <summary>
    /// Où montrer un héros (en pixels, le coin de sa case), et comment : tourné vers où il est allé en dernier, et
    /// l'image de sa marche pendant un pas (-1 : l'image fixe).
    /// </summary>
    public (Vector2 At, MapRenderer.HeroFacing Facing, int Frame) HeroPose(Hero hero)
    {
        var at = ShownAt.TryGetValue(hero, out var shown) ? shown : hero.Position;
        if (!_motion.TryGetValue(hero, out var motion))
            motion = _motion[hero] = (at, at, double.NegativeInfinity, default, 0, false);
        // un pas commencé attend les règles un peu plus que sa durée : elles y mettent le héros une image plus tard (sinon le
        // pas était oublié, et le suivant, partant de la case d'avant, se jouait à l'envers)
        else if (motion.To != at && !(motion.Ahead && motion.From == at && _time - motion.Since < MapRenderer.HeroStepTime * 1.5))
        {
            // un pas que les règles ont déjà fait (les adversaires rejoués), ou un pas commencé que les règles n'ont pas
            // fait (le héros reste) ; un saut de plus d'une case (chargement, nouveau tour) n'est pas une marche
            bool step = Math.Abs(at.X - motion.To.X) <= 1 && Math.Abs(at.Y - motion.To.Y) <= 1;
            if (step && !(motion.Ahead && at == motion.From))
                Step(hero, at, ahead: false);
            else
                _motion[hero] = (at, at, double.NegativeInfinity, motion.Facing, 0, false);
            motion = _motion[hero];
        }
        double p = (_time - motion.Since) / MapRenderer.HeroStepTime;
        if (p >= 1)
            return (ToPixels(motion.To), motion.Facing, -1);
        var from = ToPixels(motion.From);
        var pixels = from + (ToPixels(motion.To) - from) * (float)MapRenderer.HeroStepShare(p);
        // sur la grille des pixels d'art (2 pixels de l'écran)
        pixels = (pixels / 2).Round() * 2;
        int frames = MapRenderer.HeroWalkFrames(hero, motion.Facing);
        int frame = frames > 0 ? (int)((motion.Steps + p) / 2 * frames) % frames : -1;
        return (pixels, motion.Facing, frame);
    }

    public override void _Draw()
    {
        const int t = MapRenderer.TileSize;
        var half = new Vector2(t / 2f, t / 2f);

        foreach (var obj in State.Map.Objects)
            MapRenderer.DrawObject(this, obj, ToPixels(obj.Position), _time);

        // le chemin prévu et l'anneau du héros choisi : seulement ceux du joueur (pas pendant le tour d'un adversaire)
        bool mine = !State.CurrentPlayer.IsComputer;
        if (mine && State.Path is { } path)
        {
            var reachable = State.PathReachability();
            for (int i = 0; i < path.Count; i++)
                MapRenderer.DrawPathMarker(this, ToPixels(path[i]) + half, reachable[i], i == path.Count - 1);
        }

        // Le héros sélectionné a un anneau doré à ses pieds
        if (mine && State.CurrentHero is { } selected)
            MapRenderer.DrawSelection(this, HeroPose(selected).At + half + new Vector2(0, 14 * t / 48f), _time);
        foreach (var hero in State.AllHeroes)
        {
            var (at, facing, frame) = HeroPose(hero);
            MapRenderer.DrawHero(this, at + half + new Vector2(0, 4), _time, hero, facing, frame, hero.AtSea(State.Map));
        }
        // (la case survolée n'est plus encadrée : le panneau la décrit, cela suffit)
    }

    public static Vector2 ToPixels(Cell c) => new Vector2(c.X, c.Y) * MapRenderer.TileSize;
}
