using System;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Point d'entrée du jeu : crée la partie, les calques de la carte, la caméra et l'interface,
/// puis gère les commandes du joueur.
///
/// Commandes :
///   Clic gauche sur une case : affiche le chemin. Second clic : le héros part.
///   Espace : continuer le chemin tracé.        Clic droit / Échap : annuler le chemin.
///   Flèches / ZQSD / souris au bord : défilement.   C : recentrer sur le héros.
///   Entrée ou bouton : fin du tour.
///
/// Lancer avec une graine fixe : ajouter « -- --seed=42 » à la ligne de commande.
/// </summary>
public partial class Main : Node2D
{
    private const int T = MapRenderer.TileSize;
    private const double StepDuration = 0.09; // secondes par case pendant la marche
    private const float ScrollSpeed = 700;

    private GameState _state = null!;
    private ActorLayer _actors = null!;
    private Camera2D _camera = null!;
    private Hud _hud = null!;

    private Vector2 _viewSize;   // taille de la zone de carte visible, en pixels
    private Vector2 _cameraPos;  // coin haut-gauche de la carte visible, en pixels
    private bool _walking;
    private double _stepTimer;

    public override void _Ready()
    {
        _state = new GameState(ReadSeed());
        GD.Print($"Carte générée avec la graine {_state.Seed}");

        var screen = GetViewportRect().Size;
        _viewSize = new Vector2(screen.X - Hud.PanelWidth, screen.Y - Hud.TopBarHeight);

        AddChild(new TerrainLayer { Map = _state.Map });
        _actors = new ActorLayer { State = _state };
        AddChild(_actors);
        AddChild(new FogLayer { Map = _state.Map });

        _camera = new Camera2D { AnchorMode = Camera2D.AnchorModeEnum.FixedTopLeft };
        AddChild(_camera);
        _camera.MakeCurrent();

        _hud = new Hud();
        AddChild(_hud);
        _hud.Build(_state, screen);
        _hud.EndTurnPressed += EndTurn;
        _hud.Minimap.Clicked += cell => MoveCameraTo(cell * T - _viewSize / 2);

        CenterOnHero();
    }

    /// <summary>Graine passée en argument (--seed=42), sinon une graine au hasard.</summary>
    private static int ReadSeed()
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--seed="));
        return arg != null && int.TryParse(arg["--seed=".Length..], out int seed)
            ? seed
            : Random.Shared.Next(1_000_000);
    }

    // ------------------------------------------------------------------ caméra

    private void MoveCameraTo(Vector2 topLeft)
    {
        var mapSize = new Vector2(_state.Map.Width, _state.Map.Height) * T;
        _cameraPos = topLeft.Clamp(Vector2.Zero, (mapSize - _viewSize).Max(Vector2.Zero));
        // La caméra montre aussi la barre du haut : on la décale d'autant
        _camera.Position = _cameraPos - new Vector2(0, Hud.TopBarHeight);
        _hud.Minimap.VisibleCells = new Rect2(_cameraPos / T, _viewSize / T);
    }

    private void CenterOnHero()
    {
        var hero = _state.Hero.Position;
        MoveCameraTo(new Vector2(hero.X + 0.5f, hero.Y + 0.5f) * T - _viewSize / 2);
    }

    /// <summary>Recentre la vue quand le héros approche du bord.</summary>
    private void FollowHero()
    {
        var onScreen = ActorLayer.ToPixels(_state.Hero.Position) - _cameraPos;
        const float margin = 3 * T;
        if (onScreen.X < margin || onScreen.Y < margin
            || onScreen.X > _viewSize.X - margin || onScreen.Y > _viewSize.Y - margin)
            CenterOnHero();
    }

    /// <summary>Case sous la souris, ou null si la souris est sur l'interface ou hors de la carte.</summary>
    private Cell? HoveredCell()
    {
        var mouse = GetViewport().GetMousePosition();
        if (mouse.X >= _viewSize.X || mouse.Y < Hud.TopBarHeight)
            return null;
        var world = GetGlobalMousePosition();
        var cell = new Cell(Mathf.FloorToInt(world.X / T), Mathf.FloorToInt(world.Y / T));
        return _state.Map.InBounds(cell) ? cell : null;
    }

    // ------------------------------------------------------------------ règles

    private void EndTurn()
    {
        if (_walking)
            return;
        _state.EndTurn();
    }

    private void ClickCell(Cell cell)
    {
        if (_walking || !_state.Map.IsExplored(cell))
            return;
        if (_state.Path is { Count: > 0 } path && path[^1] == cell)
            StartWalking();
        else
            _state.SetDestination(cell);
    }

    private void StartWalking()
    {
        if (_state.CanStartWalking)
        {
            _walking = true;
            _stepTimer = 0;
        }
        else if (_state.Path != null)
        {
            _state.AddMessage("Plus assez de points de mouvement aujourd'hui.");
        }
    }

    // ------------------------------------------------------------------ boucle

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
                if (HoveredCell() is { } cell)
                    ClickCell(cell);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                if (!_walking)
                    _state.ClearPath();
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                switch (key.Keycode)
                {
                    case Key.Enter or Key.KpEnter: EndTurn(); break;
                    case Key.Space: StartWalking(); break;
                    case Key.Escape when !_walking: _state.ClearPath(); break;
                    case Key.C: CenterOnHero(); break;
                }
                break;
        }
    }

    public override void _Process(double delta)
    {
        ScrollCamera((float)delta);
        _actors.Hovered = HoveredCell();
        _hud.Refresh(_actors.Hovered);

        if (!_walking)
            return;
        _stepTimer -= delta;
        if (_stepTimer > 0)
            return;
        _stepTimer = StepDuration;
        if (!_state.StepAlongPath() || _state.Path == null)
            _walking = false;
        FollowHero();
    }

    private void ScrollCamera(float delta)
    {
        var direction = Vector2.Zero;
        var mouse = GetViewport().GetMousePosition();
        bool edges = GetWindow().HasFocus() && GetViewport().GetVisibleRect().HasPoint(mouse);

        // Touches physiques : ZQSD sur un clavier AZERTY = WASD sur un clavier QWERTY
        if (Input.IsKeyPressed(Key.Left) || Input.IsPhysicalKeyPressed(Key.A) || (edges && mouse.X < 4))
            direction.X -= 1;
        if (Input.IsKeyPressed(Key.Right) || Input.IsPhysicalKeyPressed(Key.D) || (edges && mouse.X > _viewSize.X - 4 && mouse.X < _viewSize.X))
            direction.X += 1;
        if (Input.IsKeyPressed(Key.Up) || Input.IsPhysicalKeyPressed(Key.W) || (edges && mouse.Y < 4))
            direction.Y -= 1;
        if (Input.IsKeyPressed(Key.Down) || Input.IsPhysicalKeyPressed(Key.S) || (edges && mouse.Y > GetViewportRect().Size.Y - 4 && mouse.X < _viewSize.X))
            direction.Y += 1;

        if (direction != Vector2.Zero)
            MoveCameraTo(_cameraPos + direction * ScrollSpeed * delta);
    }
}
