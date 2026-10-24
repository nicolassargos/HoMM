using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>Le brouillard de guerre du joueur courant, redessiné seulement quand il recule.</summary>
public partial class FogLayer : Node2D
{
    public GameState State { get; set; } = null!;
    private FogOfWar? _drawnFog;
    private int _drawnVersion = -1;

    public override void _Process(double delta)
    {
        var fog = State.CurrentPlayer.Fog;
        if (fog != _drawnFog || fog.Version != _drawnVersion)
            QueueRedraw();
    }

    public override void _Draw()
    {
        var fog = State.CurrentPlayer.Fog;
        _drawnFog = fog;
        _drawnVersion = fog.Version;
        const int t = MapRenderer.TileSize;
        for (int y = 0; y < fog.Height; y++)
        for (int x = 0; x < fog.Width; x++)
            if (!fog.IsExplored(new Cell(x, y)))
                DrawRect(new Rect2(x * t, y * t, t, t), MapRenderer.FogColor);
    }
}
