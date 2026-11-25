using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Le chemin prévu du héros choisi (seulement pendant le tour du joueur), dessiné par-dessus le décor, les objets et les
/// héros : il reste toujours visible, même derrière un arbre ou une ville.
/// </summary>
public partial class PathLayer : Node2D
{
    public GameState State { get; set; } = null!;

    public override void _Ready() => TextureFilter = TextureFilterEnum.Nearest;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (State.CurrentPlayer.IsComputer || State.Path is not { } path)
            return;
        var half = new Vector2(MapRenderer.TileSize / 2f, MapRenderer.TileSize / 2f);
        var reachable = State.PathReachability();
        for (int i = 0; i < path.Count; i++)
            MapRenderer.DrawPathMarker(this, ActorLayer.ToPixels(path[i]) + half, reachable[i], i == path.Count - 1);
    }
}
