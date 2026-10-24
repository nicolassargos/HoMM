using System;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Point d'entrée du jeu : crée la partie, les calques de la carte, la caméra, le panneau
/// et l'écran de ville, puis relie les commandes du joueur aux règles.
///
/// Commandes :
///   Clic gauche sur une case : affiche le chemin. Second clic : le héros part.
///   Espace : continuer le chemin tracé.        Clic droit / Échap : annuler le chemin.
///   Flèches / ZQSD / souris au bord : défilement.   C : recentrer sur le héros.
///   Clic sur les murs de sa ville, liste des villes du panneau, ou T : écran de ville.
///   Clic sur un de ses héros, liste des héros du panneau, ou H : changer de héros.
///   Entrée ou bouton : fin du tour.
///
/// Lancer avec une graine fixe : ajouter « -- --seed=42 » à la ligne de commande.
/// </summary>
public partial class Main : Node2D
{
    private const double StepDuration = 0.09; // secondes par case pendant la marche

    private GameState _state = null!;
    private ActorLayer _actors = null!;
    private MapCamera _camera = null!;
    private Hud _hud = null!;
    private TownScreen _townScreen = null!;

    private bool _walking;
    private double _stepTimer;

    /// <summary>Ville à ouvrir dès que les fenêtres (compte rendu de combat…) sont fermées.</summary>
    private Town? _townToOpen;

    public override void _Ready()
    {
        _state = new GameState(ReadSeed(), LoadData());
        GD.Print($"Carte générée avec la graine {_state.Seed}");
        // Option de développement : « -- --reveal » retire le brouillard
        if (OS.GetCmdlineUserArgs().Contains("--reveal"))
            _state.CurrentPlayer.Fog.Reveal(new Cell(_state.Map.Width / 2, _state.Map.Height / 2), _state.Map.Width + _state.Map.Height);

        var screen = GetViewportRect().Size;

        AddChild(new TerrainLayer { Map = _state.Map });
        _actors = new ActorLayer { State = _state };
        AddChild(_actors);
        AddChild(new FogLayer { State = _state });

        _hud = new Hud();
        AddChild(_hud);
        _hud.Build(_state, screen);
        _hud.EndTurnPressed += EndTurn;
        _hud.TownClicked += OpenTown;
        _hud.HeroClicked += SelectHero;

        _camera = new MapCamera();
        AddChild(_camera);
        _camera.Setup(_state.Map, screen, _hud.Minimap);
        _hud.Minimap.Clicked += cells => _camera.CenterOn(cells);

        _townScreen = new TownScreen();
        AddChild(_townScreen);
        _townScreen.Build(_state, screen);

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

    /// <summary>Les données du jeu, décrites dans le dossier data/ (créatures, bâtiments).</summary>
    private static GameData LoadData() => GameData.FromJson(
        Godot.FileAccess.GetFileAsString("res://data/creatures.json"),
        Godot.FileAccess.GetFileAsString("res://data/buildings.json"));

    /// <summary>Le joueur regarde la carte (pas de fenêtre ni d'écran de ville ouverts).</summary>
    private bool OnMap => !_hud.IsDialogOpen && !_townScreen.IsOpen;

    /// <summary>Les commandes de la carte sont ignorées pendant la marche, hors de la carte, ou une fois la partie finie.</summary>
    private bool CanAct => !_walking && OnMap && _state.Outcome == GameOutcome.Playing;

    private void CenterOnHero()
    {
        if (_state.CurrentHero is { } hero)
            _camera.CenterOn(hero.Position);
    }

    // ------------------------------------------------------------------ règles

    private void EndTurn()
    {
        if (!CanAct)
            return;
        _state.EndTurn();
    }

    private void ClickCell(Cell cell)
    {
        if (!CanAct || !_state.CurrentPlayer.Fog.IsExplored(cell))
            return;
        // Clic sur sa ville : les murs ouvrent l'écran de ville ; l'entrée y fait entrer le héros
        // (ou ouvre l'écran s'il y est déjà)
        // Clic sur un autre de ses héros : il devient le héros sélectionné
        if (_state.CurrentPlayer.Heroes.FirstOrDefault(h => h.Position == cell) is { } clicked && clicked != _state.CurrentHero)
        {
            SelectHero(clicked);
            return;
        }
        if (_state.Map.OccupantAt(cell) is Town town && town.Owner == _state.CurrentPlayer
            && (cell != town.Position || _state.CurrentHero?.Position == town.Position))
        {
            OpenTown(town);
            return;
        }
        if (_state.Path is { Count: > 0 } path && path[^1] == cell)
            StartWalking();
        else
            _state.SetDestination(cell);
    }

    /// <summary>Sélectionne un héros (liste du panneau, clic sur la carte, touche H) et centre la vue sur lui.</summary>
    private void SelectHero(Hero hero)
    {
        if (!CanAct)
            return;
        _state.SelectHero(hero);
        CenterOnHero();
    }

    /// <summary>Ouvre l'écran d'une ville du joueur (depuis la carte, la liste des villes ou la touche T).</summary>
    private void OpenTown(Town town)
    {
        if (!CanAct || town.Owner != _state.CurrentPlayer)
            return;
        _townScreen.Open(town);
    }

    private void StartWalking()
    {
        if (!CanAct)
            return;
        if (_state.CanStartWalking)
        {
            _walking = true;
            _stepTimer = 0;
        }
        else if (_state.Path != null)
        {
            _hud.AddMessage(Texts.NotEnoughMovement);
        }
    }

    // ------------------------------------------------------------------ boucle

    public override void _UnhandledInput(InputEvent e)
    {
        if (!OnMap)
            return;
        switch (e)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
                if (_camera.HoveredCell() is { } cell)
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
                    case Key.T when _state.CurrentPlayer.Towns.FirstOrDefault() is { } town: OpenTown(town); break;
                    case Key.H when CanAct:
                        _state.SelectNextHero();
                        CenterOnHero();
                        break;
                }
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (OnMap)
            _camera.Scroll((float)delta);
        _actors.Hovered = OnMap ? _camera.HoveredCell() : null;
        _hud.Refresh(_actors.Hovered);

        if (_walking)
        {
            _stepTimer -= delta;
            if (_stepTimer <= 0)
            {
                _stepTimer = StepDuration;
                if (!_state.StepAlongPath() || _state.Path == null)
                    _walking = false;
            }
        }

        HandleEvents();

        if (_townToOpen != null && !_hud.IsDialogOpen && _state.Outcome == GameOutcome.Playing)
        {
            _townScreen.Open(_townToOpen);
            _townToOpen = null;
        }
    }

    /// <summary>Montre à l'écran ce que les règles viennent de faire.</summary>
    private void HandleEvents()
    {
        var viewer = _state.Players[0];
        foreach (var e in _state.TakeEvents())
        {
            if (Texts.Message(e, viewer) is { } message)
                _hud.AddMessage(message);

            switch (e)
            {
                case HeroMoved moved when moved.Hero == _state.CurrentHero:
                    _camera.Follow(moved.To);
                    break;
                case BattleFought battle:
                    _walking = false;
                    _hud.ShowBattle(battle);
                    break;
                case TownEntered entered when entered.Hero.Owner == viewer:
                    _townToOpen = entered.Town;
                    break;
                case TownCaptured captured when captured.Hero.Owner == viewer:
                    _townToOpen = captured.Town;
                    break;
                case GameEnded end:
                    _walking = false;
                    _townScreen.Close();
                    _hud.ShowGameOver(end.Outcome, () => GetTree().ReloadCurrentScene());
                    break;
            }
        }
    }
}
