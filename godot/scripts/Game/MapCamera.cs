using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// La caméra de la carte d'aventure : position, défilement (clavier, bord de l'écran),
/// recentrage sur le héros, zoom. La zone visible est ce qui reste à côté du panneau et sous la barre du haut.
/// Le zoom garde des pixels d'art entiers : ×1 montre les images ×2 (un pixel d'art = 2 pixels de l'écran),
/// ×1,5 les montre ×3, comme la carte du premier Heroes.
/// </summary>
public partial class MapCamera : Camera2D
{
    private const int T = MapRenderer.TileSize;
    private const float ScrollSpeed = 700;

    /// <summary>Les niveaux de zoom (molette de la souris, touches + et -).</summary>
    public static readonly float[] Zooms = { 1f, 1.5f };

    private AdventureMap _map = null!;
    private Minimap _minimap = null!;
    private int _zoomIndex;

    /// <summary>Taille de la zone de carte visible à l'écran, en pixels de l'écran.</summary>
    private Vector2 _screenView;

    /// <summary>Taille de la zone de carte visible, en pixels de la carte (elle diminue quand on zoome).</summary>
    public Vector2 ViewSize { get; private set; }

    /// <summary>Coin haut-gauche de la carte visible, en pixels.</summary>
    public Vector2 TopLeft { get; private set; }

    public void Setup(AdventureMap map, Vector2 screenSize, Minimap minimap)
    {
        _map = map;
        _minimap = minimap;
        AnchorMode = AnchorModeEnum.FixedTopLeft;
        _screenView = new Vector2(screenSize.X - Hud.PanelWidth, screenSize.Y - Hud.TopBarHeight);
        ViewSize = _screenView;
        MakeCurrent();
    }

    /// <summary>
    /// Zoome d'un cran (+1) ou dézoome (-1), en gardant immobile le point de la carte sous
    /// <paramref name="screenPoint"/> (la souris), ou le centre de la vue.
    /// </summary>
    public void ZoomBy(int step, Vector2? screenPoint = null)
    {
        int index = Mathf.Clamp(_zoomIndex + step, 0, Zooms.Length - 1);
        if (index == _zoomIndex)
            return;
        // le point fixe, mesuré depuis le coin haut-gauche de la vue
        var anchor = screenPoint is { } point && point.X < _screenView.X && point.Y >= Hud.TopBarHeight
            ? point - new Vector2(0, Hud.TopBarHeight)
            : _screenView / 2;
        var fixedPoint = TopLeft + anchor / Zooms[_zoomIndex];
        _zoomIndex = index;
        Zoom = new Vector2(Zooms[index], Zooms[index]);
        ViewSize = _screenView / Zooms[index];
        MoveTo(fixedPoint - anchor / Zooms[index]);
    }

    public void MoveTo(Vector2 topLeft)
    {
        var mapSize = new Vector2(_map.Width, _map.Height) * T;
        float zoom = Zooms[_zoomIndex];
        // calé sur les pixels de l'écran, pour que chaque pixel d'art ait toujours la même taille
        TopLeft = (topLeft.Clamp(Vector2.Zero, (mapSize - ViewSize).Max(Vector2.Zero)) * zoom).Round() / zoom;
        // La caméra montre aussi la barre du haut : on la décale d'autant
        Position = TopLeft - new Vector2(0, Hud.TopBarHeight / zoom);
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
        if (Input.IsKeyPressed(Key.Right) || Input.IsPhysicalKeyPressed(Key.D) || (edges && mouse.X > _screenView.X - 4 && mouse.X < _screenView.X))
            direction.X += 1;
        if (Input.IsKeyPressed(Key.Up) || Input.IsPhysicalKeyPressed(Key.W) || (edges && mouse.Y < 4))
            direction.Y -= 1;
        if (Input.IsKeyPressed(Key.Down) || Input.IsPhysicalKeyPressed(Key.S) || (edges && mouse.Y > screen.Y - 4 && mouse.X < _screenView.X))
            direction.Y += 1;

        if (direction != Vector2.Zero)
            MoveTo(TopLeft + direction * ScrollSpeed / Zooms[_zoomIndex] * delta); // même vitesse à l'écran quel que soit le zoom
    }

    /// <summary>Case sous la souris, ou null si la souris est sur l'interface ou hors de la carte.</summary>
    public Cell? HoveredCell()
    {
        var mouse = GetViewport().GetMousePosition();
        if (mouse.X >= _screenView.X || mouse.Y < Hud.TopBarHeight)
            return null;
        var world = GetGlobalMousePosition();
        var cell = new Cell(Mathf.FloorToInt(world.X / T), Mathf.FloorToInt(world.Y / T));
        return _map.InBounds(cell) ? cell : null;
    }
}
