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

    public override void _Process(double delta)
    {
        _time += delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        const int t = MapRenderer.TileSize;
        var half = new Vector2(t / 2f, t / 2f);

        foreach (var obj in State.Map.Objects)
            MapRenderer.DrawObject(this, obj, ToPixels(obj.Position), _time);

        if (State.Path is { } path)
        {
            var reachable = State.PathReachability();
            for (int i = 0; i < path.Count; i++)
                MapRenderer.DrawPathMarker(this, ToPixels(path[i]) + half, reachable[i], i == path.Count - 1);
        }

        // Le héros sélectionné a un anneau doré à ses pieds
        if (State.CurrentHero is { } selected)
            MapRenderer.DrawSelection(this, ToPixels(selected.Position) + half + new Vector2(0, 14), _time);
        foreach (var hero in State.AllHeroes)
            MapRenderer.DrawHero(this, ToPixels(hero.Position) + half + new Vector2(0, 4), _time, hero.Owner);
        // (la case survolée n'est plus encadrée : le panneau la décrit, cela suffit)
    }

    public static Vector2 ToPixels(Cell c) => new Vector2(c.X, c.Y) * MapRenderer.TileSize;
}
