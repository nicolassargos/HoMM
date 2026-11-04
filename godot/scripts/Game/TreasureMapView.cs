using System;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// La carte au trésor : un petit dessin, comme sur un parchemin, du pays autour du trésor enfoui, avec une croix.
/// Elle est découpée en morceaux, un par obélisque ; ceux que le joueur n'a pas encore trouvés restent cachés.
/// La croix n'est pas au milieu du dessin : il faut reconnaître l'endroit sur la carte, puis y creuser.
/// </summary>
public partial class TreasureMapView : Control
{
    /// <summary>Taille du dessin, en cases de la carte.</summary>
    public const int Columns = 11, Rows = 7;

    /// <summary>Taille d'une case sur le dessin, en pixels.</summary>
    public const int CellSize = 32;

    public GameState State { get; set; } = null!;
    public Player Viewer { get; set; } = null!;

    private static readonly Color Parchment = MapRenderer.Rgb(214, 190, 140);
    private static readonly Color Ink = MapRenderer.Rgb(70, 50, 30);

    public override void _Ready()
    {
        // dessinée en unités de l'interface, agrandie à l'échelle de l'écran (des formes, pas d'images en pixel art)
        CustomMinimumSize = Ui.V(Columns * CellSize, Rows * CellSize);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Le coin haut-gauche du dessin sur la carte : la croix est décalée d'une façon qui dépend du trésor.</summary>
    public static Cell Origin(AdventureMap map, Cell treasure)
    {
        int shiftX = 2 + Math.Abs(treasure.X * 7 + treasure.Y * 3) % (Columns - 4);
        int shiftY = 1 + Math.Abs(treasure.X * 5 + treasure.Y * 11) % (Rows - 2);
        return new Cell(Math.Clamp(treasure.X - shiftX, 0, Math.Max(0, map.Width - Columns)),
            Math.Clamp(treasure.Y - shiftY, 0, Math.Max(0, map.Height - Rows)));
    }

    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero, 0, Vector2.One * Ui.Scale);
        var size = new Vector2(Columns, Rows) * CellSize;
        DrawRect(new Rect2(Vector2.Zero, size), Parchment);
        if (State.Map.Treasure is not { } treasure)
            return;
        var map = State.Map;
        var origin = Origin(map, treasure);

        // Le pays, en couleurs passées
        for (int y = 0; y < Rows; y++)
        for (int x = 0; x < Columns; x++)
        {
            var cell = new Cell(origin.X + x, origin.Y + y);
            if (!map.InBounds(cell))
                continue;
            var o = new Vector2(x, y) * CellSize;
            var terrain = map.GetTerrain(cell);
            var ground = terrain is TerrainType.Forest or TerrainType.Mountain ? TerrainType.Grass : terrain;
            DrawRect(new Rect2(o, CellSize, CellSize), Faded(MapRenderer.TerrainColor(ground)));
            if (map.HasRoad(cell))
                DrawRect(new Rect2(o + new Vector2(11, 11), 10, 10), Faded(MapRenderer.RoadColor));
            if (terrain == TerrainType.Forest)
                for (int i = 0; i < 2; i++)
                {
                    var p = o + new Vector2(9 + i * 14, 24);
                    DrawColoredPolygon(new[] { p + new Vector2(0, -12), p + new Vector2(-5, 0), p + new Vector2(5, 0) }, Faded(MapRenderer.Rgb(40, 90, 40)));
                }
            if (terrain == TerrainType.Mountain)
            {
                var p = o + new Vector2(16, 27);
                DrawColoredPolygon(new[] { p + new Vector2(0, -16), p + new Vector2(-10, 0), p + new Vector2(10, 0) }, Faded(MapRenderer.Rgb(110, 100, 90)));
            }
            if (map.OccupantAt(cell) is Town)
                DrawRect(new Rect2(o + new Vector2(5, 5), 22, 22), Faded(MapRenderer.Rgb(150, 140, 130)));
        }

        // La croix
        var t = new Vector2(treasure.X - origin.X + 0.5f, treasure.Y - origin.Y + 0.5f) * CellSize;
        DrawLine(t + new Vector2(-8, -8), t + new Vector2(8, 8), MapRenderer.Rgb(170, 30, 20), 4);
        DrawLine(t + new Vector2(-8, 8), t + new Vector2(8, -8), MapRenderer.Rgb(170, 30, 20), 4);

        // Les morceaux pas encore trouvés
        var pieces = State.TreasureMapPieces(Viewer);
        if (pieces.Count == 0)
            return;
        int pieceColumns = Math.Min(3, pieces.Count);
        int pieceRows = (pieces.Count + pieceColumns - 1) / pieceColumns;
        var piece = new Vector2(size.X / pieceColumns, size.Y / pieceRows);
        var font = ThemeDB.FallbackFont;
        for (int i = 0; i < pieces.Count; i++)
        {
            var rect = new Rect2(new Vector2(i % pieceColumns, i / pieceColumns) * piece, piece);
            if (!pieces[i])
            {
                DrawRect(rect, Parchment.Darkened(0.15f));
                DrawString(font, rect.GetCenter() + new Vector2(-6, 10), "?", HorizontalAlignment.Left, -1, Ui.TextSize(22), Ink);
            }
            DrawRect(rect, Ink, false, 1);
        }
        DrawRect(new Rect2(Vector2.Zero, size), Ink, false, 2);
    }

    /// <summary>Une couleur du terrain, passée comme sur un vieux parchemin.</summary>
    private static Color Faded(Color color) => color.Lerp(Parchment, 0.45f);
}
