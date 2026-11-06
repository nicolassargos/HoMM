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
///   Espace : continuer le chemin tracé.        Clic droit : annuler le chemin.   Échap : options.
///   Flèches / ZQSD / souris au bord : défilement.   C : recentrer sur le héros.
///   Clic sur les murs de sa ville, liste des villes du panneau, ou T : écran de ville.
///   Clic sur un de ses héros, liste des héros du panneau, ou H : changer de héros.
///   Entrée ou bouton : fin du tour.
///   F5 / F9 : sauvegarde rapide / la reprendre. Échap : options (sauvegarder, charger, menu principal…).
///
/// Lancé sans option, le jeu ouvre le menu principal ; toute option (ou « -- --play ») entre directement dans une
/// partie. Graine fixe : « -- --seed=42 » ; reprendre la dernière sauvegarde : « -- --load ».
/// « --menu » (ou « --menu=new », « --menu=load ») montre le menu même avec d'autres options (captures).
/// Développement : « -- --capture=apercu.png » enregistre une image de l'écran après un instant, puis quitte
/// (avec « --rules », la fenêtre du choix des conditions reste ouverte sur l'image ; avec « --town », l'écran
/// d'une ville avec presque tous ses bâtiments).
/// </summary>
public partial class Main : Node2D
{
    private const double StepDuration = 0.09; // secondes par case pendant la marche

    private GameState _state = null!;
    private ActorLayer _actors = null!;
    private MapCamera _camera = null!;
    private Hud _hud = null!;
    private TownScreen _townScreen = null!;
    private BattleScreen _battleScreen = null!;
    private HeroScreen _heroScreen = null!;
    private KingdomScreen _kingdomScreen = null!;

    /// <summary>Développement (« --battle-test ») : un combat tout de suite, joué sur l'écran de combat sans demander.</summary>
    private bool _battleTest;

    /// <summary>Développement (« --hover=x,y ») : la case survolée pour une capture d'écran.</summary>
    private static readonly Cell? _devHover = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--hover=")) is { } hover
        && hover["--hover=".Length..].Split(',') is [var hx, var hy] && int.TryParse(hx, out int x) && int.TryParse(hy, out int y)
        ? new Cell(x, y) : null;

    private bool _walking;
    private double _stepTimer;

    /// <summary>Ville à ouvrir dès que les fenêtres (compte rendu de combat…) sont fermées.</summary>
    private Town? _townToOpen;

    private SavesWindow _saves = null!;

    /// <summary>La partie à commencer, choisie dans le menu principal (voir <see cref="ShowMainMenu"/>).</summary>
    private static GameSetup? _setup;

    /// <summary>
    /// Montrer le menu principal au prochain lancement de la scène : au démarrage du jeu sans option (« --windowed »
    /// n'en est pas une pour cela), puis quand le joueur y revient.
    /// </summary>
    private static bool _goToMenu = OS.GetCmdlineUserArgs().All(a => a == "--windowed")
        || OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--menu"));

    /// <summary>La scène ne montre que le menu principal (pas de partie).</summary>
    private bool _inMenu;

    /// <summary>Partie chargée, reprise par la scène relancée (voir <see cref="LoadGame"/>).</summary>
    private static GameState? _loadedState;

    /// <summary>Le mode de la fenêtre n'est choisi qu'au lancement (pas à chaque nouvelle partie).</summary>
    private static bool _windowModeSet;

