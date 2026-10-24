using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>Le brouillard de guerre, redessiné seulement quand il recule.</summary>
public partial class FogLayer : Node2D
{
    public AdventureMap Map { get; set; } = null!;
    private int _drawnVersion = -1;

    public override void _Process(double delta)
    {
        if (Map.FogVersion != _drawnVersion)
            QueueRedraw();
    }

    public override void _Draw()
    {
        _drawnVersion = Map.FogVersion;
        const int t = MapRenderer.TileSize;
        for (int y = 0; y < Map.Height; y++)
        for (int x = 0; x < Map.Width; x++)
            if (!Map.IsExplored(new Cell(x, y)))
                DrawRect(new Rect2(x * t, y * t, t, t), MapRenderer.FogColor);
    }
}
