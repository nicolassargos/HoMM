using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>Ce qui bouge : objets, chemin prévu, héros, case survolée. Redessiné à chaque image.</summary>
public partial class ActorLayer : Node2D
{
    public GameState State { get; set; } = null!;
    public Cell? Hovered { get; set; }
    private double _time;

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

        foreach (var hero in State.AllHeroes)
            MapRenderer.DrawHero(this, ToPixels(hero.Position) + half + new Vector2(0, 4), _time);

        if (Hovered is { } h && State.CurrentPlayer.Fog.IsExplored(h))
            DrawRect(new Rect2(ToPixels(h), t, t), Colors.White, false, 1);
    }

    public static Vector2 ToPixels(Cell c) => new Vector2(c.X, c.Y) * MapRenderer.TileSize;
}