    public override void _Ready()
    {
        // plein écran au lancement (l'écran de 1920 × 1080 passait sous la barre des tâches) ; pas pour une capture
        if (!_windowModeSet)
        {
            _windowModeSet = true;
            if (!OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--capture=") || a == "--windowed"))
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        }
        Ui.UsePixelFont();
        Ui.ApplyTheme();
        _capturePath = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture="))?["--capture=".Length..];
        if (_goToMenu && _loadedState == null)
        {
            _goToMenu = false;
            ShowMainMenu();
            return;
        }
        var data = LoadData();
        var loaded = _loadedState
            ?? (OS.GetCmdlineUserArgs().Contains("--load") && SaveFiles.Latest() is { } latest ? SaveFiles.Read(latest.Path, data) : null);
        _loadedState = null;
        var setup = _setup;
        _setup = null;
        _state = loaded ?? (setup != null ? new GameState(setup.Seed, data, setup.Size) : new GameState(ReadSeed(), data, ReadSize()));
        _state.QuickBattles = false; // chaque combat s'arrête : le joueur le dirige ou choisit le combat rapide
        GD.Print(loaded != null ? "Partie chargée" : $"Carte générée avec la graine {_state.Seed}");
        // Option de développement : « -- --reveal » retire le brouillard
        if (OS.GetCmdlineUserArgs().Contains("--reveal"))
            _state.CurrentPlayer.Fog.Reveal(new Cell(_state.Map.Width / 2, _state.Map.Height / 2), _state.Map.Width + _state.Map.Height);

        var screen = GetViewportRect().Size;
        if (OS.GetCmdlineUserArgs().Contains("--terrain-test"))
            PaintTerrainTest();

        AddChild(new TerrainLayer { Map = _state.Map });
        AddChild(new DecorLayer { Map = _state.Map });
        _actors = new ActorLayer { State = _state };
        AddChild(_actors);
        AddChild(new FogLayer { State = _state });

        _hud = new Hud();
        AddChild(_hud);
        _hud.Build(_state, screen);
        _hud.EndTurnPressed += EndTurn;
        _hud.SavePressed += () => _saves.OpenToSave(Texts.SuggestedSaveName(_state.CurrentHero?.Name, _state.Day));
        _hud.DigPressed += Dig;
        _hud.TreasureMapPressed += () => _hud.ShowTreasureMap(_state, _state.Players[0]);
        _hud.LoadPressed += () => _saves.OpenToLoad();
        _hud.QuitPressed += () => GetTree().Quit();
        _hud.MainMenuPressed += GoToMainMenu;
        _hud.TownClicked += OpenTown;
        _hud.HeroClicked += SelectHero;
        _hud.NextHeroPressed += NextHero;
        _hud.WalkPressed += StartWalking;
        _hud.CenterPressed += CenterOnHero;

        _camera = new MapCamera();
        AddChild(_camera);
        _camera.Setup(_state.Map, screen, _hud.Minimap);
        _hud.Minimap.Clicked += cells => _camera.CenterOn(cells);

        _battleScreen = new BattleScreen();
        AddChild(_battleScreen);
        _battleScreen.Build(screen);

        // l'écran du héros et le royaume, en grandes fenêtres par-dessus la carte
        _heroScreen = new HeroScreen();
        AddChild(_heroScreen);
        _heroScreen.Build(_state, screen);
        _hud.HeroDetailsPressed += OpenHeroScreen;
        _kingdomScreen = new KingdomScreen();
        AddChild(_kingdomScreen);
        _kingdomScreen.Build(_state, screen);
        _hud.KingdomPressed += OpenKingdom;
        _kingdomScreen.HeroChosen += hero =>
        {
            _kingdomScreen.Close();
            SelectHero(hero);
        };
        _kingdomScreen.HeroDetailsWanted += hero =>
        {
            _kingdomScreen.Close();
            OpenHeroScreen(hero);
        };
        _kingdomScreen.TownChosen += town =>
        {
            _kingdomScreen.Close();
            OpenTown(town);
        };
        _kingdomScreen.MineChosen += cell =>
        {
            _kingdomScreen.Close();
            _camera.CenterOn(cell);
        };

        _townScreen = new TownScreen();
        AddChild(_townScreen);
        _townScreen.Build(_state, screen);

        // sauvegarder sous un nom, charger une sauvegarde (depuis les options)
        _saves = new SavesWindow();
        AddChild(_saves);
        _saves.Build(screen);
        _saves.SaveChosen += name => WriteSave(name, Texts.GameSaved);
        _saves.LoadChosen += entry => LoadSave(entry.Path);

        // Développement : « --capture-delay=3 » attend 3 secondes avant la capture (pour voir un combat avancer)
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-delay=")) is { } delay
            && double.TryParse(delay["--capture-delay=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double seconds))
            _captureTimer = seconds;
        if (loaded != null)
        {
            _hud.AddMessage(Texts.GameLoaded);
            var treasure = _state.Rules.Has(VictoryCondition.FindTreasure) ? _state.TreasureDirection() : null;
            _hud.AddMessage(Texts.Message(new RulesChosen(_state.Rules, treasure), _state.Players[0])!);
        }
        else if (setup != null)
            _state.ChooseRules(setup.Rules);
        else if (_capturePath == null || OS.GetCmdlineUserArgs().Contains("--rules")) // une capture montre la carte, sauf avec --rules
            _hud.ShowRulesChoice(rules => _state.ChooseRules(rules));
        if (OS.GetCmdlineUserArgs().Contains("--zoom")) // développement : partir zoomé
            _camera.ZoomBy(+1);
        if (_capturePath != null && OS.GetCmdlineUserArgs().Contains("--town"))
            ShowTownForCapture();
        CenterOnHero();
        // Développement : « --open=hero » ou « --open=kingdom » ouvre cet écran (pour une capture d'écran)
        if (OS.GetCmdlineUserArgs().Contains("--open=hero") && _state.CurrentHero is { } shown)
            _heroScreen.Open(shown);
        if (OS.GetCmdlineUserArgs().Contains("--open=kingdom"))
            _kingdomScreen.Open();
        // « --open=save » : la fenêtre pour sauvegarder sous un nom
        if (OS.GetCmdlineUserArgs().Contains("--open=save"))
            _saves.OpenToSave(Texts.SuggestedSaveName(_state.CurrentHero?.Name, _state.Day));
        // « --open=options » : la fenêtre des options
        if (OS.GetCmdlineUserArgs().Contains("--open=options"))
            _hud.ShowOptions();
        // « --open=end » : la fenêtre de fin de partie (une victoire, sans rien changer)
        if (OS.GetCmdlineUserArgs().Contains("--open=end"))
            _hud.ShowGameOver(new GameEnded(GameOutcome.Victory, GameEndReason.GuardsCleared), () => { });
        // « --open=level » : la fenêtre d'un choix de niveau (sans rien changer)
        if (OS.GetCmdlineUserArgs().Contains("--open=level") && _state.CurrentHero is { } leveling)
            _hud.ShowLevelChoice(new HeroLevelChoice(leveling, 2, new[] { HeroStat.Attack, HeroStat.Defense, HeroStat.Power }), _ => { });
        // Développement : « --center=treasure » centre la vue sur le trésor (pour une capture d'écran)
        if (OS.GetCmdlineUserArgs().Contains("--center=treasure") && _state.Map.Treasure is { } hidden)
            _camera.CenterOn(hidden);
        if (OS.GetCmdlineUserArgs().Contains("--center=obelisk") && _state.Obelisks.FirstOrDefault() is { } firstObelisk)
            _camera.CenterOn(firstObelisk.Position);
        // Développement : « --cursor-preview=dossier » enregistre les curseurs en images
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--cursor-preview=")) is { } preview)
            MapCursor.SavePreview(preview["--cursor-preview=".Length..]);
        // Développement : « --battle-showcase » : les 7 créatures du Château contre les 7 neutres, sur l'écran de combat
        // (pour comparer les images ; ce combat ne compte pas dans la partie) ; avec « --battle-auto », il se joue seul
        if (OS.GetCmdlineUserArgs().Contains("--battle-showcase"))
        {
            var castle = new Army();
            var neutral = new Army();
            foreach (var creature in _state.Creatures.All.OrderBy(c => c.Tier))
                (creature.Faction == "chateau" ? castle : neutral).Add(creature, 5);
            _battleScreen.Open(new TacticalBattle(castle, neutral, new GameRandom(1)), "la vitrine", TerrainType.Grass, () => { },
                auto: OS.GetCmdlineUserArgs().Contains("--battle-auto"), hero: _state.CurrentHero);
        }
        // Développement : « --creature-preview=fantome,chevalier » : ces créatures des deux côtés du champ (le défenseur
        // les montre retournées), avec un Archer de chaque côté pour l'échelle ; une créature pas encore dans les règles
        // reçoit des caractéristiques moyennes, pour voir ses images et ses animations en jeu. Avec « --battle-auto »,
        // le combat se joue seul (marche et attaques) ; sinon, on voit les attentes et on peut jouer.
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--creature-preview=")) is { } previewArg)
        {
            var ids = previewArg["--creature-preview=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!ids.Contains("archer"))
                ids.Add("archer");
            CreatureType Find(string id) => _state.Creatures.All.FirstOrDefault(c => c.Id == id)
                ?? new CreatureType { Id = id, Name = id, Plural = id, Faction = "apercu", Tier = 4, Attack = 10, Defense = 10,
                                      MinDamage = 4, MaxDamage = 6, HitPoints = 40, Speed = 5, CommandCost = 1 };
            var left = new Army();
            var right = new Army();
            foreach (var id in ids.Take(7))
            {
                left.AddStack(Find(id), 5);
                right.AddStack(Find(id), 5);
            }
            _battleScreen.Open(new TacticalBattle(left, right, new GameRandom(1)), "l'aperçu", TerrainType.Grass, () => { },
                auto: OS.GetCmdlineUserArgs().Contains("--battle-auto"), hero: _state.CurrentHero);
        }
        // Développement : « --battle-test » : des Orcs et des Loups à deux cases, et le héros y va
        if (OS.GetCmdlineUserArgs().Contains("--battle-test") && _state.CurrentHero is { } fighter)
        {
            _battleTest = true;
            var at = fighter.Position.Offset(2, 0);
            foreach (var obj in _state.Map.Objects.Where(o => o.Footprint.Any(c => Math.Abs(c.X - at.X) <= 2 && Math.Abs(c.Y - at.Y) <= 2)).ToList())
                _state.Map.RemoveObject(obj);
            var monsters = new MonsterGuard(at, _state.Creatures.Get("orc"), 8);
            _state.Map.AddObject(monsters);
            _state.SetDestination(fighter.Position.Offset(1, 0));
            _state.StepAlongPath();
        }
        // Développement : « --dig-test » enfouit le trésor sous le héros et le fait creuser (fenêtre du trésor)
        if (OS.GetCmdlineUserArgs().Contains("--dig-test"))
        {
            _state.Map.Treasure = _state.CurrentHero!.Position;
            _state.Dig();
        }
        // Développement : « --treasure-map » montre la carte au trésor (avec deux morceaux dévoilés)
        if (OS.GetCmdlineUserArgs().Contains("--treasure-map"))
        {
            foreach (var obelisk in _state.Obelisks.Take(2))
                _state.Players[0].Obelisks.Add(obelisk.Id);
            _hud.ShowTreasureMap(_state, _state.Players[0]);
        }
    }

    /// <summary>
    /// Pour une capture d'écran seulement (« --town ») : donne au joueur la première ville avec presque tous ses bâtiments,
    /// une armée de chaque créature en garnison, et ouvre son écran.
    /// </summary>
    private void ShowTownForCapture()
    {
        var player = _state.Players[0];
        var town = _state.Towns.First();
        town.SetOwner(player);
        var data = _state.Data; // les créatures de la partie (une autre copie des données ne fusionnerait pas les troupes)
        // tous les bâtiments sauf les trois derniers, pour voir aussi des cartes « à construire »
        foreach (var building in data.Buildings.All.SkipLast(3))
            if (!town.Has(building.Id))
                town.AddBuilding(building);
        foreach (var creature in data.Creatures.All.Where(c => c.Faction == "chateau").Take(4))
            town.Garrison.Add(creature, 10);
        // le héros dans la ville, pour voir aussi son armée
        if (_state.CurrentHero is { } hero)
            hero.Position = town.Position;
        OpenTown(town);
        // « --town-window=build|recruit|tavern » ouvre cette fenêtre ; « --town-build=id » construit ce bâtiment
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--town-window=")) is { } window)
            _townScreen.ShowWindow(window["--town-window=".Length..]);
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--town-build=")) is { } build)
        {
            town.BuiltToday = false;
            _state.Build(town, build["--town-build=".Length..]);
        }
    }

    /// <summary>
    /// Développement (« --terrain-test ») : peint autour du héros un échantillon de toutes les rencontres de
    /// terrains (rives, chemins, coins, cases seules), pour juger les bords sur une seule image.
    /// </summary>
    private void PaintTerrainTest()
    {
        string[] rows =
        {
            "gggggggggggggggggggggg",
            "gwwwggrrrrrrgsssgmmmgg",
            "gwwwgggggrggsssggmmggg",
            "ggwgggsgggrgggsggggwgg",
            "gggggwswgggrrggwwwgggg",
            "gsssggwgggggrggwswgmgg",
            "gswsggggmmmgrggwwwgggg",
            "gsssgwgggmgggggggggggg",
            "ggggwwwggggmgwgwgwgsgg",
            "grrrrrrRRRRRrrqqqqrrrg",
            "gfffggMMMggwwwwwwwwggg",
            "gfgfggMgMggwsssssswggg",
            "gggggggggggwwwwwwwwggg",
            "gggggggggggggggggggggg",
        };
        // r : route sur l'herbe, R : route sur le sable, q : route dans le marais
        var roads = new System.Collections.Generic.Dictionary<char, TerrainType>
        {
            ['r'] = TerrainType.Grass, ['R'] = TerrainType.Sand, ['q'] = TerrainType.Swamp,
        };
        var kinds = new System.Collections.Generic.Dictionary<char, TerrainType>
        {
            ['g'] = TerrainType.Grass, ['s'] = TerrainType.Sand, ['m'] = TerrainType.Swamp,
            ['w'] = TerrainType.Water, ['f'] = TerrainType.Forest, ['M'] = TerrainType.Mountain,
        };
        var hero = _state.CurrentHero!.Position;
        int left = Math.Clamp(hero.X - rows[0].Length / 2, 0, _state.Map.Width - rows[0].Length);
        int top = Math.Clamp(hero.Y - rows.Length / 2, 0, _state.Map.Height - rows.Length);
        for (int y = 0; y < rows.Length; y++)
        for (int x = 0; x < rows[y].Length; x++)
        {
            var cell = new Cell(left + x, top + y);
            char kind = rows[y][x];
            _state.Map.SetRoad(cell, roads.ContainsKey(kind));
            _state.Map.SetTerrain(cell, roads.TryGetValue(kind, out var under) ? under : kinds[kind]);
        }
    }

    /// <summary>Graine passée en argument (--seed=42), sinon une graine au hasard.</summary>
    /// <summary>Développement : « --size=tiny » (small, normal, large, huge) choisit la taille de la carte.</summary>
    private static MapSize ReadSize()
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--size="));
        return arg != null && Enum.TryParse(arg["--size=".Length..], ignoreCase: true, out MapSize size) ? size : MapSize.Normal;
    }

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
        Godot.FileAccess.GetFileAsString("res://data/buildings.json"),
        Godot.FileAccess.GetFileAsString("res://data/artifacts.json"));

    /// <summary>Le joueur regarde la carte (pas de fenêtre ni d'écran de ville ouverts).</summary>
    /// <summary>Passe au héros suivant et centre la vue sur lui (touche H ou bouton).</summary>
    private void NextHero()
    {
        if (!CanAct)
            return;
        _state.SelectNextHero();
        CenterOnHero();
    }

    private bool OnMap => !_inMenu && !_saves.IsOpen && !_hud.IsDialogOpen && !_townScreen.IsOpen && !_battleScreen.IsOpen
        && !_heroScreen.IsOpen && !_kingdomScreen.IsOpen;

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
        // la sauvegarde automatique, au début de chaque tour (une seule, remplacée à chaque fois)
        if (_state.Outcome == GameOutcome.Playing && _state.CurrentBattle == null && _capturePath == null)
            SaveFiles.Write(SaveFiles.AutoName, _state);
    }

    /// <summary>F5 : la sauvegarde rapide (sur la carte, hors marche).</summary>
    private void SaveGame()
    {
        if (CanAct)
            WriteSave(SaveFiles.QuickName, Texts.QuickSaved);
    }

    private void WriteSave(string name, string done)
    {
        if (_state.CurrentBattle != null)
            return;
        _hud.AddMessage(SaveFiles.Write(name, _state) ? done : Texts.SaveFailed);
    }

    /// <summary>F9 : reprend la sauvegarde rapide.</summary>
    private void LoadGame()
    {
        if (_walking || !OnMap)
            return;
        if (!SaveFiles.Exists(SaveFiles.QuickName))
        {
            _hud.AddMessage(Texts.NoQuickSave);
            return;
        }
        LoadSave(SaveFiles.List().First(e => e.IsQuick).Path);
    }

    /// <summary>
    /// Charge une sauvegarde : elle est d'abord lue (une sauvegarde abîmée ne fait pas perdre la partie en cours),
    /// puis la scène est relancée avec elle.
    /// </summary>
    private void LoadSave(string path)
    {
        _loadedState = SaveFiles.Read(path, _state.Data);
        if (_loadedState == null)
        {
            _hud.AddMessage(Texts.LoadFailed);
            return;
        }
        GetTree().ReloadCurrentScene();
    }

    /// <summary>Retour au menu principal (fin de partie, ou options) : la scène est relancée sur le menu.</summary>
    private void GoToMainMenu()
    {
        _goToMenu = true;
        GetTree().ReloadCurrentScene();
    }

    /// <summary>La scène ne montre que le menu principal ; un choix relance la scène sur la partie.</summary>
    private void ShowMainMenu()
    {
        _inMenu = true;
        var menu = new MainMenu();
        AddChild(menu);
        menu.Build(GetViewportRect().Size);
        menu.NewGame += setup =>
        {
            _setup = setup;
            GetTree().ReloadCurrentScene();
        };
        menu.LoadChosen += entry =>
        {
            _loadedState = SaveFiles.Read(entry.Path, LoadData());
            if (_loadedState == null)
            {
                var dialog = new GameDialog { Title = Texts.LoadTitle, DialogText = Texts.LoadFailed, Exclusive = true };
                menu.AddChild(dialog);
                dialog.PopupCentered();
                return;
            }
            GetTree().ReloadCurrentScene();
        };
        // développement : « --menu=new » ou « --menu=load » ouvre cette fenêtre (captures)
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--menu=")) is { } open)
            menu.OpenForCapture(open["--menu=".Length..]);
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

    /// <summary>L'écran du héros (clic sur son portrait, double clic dans une liste).</summary>
    private void OpenHeroScreen(Hero hero)
    {
        if (OnMap)
            _heroScreen.Open(hero);
    }

    /// <summary>Le royaume (bouton ou touche K).</summary>
    private void OpenKingdom()
    {
        if (OnMap)
            _kingdomScreen.Open();
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

    /// <summary>F11 ou Alt+Entrée, partout (carte, ville, combat) : plein écran ou fenêtre.</summary>
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } key
            && (key.Keycode == Key.F11 || (key.Keycode is Key.Enter or Key.KpEnter && key.AltPressed)))
        {
            ToggleFullscreen();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Passe du plein écran à une fenêtre agrandie (avec sa barre de titre), ou l'inverse.</summary>
    public static void ToggleFullscreen()
    {
        bool full = DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        DisplayServer.WindowSetMode(full ? DisplayServer.WindowMode.Maximized : DisplayServer.WindowMode.Fullscreen);
    }

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
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheel:
                _camera.ZoomBy(+1, wheel.Position);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheel:
                _camera.ZoomBy(-1, wheel.Position);
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                switch (key.Keycode)
                {
                    case Key.Enter or Key.KpEnter: EndTurn(); break;
                    case Key.Space: StartWalking(); break;
                    case Key.Escape: _hud.ShowOptions(); break;
                    case Key.C: CenterOnHero(); break;
                    case Key.K: OpenKingdom(); break;
                    case Key.Plus or Key.KpAdd or Key.Equal: _camera.ZoomBy(+1); break;
                    case Key.Minus or Key.KpSubtract: _camera.ZoomBy(-1); break;
                    case Key.F5: SaveGame(); break;
                    case Key.F: Dig(); break;
                    case Key.M: _hud.ShowTreasureMap(_state, _state.Players[0]); break;
                    case Key.F9: LoadGame(); break;
                    case Key.T when _state.CurrentPlayer.Towns.FirstOrDefault() is { } town: OpenTown(town); break;
                    case Key.H: NextHero(); break;
                }
                break;
        }
    }

