using System;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Le sol, dessiné une seule fois (le terrain ne change pas pendant la partie). Les arbres et les montagnes
/// sont dessinés par-dessus, par <see cref="DecorLayer"/>.
/// </summary>
public partial class TerrainLayer : Node2D
{
    public AdventureMap Map { get; set; } = null!;

    /// <summary>Le sol aux bords naturels (shader), ou null : le sol est alors dessiné case par case.</summary>
    private SmoothGround? _smooth;

    public override void _Ready()
    {
        // Pixel art : les images agrandies restent nettes
        TextureFilter = TextureFilterEnum.Nearest;
        _smooth = SmoothGround.Create(Map);
        if (_smooth != null)
            AddChild(_smooth);
    }

    public override void _Draw()
    {
        if (_smooth != null)
            return;
        var rng = new Random(1);
        for (int y = 0; y < Map.Height; y++)
        for (int x = 0; x < Map.Width; x++)
            MapRenderer.DrawTile(this, Map.GetTerrain(new Cell(x, y)), new Vector2(x, y) * MapRenderer.TileSize, rng);
    }
}
