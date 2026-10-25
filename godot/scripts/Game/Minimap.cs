using System;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>Mini-carte : terrain exploré, mines, héros et zone visible. Un clic y déplace la vue.</summary>
public partial class Minimap : Control
{
    public GameState State { get; set; } = null!;

    /// <summary>Taille d'une case de la carte sur la mini-carte, en pixels.</summary>
    public int CellSize { get; set; } = 6;

    /// <summary>Partie de la carte visible à l'écran, en cases.</summary>
    public Rect2 VisibleCells { get; set; }

    /// <summary>Déclenché quand on clique : position visée sur la carte, en cases.</summary>
    public event Action<Vector2>? Clicked;

    public override void _Process(double delta) => QueueRedraw();

    public override void _GuiInput(InputEvent e)
    {
        bool click = e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
        bool drag = e is InputEventMouseMotion motion && motion.ButtonMask.HasFlag(MouseButtonMask.Left);
        if (click || drag)
        {
            Clicked?.Invoke(((InputEventMouse)e).Position / CellSize);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var map = State.Map;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var cell = new Cell(x, y);
            Color color = MapRenderer.FogColor;
            if (State.CurrentPlayer.Fog.IsExplored(cell))
            {
                color = MapRenderer.TerrainColor(map.GetTerrain(cell));
                if (map.ObjectAt(cell) is Mine mine)
                    color = mine.Owner != null ? MapRenderer.PlayerColor : MapRenderer.Rgb(230, 230, 230);
                else if (map.OccupantAt(cell) is Town town)
                    color = town.Owner != null ? MapRenderer.PlayerColor : MapRenderer.NeutralColor;
            }
            DrawRect(new Rect2(x * CellSize, y * CellSize, CellSize, CellSize), color);
        }

        foreach (var h in State.CurrentPlayer.Heroes)
        {
            var hero = h.Position;
            DrawRect(new Rect2(hero.X * CellSize - 1, hero.Y * CellSize - 1, CellSize + 2, CellSize + 2), Colors.Yellow);
        }
        DrawRect(new Rect2(VisibleCells.Position * CellSize, VisibleCells.Size * CellSize), Colors.White, false, 1);
    }
}
