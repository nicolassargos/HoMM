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

    public GameState(int seed, GameData data) : this(seed, data, new MapGenerator(seed, data).Generate())
    {
        var human = new Player("Rouge", Map) { Id = _nextPlayerId++ };
        Players.Add(human);
        var hero = new Hero("Sire Roland", human, Map.Start) { Id = _nextHeroId++ };
        hero.Army.Add(Creatures.Get("paysan"), 30);
        hero.Army.Add(Creatures.Get("archer"), 15);
        hero.Army.Add(Creatures.Get("griffon"), 4);
        CurrentHero = hero;
    }

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
        { "Dame Isolde", "Sire Gauvain", "Dame Morgane", "Sire Perceval", "Dame Viviane", "Sire Tristan", "Dame Enide", "Sire Yvain" };

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
        if (Day > 0)
            return false;
        Rules = rules;
        Emit(new RulesChosen(rules));
        return true;
    }

    /// <summary>Vérifie la victoire après chaque changement qui peut la donner (monstre vaincu, ville prise).</summary>
    private void CheckVictory()
    {
        if (Outcome != GameOutcome.Playing)
            return;
        GameEndReason? reason = Rules.Victory switch
        {
            VictoryCondition.ClearGuards when !Map.Objects.OfType<MonsterGuard>().Any() => GameEndReason.GuardsCleared,
            VictoryCondition.CaptureTowns when Towns.Any() && Towns.All(t => t.Owner == Players[0]) => GameEndReason.TownsCaptured,
            _ => null,
        };
        if (reason is { } won)
            EndGame(GameOutcome.Victory, won);
    }

    // ------------------------------------------------------------------ tours

    /// <summary>Fin du tour du joueur courant. Quand tous les joueurs ont joué, un nouveau jour commence.</summary>
    public void EndTurn()
    {
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
        Path = Pathfinder.FindPath(Map, hero.Position, target, OtherHeroCells(hero));
    }

    public void ClearPath() => Path = null;

    /// <summary>Les cases des autres héros : on ne peut pas les traverser.</summary>
    private HashSet<Cell> OtherHeroCells(Hero hero) =>
        AllHeroes.Where(h => h != hero).Select(h => h.Position).ToHashSet();

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
        Outcome == GameOutcome.Playing && CurrentHero is { } hero && Path is { Count: > 0 } && hero.CanStepTo(Map, Path[0]);

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

        // Un autre héros est sur le chemin : on s'arrête (le chemin reste prévu)
        if (AllHeroes.Any(h => h != hero && h.Position == next))
            return false;

        // Attaque directe d'un monstre voisin : le héros reste sur sa case
        if (Map.ObjectAt(next) is MonsterGuard target)
        {
            hero.SpendMovement(Pathfinder.StepCost(Map, hero.Position, next));
            Path = null;
            Fight(hero, target, target.Army);
            return false;
        }

        Path.RemoveAt(0);
        var from = hero.Position;
        hero.StepTo(Map, next);
        Emit(new HeroMoved(hero, from, next));

        bool fought = false;
        foreach (var guard in Map.GuardsAround(next).ToList())
        {
            fought = true;
            if (!Fight(hero, guard, guard.Army))
                return false;
            // Le monstre visé a été vaincu : inutile d'aller sur sa case
            if (Path is { Count: > 0 } && Path[^1] == guard.Position)
                Path = null;
        }

        // Une ville ennemie ou neutre : il faut d'abord vaincre sa garnison
        if (Map.ObjectAt(next) is Town { Garrison.IsEmpty: false } town && town.Owner != hero.Owner)
        {
            fought = true;
            if (!Fight(hero, town, town.Garrison))
                return false;
        }

        if (Map.ObjectAt(next) is { } obj and not MonsterGuard)
        {
            var visit = obj.Visit(hero);
            if (obj.RemovedOnVisit)
                Map.RemoveObject(obj);
            Emit(visit);
            CheckVictory();
        }
        if (Path is { Count: 0 })
            Path = null;
        return !fought;
    }

    /// <summary>Combat automatique contre un monstre ou une garnison. Renvoie true si le héros a gagné.</summary>
    private bool Fight(Hero hero, MapObject defender, Army defenderArmy)
    {
        var rebels = RollRevolts(hero);
        var before = defenderArmy.Clone().Stacks;
        var report = Battle.Resolve(hero.Army, defenderArmy, hero, rebels);
        Emit(new BattleFought(hero, defender, before, report));

        if (report.AttackerWon)
        {
            GrantExperience(hero, report.Experience);
            if (defender is MonsterGuard guard)
                Map.RemoveObject(guard);
            CheckVictory();
            return true;
        }

        RemoveHero(hero);
        return false;
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
        hero.Owner.Heroes.Remove(hero);
        _paths.Remove(hero);
        _pendingChoices.RemoveAll(c => c.Hero == hero);
        if (CurrentHero == hero)
            CurrentHero = hero.Owner.Heroes.FirstOrDefault();
        Emit(new HeroDefeated(hero));
        if (hero.Owner == Players[0] && hero.Owner.IsDefeated)
            EndGame(GameOutcome.Defeat, GameEndReason.HeroesLost);
    }

    /// <summary>Pour chaque case du chemin : est-elle atteignable aujourd'hui ?</summary>
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
    public BattleReport? PredictBattle(Army enemy) =>
        CurrentHero is { } hero ? Battle.Resolve(hero.Army.Clone(), enemy.Clone(), hero) : null;

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
    /// créatures du premier niveau, et devient le héros sélectionné.
    /// </summary>
    public HireResult HireHero(Town town)
    {
        var result = CanHireHero(town);
        if (result != HireResult.Ok)
            return result;
        CurrentPlayer.Resources[ResourceType.Gold] -= HeroCost;
        var hero = new Hero(NextHeroName()!, CurrentPlayer, town.Position) { Id = _nextHeroId++ };
        var firstTier = Creatures.All.Where(c => c.Faction == town.Faction).OrderBy(c => c.Tier).First();
        hero.Army.Add(firstTier, 12);
        CurrentHero = hero;
        Emit(new HeroHired(hero, town));
        return HireResult.Ok;
    }

    /// <summary>Le premier nom de héros pas encore utilisé dans la partie.</summary>
    private string? NextHeroName()
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