    /// <summary>Option de développement : fichier où enregistrer une image de l'écran (voir <see cref="Capture"/>).</summary>
    private string? _capturePath;
    private double _captureTimer = 1.0;

    /// <summary>Enregistre une image de l'écran (pour vérifier les graphismes sans jouer), puis quitte.</summary>
    private void Capture(double delta)
    {
        _captureTimer -= delta;
        if (_captureTimer > 0)
            return;
        var error = GetViewport().GetTexture().GetImage().SavePng(_capturePath!);
        GD.Print(error == Error.Ok ? $"Image enregistrée : {_capturePath}" : $"Image non enregistrée : {error}");
        GetTree().Quit();
    }

    public override void _Process(double delta)
    {
        if (_capturePath != null)
            Capture(delta);
        if (_inMenu)
            return;
        if (OnMap)
            _camera.Scroll((float)delta);
        _actors.Hovered = OnMap ? _camera.HoveredCell() : null;
        if (_devHover is { } pretend) // développement : « --hover=x,y », comme si la souris était sur cette case
            _actors.Hovered = pretend;
        UpdateCursor();
        _hud.Refresh(_actors.Hovered);
        _heroScreen.Refresh();
        _kingdomScreen.Refresh();

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

    /// <summary>Remet le curseur normal en quittant (sinon Godot signale une image de curseur non libérée).</summary>
    public override void _ExitTree() => Input.SetCustomMouseCursor(null);

    /// <summary>Ce qui a décidé du curseur la dernière fois : on ne le recalcule que si cela change.</summary>
    private (Cell?, int, Cell, int, int, bool)? _cursorKey;

    /// <summary>Le curseur de la souris selon la case survolée (botte et jours, épées, main, croix, casque).</summary>
    private void UpdateCursor()
    {
        var hero = _state.CurrentHero;
        var key = (_actors.Hovered, hero?.Id ?? 0, hero?.Position ?? default, hero?.Movement ?? 0, _state.Map.Objects.Count, CanAct);
        if (key == _cursorKey)
            return;
        _cursorKey = key;
        var (kind, days) = CanAct || _actors.Hovered == null ? MapCursor.Choose(_state, _actors.Hovered) : (CursorKind.Pointer, 0);
        MapCursor.Show(kind, days);
    }

    /// <summary>Un combat commence : le joueur choisit de le diriger (écran de combat) ou le combat rapide.</summary>
    private void StartBattle(BattleStarted started)
    {
        string opponent = Texts.Opponent(started.Defender, started.Defender switch
        {
            MonsterGuard guard => guard.Army,
            Town town => town.Garrison,
            _ => new Army(),
        });
        var terrain = _state.Map.GetTerrain(started.Hero.Position);
        void Tactical() => _battleScreen.Open(started.Battle, opponent, terrain, () => _state.FinishBattle(),
            auto: _battleTest && OS.GetCmdlineUserArgs().Contains("--battle-auto"), hero: started.Hero);
        void Quick()
        {
            BattleAI.PlayOut(started.Battle);
            _state.FinishBattle();
        }
        if (_battleTest)
            Tactical();
        else
            _hud.ShowBattleChoice(opponent, Tactical, Quick);
    }

    /// <summary>Le héros courant creuse sur sa case (touche F, ou bouton des options).</summary>
    private void Dig()
    {
        if (!CanAct)
            return;
        if (_state.Dig() == DigResult.NeedsFullDay)
            _hud.AddMessage(Texts.DigNeedsFullDay);
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
                case BattleStarted started when started.Hero.Owner == viewer:
                    _walking = false;
                    StartBattle(started);
                    break;
                case BattleFought battle:
                    _walking = false;
                    _hud.ShowBattle(battle);
                    break;
                case HeroLevelChoice choice when choice.Hero.Owner == viewer:
                    _hud.ShowLevelChoice(choice, stat => _state.ChooseStat(choice, stat));
                    break;
                case BuildingBuilt built:
                    _townScreen.ShowBuilt(built); // le nouveau bâtiment apparaît sur la ville
                    break;
                case TownEntered entered when entered.Hero.Owner == viewer:
                    _townToOpen = entered.Town;
                    break;
                case TownCaptured captured when captured.Hero.Owner == viewer:
                    _townToOpen = captured.Town;
                    break;
                case ObeliskVisited { FirstVisit: true } obelisk when obelisk.Hero.Owner == viewer:
                    _walking = false;
                    _hud.ShowTreasureMap(_state, viewer);
                    break;
                case TreasureDug dug when dug.Hero.Owner == viewer:
                    _walking = false;
                    _hud.ShowArtifactFound(Texts.TreasureDugTitle, Texts.TreasureDugText(dug), dug.Artifact);
                    break;
                case GameEnded end:
                    _walking = false;
                    _townScreen.Close();
                    _hud.ShowGameOver(end, GoToMainMenu);
                    break;
            }
        }
    }
}
