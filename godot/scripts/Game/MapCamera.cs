using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// La caméra de la carte d'aventure : position, défilement (clavier, bord de l'écran),
/// recentrage sur le héros. La zone visible est ce qui reste à côté du panneau et sous la barre du haut.
/// </summary>
public partial class MapCamera : Camera2D
{
    private const int T = MapRenderer.TileSize;
    private const float ScrollSpeed = 700;

    private AdventureMap _map = null!;
    private Minimap _minimap = null!;

    /// <summary>Taille de la zone de carte visible, en pixels.</summary>
    public Vector2 ViewSize { get; private set; }

    /// <summary>Coin haut-gauche de la carte visible, en pixels.</summary>
    public Vector2 TopLeft { get; private set; }

    public void Setup(AdventureMap map, Vector2 screenSize, Minimap minimap)
    {
        _map = map;
        _minimap = minimap;
        AnchorMode = AnchorModeEnum.FixedTopLeft;
        ViewSize = new Vector2(screenSize.X - Hud.PanelWidth, screenSize.Y - Hud.TopBarHeight);
        MakeCurrent();
    }

    public void MoveTo(Vector2 topLeft)
    {
        var mapSize = new Vector2(_map.Width, _map.Height) * T;
        TopLeft = topLeft.Clamp(Vector2.Zero, (mapSize - ViewSize).Max(Vector2.Zero));
        // La caméra montre aussi la barre du haut : on la décale d'autant
        Position = TopLeft - new Vector2(0, Hud.TopBarHeight);
        _minimap.VisibleCells = new Rect2(TopLeft / T, ViewSize / T);
    }

    /// <summary>Centre la vue sur une position de la carte, en cases (peut être fractionnaire).</summary>
    public void CenterOn(Vector2 cells) => MoveTo(cells * T - ViewSize / 2);

    public void CenterOn(Cell cell) => CenterOn(new Vector2(cell.X + 0.5f, cell.Y + 0.5f));

    /// <summary>Recentre la vue quand la case approche du bord.</summary>
    public void Follow(Cell cell)
    {
        var onScreen = ActorLayer.ToPixels(cell) - TopLeft;
        const float margin = 3 * T;
        if (onScreen.X < margin || onScreen.Y < margin
            || onScreen.X > ViewSize.X - margin || onScreen.Y > ViewSize.Y - margin)
            CenterOn(cell);
    }

    /// <summary>Défilement avec les flèches, ZQSD, ou la souris au bord de l'écran.</summary>
    public void Scroll(float delta)
    {
        var direction = Vector2.Zero;
        var mouse = GetViewport().GetMousePosition();
        var screen = GetViewportRect().Size;
        bool edges = GetWindow().HasFocus() && GetViewport().GetVisibleRect().HasPoint(mouse);

        // Touches physiques : ZQSD sur un clavier AZERTY = WASD sur un clavier QWERTY
        if (Input.IsKeyPressed(Key.Left) || Input.IsPhysicalKeyPressed(Key.A) || (edges && mouse.X < 4))
            direction.X -= 1;
        if (Input.IsKeyPressed(Key.Right) || Input.IsPhysicalKeyPressed(Key.D) || (edges && mouse.X > ViewSize.X - 4 && mouse.X < ViewSize.X))
            direction.X += 1;
        if (Input.IsKeyPressed(Key.Up) || Input.IsPhysicalKeyPressed(Key.W) || (edges && mouse.Y < 4))
            direction.Y -= 1;
        if (Input.IsKeyPressed(Key.Down) || Input.IsPhysicalKeyPressed(Key.S) || (edges && mouse.Y > screen.Y - 4 && mouse.X < ViewSize.X))
            direction.Y += 1;

        if (direction != Vector2.Zero)
            MoveTo(TopLeft + direction * ScrollSpeed * delta);
    }

    /// <summary>Case sous la souris, ou null si la souris est sur l'interface ou hors de la carte.</summary>
    public Cell? HoveredCell()
    {
        var mouse = GetViewport().GetMousePosition();
        if (mouse.X >= ViewSize.X || mouse.Y < Hud.TopBarHeight)
            return null;
        var world = GetGlobalMousePosition();
        var cell = new Cell(Mathf.FloorToInt(world.X / T), Mathf.FloorToInt(world.Y / T));
        return _map.InBounds(cell) ? cell : null;
    }
}
