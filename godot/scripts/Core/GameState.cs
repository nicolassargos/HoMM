using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

public enum GameOutcome { Playing, Victory, Defeat }

/// <summary>
/// L'état d'une partie : carte, joueurs, calendrier, chemin choisi.
/// Les règles ne produisent aucun texte : elles décrivent ce qui se passe par des événements
/// (voir <see cref="TakeEvents"/>), que l'affichage met en mots.
/// Prévu pour plusieurs joueurs (tour par tour), même s'il n'y a pour l'instant qu'un humain.
/// </summary>
public sealed partial class GameState
{
    private int _currentPlayer;
    private readonly GameRandom _rng;
    private readonly List<HeroLevelChoice> _pendingChoices = new();
    private int _nextPlayerId = 1;
    private int _nextHeroId = 1;

    public GameState(int seed, GameData data) : this(seed, data, MapSize.Normal) { }

    /// <summary>
    /// Une nouvelle partie sur une carte générée de cette taille, avec <paramref name="opponents"/> adversaires joués par
    /// l'ordinateur (au plus <see cref="MapSizes.MaxOpponents"/>), chacun dans un coin de la carte.
    /// </summary>
    public GameState(int seed, GameData data, MapSize size, int opponents = 0)
        : this(seed, data, Generate(seed, data, size, Math.Clamp(opponents, 0, MapSizes.MaxOpponents(size))))
        => AddPlayers(Map.Starts.Count);

    private static AdventureMap Generate(int seed, GameData data, MapSize size, int opponents)
    {
        var (width, height) = MapSizes.Dimensions(size);
        return new MapGenerator(seed, data) { Players = opponents + 1 }.Generate(width, height);
    }

    /// <summary>Les noms (et couleurs) des joueurs, dans l'ordre : le joueur humain, puis les adversaires.</summary>
    private static readonly string[] PlayerNames = { "Rouge", "Bleu", "Vert", "Orange" };

    /// <summary>Les héros de départ des adversaires (pris à la fin de la liste de la taverne, qui ne les propose plus).</summary>
    private static readonly string[] ComputerHeroNames = { "Sire Yvain", "Dame Viviane", "Brann le Rouge" };

    /// <summary>
    /// Une partie sur une carte faite à la main (pour les tests, et plus tard un éditeur de cartes), avec le joueur
    /// humain et son héros de départ sur <see cref="AdventureMap.Start"/>.
    /// </summary>
    public static GameState ForMap(AdventureMap map, GameData data, int seed = 0)
    {
        var state = new GameState(seed, data, map);
        state.AddPlayers(Math.Max(1, map.Starts.Count));
        return state;
    }

