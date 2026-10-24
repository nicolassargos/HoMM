using System;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>Le terrain, dessiné une seule fois (le terrain ne change pas pendant la partie).</summary>
public partial class TerrainLayer : Node2D
{
    public AdventureMap Map { get; set; } = null!;

    public override void _Draw()
    {
        var rng = new Random(1);
        for (int y = 0; y < Map.Height; y++)
        for (int x = 0; x < Map.Width; x++)
            MapRenderer.DrawTile(this, Map.GetTerrain(new Cell(x, y)), new Vector2(x, y) * MapRenderer.TileSize, rng);
    }
}