    /// <summary>
    /// Les joueurs, chacun sur son départ avec son héros et sa petite armée : le joueur humain d'abord, puis les adversaires
    /// joués par l'ordinateur.
    /// </summary>
    private void AddPlayers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var player = new Player(PlayerNames[i % PlayerNames.Length], Map) { Id = _nextPlayerId++, IsComputer = i > 0 };
            Players.Add(player);
            string name = i == 0 ? "Sire Roland" : ComputerHeroNames[(i - 1) % ComputerHeroNames.Length];
            var hero = new Hero(name, player, Map.Starts.Count > i ? Map.Starts[i] : Map.Start) { Id = _nextHeroId++ };
            hero.Army.Add(Creatures.Get("paysan"), 30);
            hero.Army.Add(Creatures.Get("archer"), 15);
            hero.Army.Add(Creatures.Get("griffon"), 4);
            if (i == 0)
                CurrentHero = hero;
        }
    }

    /// <summary>Les adversaires joués par l'ordinateur.</summary>
    public IEnumerable<Player> Opponents => Players.Where(p => p.IsComputer);

    /// <summary>Une partie sur une carte déjà faite, sans joueur (pour le chargement, voir <see cref="Load"/>).</summary>
    private GameState(int seed, GameData data, AdventureMap map)
    {
        Seed = seed;
        Data = data;
        _rng = new GameRandom(seed);
        Map = map;
    }

    public int Seed { get; }
    public GameData Data { get; }
    public CreatureCatalog Creatures => Data.Creatures;
    public IEnumerable<Town> Towns => Map.Objects.OfType<Town>();
    public AdventureMap Map { get; }
    public List<Player> Players { get; } = new();
    public Player CurrentPlayer => Players[_currentPlayer];

    /// <summary>Le héros sélectionné du joueur dont c'est le tour (null s'il n'en a plus).</summary>
    public Hero? CurrentHero { get; private set; }

    public GameOutcome Outcome { get; private set; } = GameOutcome.Playing;

    /// <summary>Conditions de victoire et de défaite de la partie (voir <see cref="ChooseRules"/>).</summary>
    public GameRules Rules { get; private set; } = GameRules.Default;

    /// <summary>Jour écoulé depuis le début (0 = premier jour).</summary>
    public int Day { get; private set; }

    /// <summary>Chemin prévu pour le héros sélectionné, ou null. Chaque héros garde le sien.</summary>
    public List<Cell>? Path
    {
        get => CurrentHero is { } hero ? _paths.GetValueOrDefault(hero) : null;
        private set
        {
            if (CurrentHero is not { } hero)
                return;
            if (value == null)
                _paths.Remove(hero);
            else
                _paths[hero] = value;
        }
    }

    private readonly Dictionary<Hero, List<Cell>> _paths = new();

    /// <summary>Prix d'un héros à la taverne, en or (comme dans Heroes 3).</summary>
    public const int HeroCost = 2500;

    /// <summary>Nombre maximal de héros par joueur.</summary>
    public const int MaxHeroesPerPlayer = 8;

    /// <summary>Les héros qu'on peut recruter dans les tavernes, dans l'ordre (données de jeu, comme les noms de villes).</summary>
    private static readonly string[] HeroNames =
        {
            "Dame Isolde", "Sire Gauvain", "Dame Morgane", "Sire Perceval", "Renaud le Goupil", "Brann le Rouge",
            "Dame Viviane", "Sire Yvain",
        };

    private readonly List<GameEvent> _events = new();

    public IEnumerable<Hero> AllHeroes => Players.SelectMany(p => p.Heroes);

    public Hero? HeroById(int id) => AllHeroes.FirstOrDefault(h => h.Id == id);

    public Player? PlayerById(int id) => Players.FirstOrDefault(p => p.Id == id);

    /// <summary>
    /// Les événements survenus depuis le dernier appel, du plus ancien au plus récent.
    /// La file est vidée : chaque événement n'est rendu qu'une fois.
    /// </summary>
    public IReadOnlyList<GameEvent> TakeEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }

    private void Emit(GameEvent e) => _events.Add(e);

    private void EndGame(GameOutcome outcome, GameEndReason reason)
    {
        Outcome = outcome;
        Path = null;
        Emit(new GameEnded(outcome, reason));
    }

    /// <summary>
    /// Choisit les conditions de la partie. Ce n'est possible qu'au premier jour, avant toute action
    /// (renvoie false sinon).
    /// </summary>
    public bool ChooseRules(GameRules rules)
    {
        if (Day > 0 || rules.Victory == VictoryCondition.None)
            return false;
        Rules = rules;
        Emit(new RulesChosen(rules, rules.Has(VictoryCondition.FindTreasure) ? TreasureDirection() : null));
        return true;
    }

    /// <summary>Un héros du joueur humain a déterré le trésor (la partie est alors gagnée s'il fallait le trouver).</summary>
    private bool _treasureFound;

    /// <summary>Dans quelle partie de la carte le trésor est enfoui (grille de 3 × 3), ou null s'il n'y en a pas (ou plus).</summary>
    public Compass? TreasureDirection()
    {
        if (Map.Treasure is not { } treasure)
            return null;
        int column = Math.Clamp(treasure.X * 3 / Map.Width, 0, 2);
        int row = Math.Clamp(treasure.Y * 3 / Map.Height, 0, 2);
        return (row, column) switch
        {
            (0, 0) => Compass.NorthWest, (0, 1) => Compass.North, (0, 2) => Compass.NorthEast,
            (1, 0) => Compass.West, (1, 1) => Compass.Center, (1, 2) => Compass.East,
            (2, 0) => Compass.SouthWest, (2, 1) => Compass.South, _ => Compass.SouthEast,
        };
    }

    /// <summary>Vérifie la victoire après chaque changement qui peut la donner (monstre vaincu, ville prise, trésor).</summary>
    private void CheckVictory()
    {
        if (Outcome != GameOutcome.Playing)
            return;
        // La première condition accomplie donne la victoire
        GameEndReason? reason = null;
        if (Rules.Has(VictoryCondition.FindTreasure) && _treasureFound)
            reason = GameEndReason.TreasureFound;
        else if (Rules.Has(VictoryCondition.CaptureTowns) && Towns.Any() && Towns.All(t => t.Owner == Players[0]))
            reason = GameEndReason.TownsCaptured;
        else if (Rules.Has(VictoryCondition.ClearGuards) && !Map.Objects.OfType<MonsterGuard>().Any())
            reason = GameEndReason.GuardsCleared;
        else if (Rules.Has(VictoryCondition.DefeatOpponents) && Opponents.Any() && Opponents.All(p => p.IsDefeated))
            reason = GameEndReason.OpponentsDefeated;
        if (reason is { } won)
            EndGame(GameOutcome.Victory, won);
    }

    // ------------------------------------------------------------------ tours

    /// <summary>Fin du tour du joueur courant. Quand tous les joueurs ont joué, un nouveau jour commence.</summary>
    public void EndTurn()
    {
        if (Outcome != GameOutcome.Playing || CurrentBattle != null)
            return;
        CheckVictory();
        if (Outcome != GameOutcome.Playing)
            return;

        int previous = _currentPlayer;
        do
            _currentPlayer = (_currentPlayer + 1) % Players.Count;
        while (CurrentPlayer.IsDefeated && _currentPlayer != previous);

        if (_currentPlayer <= previous)
        {
            NewDay();
            if (Outcome != GameOutcome.Playing)
                return;
        }
        // Les chemins prévus sont gardés d'un jour à l'autre (chaque héros a le sien)
        if (_currentPlayer != previous)
        {
            CurrentHero = CurrentPlayer.Heroes.FirstOrDefault();
            Emit(new TurnStarted(CurrentPlayer));
        }
    }

    private void NewDay()
    {
        Day++;
        foreach (var hero in AllHeroes)
            hero.NewDay();

        bool newWeek = Day % 7 == 0;
        foreach (var town in Towns)
        {
            town.NewDay();
            if (newWeek)
                town.NewWeek();
        }
        if (newWeek)
            foreach (var guard in Map.Objects.OfType<MonsterGuard>())
                guard.GrowWeekly();
        Emit(new DayStarted(Day, newWeek));

        foreach (var player in Players)
        {
            var income = player.DailyIncome();
            foreach (var (resource, amount) in income)
                player.Resources[resource] += amount;
            if (income.Count > 0)
                Emit(new IncomeReceived(player, income));
        }

        if (Rules.DayLimit is { } limit && Day > limit)
            EndGame(GameOutcome.Defeat, GameEndReason.TimeOut);
    }

    // ------------------------------------------------------------- déplacement

    public void SetDestination(Cell target)
    {
        if (CurrentHero is not { } hero || Outcome != GameOutcome.Playing)
            return;
        // Un clic sur les murs d'une ville mène à son entrée
        if (Map.OccupantAt(target) is { } occupant)
            target = occupant.Position;
        Path = Pathfinder.FindPath(Map, hero.Position, target, OtherHeroCells(hero, target));
    }

    public void ClearPath() => Path = null;

    /// <summary>Les cases des autres héros : on ne peut pas les traverser.</summary>
    private HashSet<Cell> OtherHeroCells(Hero hero) =>
        AllHeroes.Where(h => h != hero).Select(h => h.Position).ToHashSet();

    /// <summary>Les cases des autres héros, sauf celle d'un héros ennemi visé (on va jusqu'à lui pour l'attaquer).</summary>
    private HashSet<Cell> OtherHeroCells(Hero hero, Cell target) =>
        AllHeroes.Where(h => h != hero && !(h.Position == target && h.Owner != hero.Owner)).Select(h => h.Position).ToHashSet();

    /// <summary>Le héros d'un autre joueur sur cette case, s'il y en a un.</summary>
    public Hero? EnemyHeroAt(Cell cell, Player player) => AllHeroes.FirstOrDefault(h => h.Position == cell && h.Owner != player);

    /// <summary>Sélectionne un héros du joueur courant (son chemin prévu est retrouvé).</summary>
    public void SelectHero(Hero hero)
    {
        if (hero.Owner == CurrentPlayer && hero.Owner.Heroes.Contains(hero))
            CurrentHero = hero;
    }

    /// <summary>Le héros suivant du joueur courant (pour passer de l'un à l'autre).</summary>
    public void SelectNextHero()
    {
        var heroes = CurrentPlayer.Heroes;
        if (heroes.Count == 0)
            return;
        int index = CurrentHero is { } hero ? heroes.IndexOf(hero) : -1;
        CurrentHero = heroes[(index + 1) % heroes.Count];
    }

    public bool CanStartWalking =>
        Outcome == GameOutcome.Playing && CurrentBattle == null && CurrentHero is { } hero && Path is { Count: > 0 }
        && hero.CanStepTo(Map, Path[0]);

    /// <summary>
    /// Fait avancer le héros d'une case. Renvoie false s'il ne peut plus avancer.
    /// Entrer à côté d'un monstre déclenche un combat (événement <see cref="BattleFought"/>) et arrête la marche.
    /// </summary>
    public bool StepAlongPath()
    {
        if (!CanStartWalking)
            return false;
        var hero = CurrentHero!;
        var next = Path![0];

        // Un héros d'un autre joueur sur la case suivante : on l'attaque, sans bouger (comme un monstre)
        if (EnemyHeroAt(next, hero.Owner) is { } enemy)
        {
            hero.SpendMovement(Pathfinder.StepCost(Map, hero.Position, next));
            Path = null;
            BeginFights(hero, hero.Position, hero.Position, new[] { ((MapObject)new HeroDefender(enemy), enemy.Army) }, visit: false);
            return false;
        }
        // Un de ses héros est sur le chemin : on s'arrête (le chemin reste prévu)
        if (AllHeroes.Any(h => h != hero && h.Position == next))
            return false;

        // Attaque directe d'un monstre voisin : le héros reste sur sa case
        if (Map.ObjectAt(next) is MonsterGuard target)
        {
            hero.SpendMovement(Pathfinder.StepCost(Map, hero.Position, next));
            Path = null;
            BeginFights(hero, hero.Position, hero.Position, new[] { ((MapObject)target, target.Army) }, visit: false);
            return false;
        }

        Path.RemoveAt(0);
        var from = hero.Position;
        hero.StepTo(Map, next);
        Emit(new HeroMoved(hero, from, next));

        // Les monstres autour attaquent, puis la garnison d'une ville ennemie ou neutre ; ensuite seulement, la visite
        var opponents = Map.GuardsAround(next).Select(g => ((MapObject)g, g.Army)).ToList();
        if (Map.ObjectAt(next) is Town { Garrison.IsEmpty: false } town && town.Owner != hero.Owner)
            opponents.Add((town, town.Garrison));
        if (opponents.Count > 0)
        {
            BeginFights(hero, from, next, opponents, visit: true);
            return false;
        }
        Visit(hero, next);
        return true;
    }

    /// <summary>Le héros visite l'objet de la case où il vient d'arriver (s'il y en a un).</summary>
    private void Visit(Hero hero, Cell cell)
    {
        if (Map.ObjectAt(cell) is { } obj and not MonsterGuard)
        {
            var visit = obj.Visit(hero);
            if (visit is TownCaptured captured)
            {
                hero.Owner.Statistics.TownsCaptured++;
                if (captured.PreviousOwner is { } loser)
                    AnnounceDefeat(loser);
            }
            if (obj.RemovedOnVisit)
                Map.RemoveObject(obj);
            Emit(visit);
            CheckVictory();
        }
        if (Path is { Count: 0 })
            Path = null;
    }

    // -------------------------------------------------------------- combats

    /// <summary>
    /// True (par défaut) : un combat est joué tout de suite en combat rapide (tests, simulation, futures IA).
    /// L'affichage met false : la partie s'arrête au début de chaque combat (<see cref="CurrentBattle"/>), le joueur
    /// le joue sur l'écran de combat ou choisit le combat rapide, puis l'affichage appelle <see cref="FinishBattle"/>.
    /// </summary>
    public bool QuickBattles { get; set; } = true;

    /// <summary>
    /// Un joueur humain attaqué (son héros, sa ville) dirige sa défense (étape 8) : le combat attend, même pendant le tour
    /// d'un adversaire qui joue en combats rapides ; l'adversaire reprend son tour une fois le combat fini.
    /// </summary>
    public bool HumansDefend { get; set; }

    /// <summary>Le défenseur est-il un joueur humain (son héros, ou sa ville) ?</summary>
    private static bool DefendedByHuman(MapObject defender) => defender switch
    {
        HeroDefender d => !d.Hero.Owner.IsComputer,
        Town { Owner: { } owner } => !owner.IsComputer,
        _ => false,
    };

    /// <summary>Le combat en cours (la partie attend qu'il finisse), ou null.</summary>
    public TacticalBattle? CurrentBattle { get; private set; }

    /// <summary>
    /// Le pas qui a mené aux combats : le héros, la case d'où il vient (il y revient s'il fuit), celle où il est, ses
    /// adversaires dans l'ordre, et s'il visite la case une fois les combats gagnés.
    /// </summary>
    private sealed record PendingStep(Hero Hero, Cell From, Cell Cell, Queue<(MapObject Defender, Army Army)> Opponents, bool Visit);

    /// <summary>Ce qu'il faut pour faire le bilan du combat en cours.</summary>
    private sealed record BattleContext(Hero Hero, MapObject Defender, IReadOnlyList<CreatureStack> DefenderArmy);

    private PendingStep? _step;
    private BattleContext? _battle;

    private void BeginFights(Hero hero, Cell from, Cell cell, IEnumerable<(MapObject Defender, Army Army)> opponents, bool visit)
    {
        _step = new PendingStep(hero, from, cell, new Queue<(MapObject, Army)>(opponents), visit);
        NextFight();
    }

    /// <summary>Le combat suivant du pas en cours ; quand il n'y en a plus, la visite de la case.</summary>
    private void NextFight()
    {
        if (_step is not { } step)
            return;
        if (step.Opponents.Count == 0)
        {
            _step = null;
            if (step.Visit)
                Visit(step.Hero, step.Cell);
            return;
        }
        var (defender, army) = step.Opponents.Dequeue();
        var rebels = RollRevolts(step.Hero);
        _battle = new BattleContext(step.Hero, defender, army.Clone().Stacks);
        CurrentBattle = new TacticalBattle(step.Hero.Army, army, _rng, step.Hero, rebels, (defender as HeroDefender)?.Hero);
        Emit(new BattleStarted(step.Hero, defender, CurrentBattle));
        if (QuickBattles && !(HumansDefend && DefendedByHuman(defender)))
        {
            BattleAI.PlayOut(CurrentBattle);
            FinishBattle();
        }
    }

    /// <summary>
    /// Le combat en cours est fini : son bilan (expérience, monstre retiré, héros perdu, ou fuite), puis la suite du pas
    /// (le combat suivant, ou la visite de la case). Renvoie false s'il n'y a pas de combat fini à conclure.
    /// </summary>
    public bool FinishBattle()
    {
        if (CurrentBattle is not { IsOver: true } battle || _battle is not { } context)
            return false;
        var report = battle.Report();
        CurrentBattle = null;
        _battle = null;
        var hero = context.Hero;
        // les chiffres de la partie
        var stats = hero.Owner.Statistics;
        stats.CreaturesLost += report.AttackerLosses.Sum(s => s.Count);
        stats.CreaturesKilled += report.DefenderLosses.Sum(s => s.Count);
        if (report.AttackerWon)
            stats.BattlesWon++;
        // celui qui se défendait : le propriétaire de la ville, ou le héros attaqué
        var defenderHero = (context.Defender as HeroDefender)?.Hero;
        var defenderOwner = defenderHero?.Owner ?? (context.Defender as Town)?.Owner;
        if (defenderOwner != null)
        {
            defenderOwner.Statistics.CreaturesLost += report.DefenderLosses.Sum(s => s.Count);
            defenderOwner.Statistics.CreaturesKilled += report.AttackerLosses.Sum(s => s.Count);
            if (!report.AttackerWon && !report.Fled)
                defenderOwner.Statistics.BattlesWon++;
        }
        Emit(new BattleFought(hero, context.Defender, context.DefenderArmy, report, defenderOwner));

        // le héros qui se défendait : vaincu, il disparaît ; vainqueur, il gagne la valeur des troupes tuées
        if (defenderHero != null)
        {
            if (report.AttackerWon)
                RemoveHero(defenderHero);
            else
                GrantExperience(defenderHero, Battle.ExperienceFor(report.AttackerLosses));
        }

        if (report.AttackerWon)
        {
            GrantExperience(hero, report.Experience);
            if (context.Defender is MonsterGuard guard)
            {
                Map.RemoveObject(guard);
                // Le monstre visé a été vaincu : inutile d'aller sur sa case
                if (Path is { Count: > 0 } && Path[^1] == guard.Position)
                    Path = null;
            }
            CheckVictory();
            NextFight();
            return true;
        }

        var step = _step;
        _step = null;
        if (report.Fled && !hero.Army.IsEmpty && step != null)
        {
            // Le héros s'est enfui : il revient sur la case d'où il venait, sans mouvement pour aujourd'hui
            if (hero.Position != step.From)
            {
                var at = hero.Position;
                hero.Position = step.From;
                Emit(new HeroMoved(hero, at, step.From));
            }
            hero.SpendMovement(hero.Movement);
            Path = null;
            return true;
        }
        RemoveHero(hero);
        return true;
    }

    /// <summary>
    /// Au début d'un combat, les troupes trop nombreuses pour le charisme peuvent se révolter :
    /// elles sortent de l'armée et renvoyées dans un second groupe (combattent contre le héros).
    /// </summary>
    private Army RollRevolts(Hero hero)
    {
        var rebels = new Army();
        for (int i = hero.Army.Stacks.Count - 1; i >= 0; i--)
        {
            var stack = hero.Army.Stacks[i];
            double chance = Command.RevoltChance(stack.Type, stack.Count, hero.Charisma);
            if (chance > 0 && _rng.NextDouble() < chance)
                hero.Army.MoveStackTo(i, rebels);
        }
        return rebels;
    }

    /// <summary>
    /// Ajoute de l'expérience. Chaque niveau atteint crée un choix (<see cref="HeroLevelChoice"/>) :
    /// le héros ne monte vraiment de niveau qu'une fois la caractéristique choisie (<see cref="ChooseStat"/>).
    /// </summary>
    public void GrantExperience(Hero hero, int amount)
    {
        hero.AddExperience(amount);
        while (hero.Experience >= Hero.ExperienceForLevel(NextLevelAfterPending(hero)))
        {
            var choice = new HeroLevelChoice(hero, NextLevelAfterPending(hero), Enum.GetValues<HeroStat>());
            _pendingChoices.Add(choice);
            Emit(choice);
        }
    }

    /// <summary>Les niveaux gagnés dont la caractéristique n'est pas encore choisie (tous joueurs).</summary>
    public IReadOnlyList<HeroLevelChoice> PendingChoices => _pendingChoices;

    /// <summary>Le joueur choisit la caractéristique gagnée à ce niveau. Renvoie false si ce choix n'est pas proposé.</summary>
    public bool ChooseStat(HeroLevelChoice choice, HeroStat stat)
    {
        if (!_pendingChoices.Contains(choice) || !choice.Options.Contains(stat))
            return false;
        _pendingChoices.Remove(choice);
        var before = choice.Hero.Statistics;
        choice.Hero.LevelUp(stat);
        Emit(new HeroLeveledUp(choice.Hero, stat, before, choice.Hero.Statistics));
        return true;
    }

    /// <summary>Le niveau que le héros atteindra avec son prochain choix (niveaux en attente compris).</summary>
    private int NextLevelAfterPending(Hero hero) =>
        hero.Level + _pendingChoices.Count(c => c.Hero == hero) + 1;


    private void RemoveHero(Hero hero)
    {
        if (_step?.Hero == hero)
            _step = null;
        hero.Owner.Heroes.Remove(hero);
        _paths.Remove(hero);
        _pendingChoices.RemoveAll(c => c.Hero == hero);
        if (CurrentHero == hero)
            CurrentHero = hero.Owner.Heroes.FirstOrDefault();
        Emit(new HeroDefeated(hero));
        AnnounceDefeat(hero.Owner);
        if (hero.Owner == Players[0] && hero.Owner.IsDefeated)
            EndGame(GameOutcome.Defeat, GameEndReason.HeroesLost);
        else
            CheckVictory(); // le dernier héros d'un adversaire
    }

    private readonly HashSet<Player> _announcedDefeats = new();

    /// <summary>Un joueur qui n'a plus ni héros ni ville est éliminé (annoncé une seule fois).</summary>
    private void AnnounceDefeat(Player player)
    {
        if (player.IsDefeated && _announcedDefeats.Add(player))
            Emit(new PlayerDefeated(player));
    }

    /// <summary>Pour chaque case du chemin : est-elle atteignable aujourd'hui ?</summary>
    /// <summary>
    /// Combien de jours faut-il au héros courant pour aller jusqu'à <paramref name="target"/> (1 = aujourd'hui), en
    /// suivant le chemin qu'il prendrait ; null si on ne peut pas y aller. Ne change rien (pour le curseur).
    /// Comme en marchant : quand le mouvement restant ne suffit pas pour le pas suivant, il faut attendre le lendemain.
    /// </summary>
    public int? DaysTo(Cell target)
    {
        if (CurrentHero is not { } hero)
            return null;
        if (Map.OccupantAt(target) is { } occupant)
            target = occupant.Position;
        if (Pathfinder.FindPath(Map, hero.Position, target, OtherHeroCells(hero, target)) is not { } path)
            return null;
        int days = 1, movement = hero.Movement;
        var previous = hero.Position;
        foreach (var cell in path)
        {
            int cost = Pathfinder.StepCost(Map, previous, cell);
            if (cost > movement)
            {
                days++;
                movement = hero.MaxMovement;
            }
            movement -= cost;
            previous = cell;
        }
        return days;
    }

    public List<bool> PathReachability()
    {
        var result = new List<bool>();
        if (Path == null || CurrentHero is not { } hero)
            return result;
        int movement = hero.Movement;
        var previous = hero.Position;
        foreach (var cell in Path)
        {
            movement -= Pathfinder.StepCost(Map, previous, cell);
            result.Add(movement >= 0);
            previous = cell;
        }
        return result;
    }

    /// <summary>
    /// Les recrues iraient-elles chez le héros de la ville (garnison pleine) et lui feraient-elles dépasser
    /// la limite de commandement de la troupe ? Renvoie ce héros, sinon null. Ne modifie rien.
    /// </summary>
    public Hero? RecruitOverloads(Town town, CreatureType type, int count)
    {
        if (town.Garrison.CanReceive(type) || VisitingHero(town) is not { } hero)
            return null;
        int inArmy = hero.Army.Stacks.FirstOrDefault(s => s.Type == type)?.Count ?? 0;
        return Command.IsOverLimit(type, inArmy + count, hero.Charisma) ? hero : null;
    }

    /// <summary>Prévision d'un combat du héros courant contre cette armée, sans rien modifier (pour l'interface).</summary>
    public BattleReport? PredictBattle(Army enemy, Hero? enemyHero = null) =>
        CurrentHero is { } hero ? Battle.Resolve(hero.Army.Clone(), enemy.Clone(), hero, defenderHero: enemyHero) : null;

    // ------------------------------------------------------------------ villes

    /// <summary>
    /// Recrute des créatures dans une ville du joueur courant. Elles vont dans la garnison,
    /// ou chez le héros présent si la garnison n'a plus de place.
    /// </summary>
    public RecruitResult Recruit(Town town, CreatureType type, int count)
    {
        if (town.Owner != CurrentPlayer || Outcome != GameOutcome.Playing)
            return RecruitResult.NotOwner;
        if (count <= 0 || count > town.Available.GetValueOrDefault(type))
            return RecruitResult.NotAvailable;
        var cost = type.Cost.ToDictionary(c => c.Key, c => c.Value * count);
        if (!CurrentPlayer.CanAfford(cost))
            return RecruitResult.NotEnoughResources;

        var hero = VisitingHero(town);
        Army destination;
        if (town.Garrison.CanReceive(type))
            destination = town.Garrison;
        else if (hero != null && hero.Owner == CurrentPlayer && hero.Army.CanReceive(type))
            destination = hero.Army;
        else
            return RecruitResult.NoRoom;

        CurrentPlayer.Pay(cost);
        town.Available[type] -= count;
        destination.Add(type, count);
        Emit(new CreaturesRecruited(town, type, count, destination != town.Garrison));
        return RecruitResult.Ok;
    }

    /// <summary>
    /// Fait passer une troupe entre la garnison et le héros présent dans la ville.
    /// Comme dans Heroes, un héros garde toujours au moins une troupe.
    /// </summary>
    public bool TransferStack(Town town, bool fromGarrison, int index)
    {
        if (town.Owner != CurrentPlayer || Outcome != GameOutcome.Playing)
            return false;
        if (VisitingHero(town) is not { } hero || hero.Owner != CurrentPlayer)
            return false;
        var (from, to) = fromGarrison ? (town.Garrison, hero.Army) : (hero.Army, town.Garrison);
        if (!fromGarrison && hero.Army.Stacks.Count <= 1)
            return false;
        if (index < 0 || index >= from.Stacks.Count)
            return false;

        var stack = from.Stacks[index];
        var (type, count) = (stack.Type, stack.Count);
        if (!from.MoveStackTo(index, to))
            return false;
        Emit(new StackTransferred(town, hero, type, count, fromGarrison));
        return true;
    }

    /// <summary>
    /// Glisse une troupe dans une ville (étape 7) : entre la garnison et le héros présent, ou dans la même armée (voir
    /// <see cref="Army.MoveTroop"/> : déplacer, fusionner, échanger, partager avec <paramref name="count"/>). Le héros
    /// garde toujours au moins une troupe. Renvoie false si c'est impossible.
    /// </summary>
    public bool MoveTroop(Town town, bool fromGarrison, int index, bool toGarrison, int slot, int? count = null)
    {
        if (town.Owner != CurrentPlayer || Outcome != GameOutcome.Playing)
            return false;
        var hero = VisitingHero(town);
        if ((!fromGarrison || !toGarrison) && (hero == null || hero.Owner != CurrentPlayer))
            return false;
        var from = fromGarrison ? town.Garrison : hero!.Army;
        var to = toGarrison ? town.Garrison : hero!.Army;
        return MoveTroop(from, index, to, slot, count, hero);
    }

    /// <summary>Glisse une troupe dans l'armée d'un héros du joueur courant (réordonner, fusionner, partager).</summary>
    public bool MoveTroop(Hero hero, int index, int slot, int? count = null)
    {
        if (hero.Owner != CurrentPlayer || Outcome != GameOutcome.Playing)
            return false;
        return MoveTroop(hero.Army, index, hero.Army, slot, count, hero);
    }

    private bool MoveTroop(Army from, int index, Army to, int slot, int? count, Hero? hero)
    {
        // le héros ne doit pas se retrouver sans troupe : on essaie sur des copies d'abord
        if (hero != null && from != to && (from == hero.Army || to == hero.Army))
        {
            var (fromCopy, toCopy) = (from.Clone(), to.Clone());
            if (!fromCopy.MoveTroop(index, toCopy, slot, count))
                return false;
            if ((from == hero.Army ? fromCopy : toCopy).IsEmpty)
                return false;
        }
        if (!from.MoveTroop(index, to, slot, count))
            return false;
        Emit(new TroopMoved(hero));
        return true;
    }

    /// <summary>Les héros que la taverne propose (deux, comme dans Heroes 3) : les premiers noms pas encore utilisés.</summary>
    public IReadOnlyList<string> TavernOffers()
    {
        var used = AllHeroes.Select(h => h.Name).ToHashSet();
        return HeroNames.Where(n => !used.Contains(n)).Take(TavernOfferCount).ToList();
    }

    /// <summary>Nombre de héros proposés à la taverne.</summary>
    public const int TavernOfferCount = 2;

    /// <summary>Peut-on recruter un héros dans cette ville ?</summary>
    public HireResult CanHireHero(Town town)
    {
        if (town.Owner != CurrentPlayer || Outcome != GameOutcome.Playing)
            return HireResult.NotOwner;
        if (!town.Buildings.Any(b => b.Tavern && town.Has(b.Id)))
            return HireResult.NoTavern;
        if (CurrentPlayer.Heroes.Count >= MaxHeroesPerPlayer)
            return HireResult.TooManyHeroes;
        if (NextHeroName() == null)
            return HireResult.NoHeroLeft;
        if (VisitingHero(town) != null)
            return HireResult.TownOccupied;
        if (CurrentPlayer.Resources.GetValueOrDefault(ResourceType.Gold) < HeroCost)
            return HireResult.NotEnoughGold;
        return HireResult.Ok;
    }

    /// <summary>
    /// Recrute un héros à la taverne d'une ville : il apparaît à l'entrée de la ville avec quelques
    /// créatures du premier niveau, et devient le héros sélectionné. <paramref name="name"/> : l'un des héros proposés
    /// (<see cref="TavernOffers"/>) ; sans nom, le premier.
    /// </summary>
    public HireResult HireHero(Town town, string? name = null)
    {
        var result = CanHireHero(town);
        if (result != HireResult.Ok)
            return result;
        if (name != null && !TavernOffers().Contains(name))
            return HireResult.NoHeroLeft;
        CurrentPlayer.Resources[ResourceType.Gold] -= HeroCost;
        var hero = new Hero(name ?? NextHeroName()!, CurrentPlayer, town.Position) { Id = _nextHeroId++ };
        var firstTier = Creatures.All.Where(c => c.Faction == town.Faction).OrderBy(c => c.Tier).First();
        hero.Army.Add(firstTier, 12);
        CurrentHero = hero;
        Emit(new HeroHired(hero, town));
        return HireResult.Ok;
    }

    // -------------------------------------------------------------- trésor

    /// <summary>Les obélisques de la carte, dans un ordre fixe : le n-ième dévoile le n-ième morceau de la carte au trésor.</summary>
    public IReadOnlyList<Obelisk> Obelisks => Map.Objects.OfType<Obelisk>().OrderBy(o => o.Id).ToList();

    /// <summary>Pour chaque morceau de la carte au trésor (un par obélisque) : le joueur l'a-t-il dévoilé ?</summary>
    public IReadOnlyList<bool> TreasureMapPieces(Player player) => Obelisks.Select(o => player.Obelisks.Contains(o.Id)).ToList();

    /// <summary>
    /// Le héros courant creuse sur sa case. Il faut une journée entière de mouvement, qui est alors dépensée.
    /// Sur la bonne case, il déterre le trésor : un artefact légendaire, qui fait gagner s'il fallait le trouver.
    /// </summary>
    public DigResult Dig()
    {
        if (Outcome != GameOutcome.Playing || CurrentHero is not { } hero)
            return DigResult.NoHero;
        if (hero.Movement < hero.MaxMovement)
            return DigResult.NeedsFullDay;
        hero.SpendMovement(hero.Movement);
        Path = null;
        if (Map.Treasure != hero.Position || Data.Artifacts.Treasure is not { } artifact)
        {
            Emit(new NothingDug(hero));
            return DigResult.Nothing;
        }
        Map.Treasure = null;
        hero.Equip(artifact);
        if (hero.Owner == Players[0])
            _treasureFound = true;
        Emit(new TreasureDug(hero, artifact));
        CheckVictory();
        return DigResult.Found;
    }

    /// <summary>Le nom du prochain héros que la taverne propose : le premier pas encore utilisé dans la partie.</summary>
    public string? NextHeroName()
    {
        var used = AllHeroes.Select(h => h.Name).ToHashSet();
        return HeroNames.FirstOrDefault(n => !used.Contains(n));
    }

    /// <summary>Le héros qui se trouve dans la ville (sur sa case), s'il y en a un.</summary>
    public Hero? VisitingHero(Town town) => AllHeroes.FirstOrDefault(h => h.Position == town.Position);

    /// <summary>Construit un bâtiment dans une ville du joueur courant.</summary>
    public BuildResult Build(Town town, string buildingId)
    {
        var building = Data.Buildings.Get(buildingId);
        var result = town.CanBuild(building, CurrentPlayer);
        if (result != BuildResult.Ok || Outcome != GameOutcome.Playing)
            return result;
        town.Build(building, CurrentPlayer);
        Emit(new BuildingBuilt(town, building));
        return BuildResult.Ok;
    }
}

/// <summary>Ce que donne <see cref="GameState.Dig"/>.</summary>
public enum DigResult { Found, Nothing, NeedsFullDay, NoHero }

/// <summary>Le héros a creusé au mauvais endroit : il n'y a rien.</summary>
public sealed record NothingDug(Hero Hero) : GameEvent;

/// <summary>Le héros a déterré le trésor : il porte maintenant l'artefact.</summary>
public sealed record TreasureDug(Hero Hero, ArtifactType Artifact) : GameEvent;

/// <summary>
/// Un combat commence (contre un monstre, ou la garnison d'une ville). Avec <see cref="GameState.QuickBattles"/> à
/// false, la partie attend : l'affichage fait jouer <paramref name="Battle"/>, puis appelle <see cref="GameState.FinishBattle"/>.
/// </summary>
public sealed record BattleStarted(Hero Hero, MapObject Defender, TacticalBattle Battle) : GameEvent;
