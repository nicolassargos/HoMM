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
public sealed class GameState
{
    private int _currentPlayer;

    public GameState(int seed, CreatureCatalog creatures)
    {
        Seed = seed;
        Creatures = creatures;
        Map = new MapGenerator(seed, creatures).Generate();

        var human = new Player("Rouge", Map);
        Players.Add(human);
        var hero = new Hero("Sire Roland", human, Map.Start);
        hero.Army.Add(creatures.Get("piquier"), 30);
        hero.Army.Add(creatures.Get("archer"), 12);
        hero.Army.Add(creatures.Get("griffon"), 4);
        CurrentHero = hero;
    }

    public int Seed { get; }
    public CreatureCatalog Creatures { get; }
    public AdventureMap Map { get; }
    public List<Player> Players { get; } = new();
    public Player CurrentPlayer => Players[_currentPlayer];

    /// <summary>Le héros sélectionné du joueur dont c'est le tour (null s'il n'en a plus).</summary>
    public Hero? CurrentHero { get; private set; }

    public GameOutcome Outcome { get; private set; } = GameOutcome.Playing;

    /// <summary>Jour écoulé depuis le début (0 = premier jour).</summary>
    public int Day { get; private set; }

    /// <summary>Chemin prévu pour le héros, ou null.</summary>
    public List<Cell>? Path { get; private set; }

    private readonly List<GameEvent> _events = new();

    public IEnumerable<Hero> AllHeroes => Players.SelectMany(p => p.Heroes);

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

    private void EndGame(GameOutcome outcome)
    {
        Outcome = outcome;
        Path = null;
        Emit(new GameEnded(outcome));
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
            NewDay();
        // Le chemin prévu est gardé d'un jour à l'autre, sauf si c'est au tour d'un autre joueur
        if (_currentPlayer != previous)
        {
            Path = null;
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
    }

    // ------------------------------------------------------------- déplacement

    public void SetDestination(Cell target)
    {
        if (CurrentHero is { } hero && Outcome == GameOutcome.Playing)
            Path = Pathfinder.FindPath(Map, hero.Position, target);
    }

    public void ClearPath() => Path = null;

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

        // Attaque directe d'un monstre voisin : le héros reste sur sa case
        if (Map.ObjectAt(next) is MonsterGuard target)
        {
            hero.SpendMovement(Pathfinder.StepCost(Map, hero.Position, next));
            Path = null;
            Fight(hero, target);
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
            if (!Fight(hero, guard))
                return false;
            // Le monstre visé a été vaincu : inutile d'aller sur sa case
            if (Path is { Count: > 0 } && Path[^1] == guard.Position)
                Path = null;
        }

        if (Map.ObjectAt(next) is { } obj and not MonsterGuard)
        {
            var visit = obj.Visit(hero);
            if (obj.RemovedOnVisit)
                Map.RemoveObject(obj);
            Emit(visit);
        }
        if (Path is { Count: 0 })
            Path = null;
        return !fought;
    }

    /// <summary>Combat automatique. Renvoie true si le héros a gagné.</summary>
    private bool Fight(Hero hero, MonsterGuard guard)
    {
        int enemyCount = guard.Count;
        var report = Battle.Resolve(hero.Army, guard.Army);
        Emit(new BattleFought(hero, guard, enemyCount, report));

        if (report.AttackerWon)
        {
            Map.RemoveObject(guard);
            if (!Map.Objects.OfType<MonsterGuard>().Any())
                EndGame(GameOutcome.Victory);
            return true;
        }

        RemoveHero(hero);
        return false;
    }

    private void RemoveHero(Hero hero)
    {
        hero.Owner.Heroes.Remove(hero);
        Path = null;
        if (CurrentHero == hero)
            CurrentHero = hero.Owner.Heroes.FirstOrDefault();
        Emit(new HeroDefeated(hero));
        if (hero.Owner == Players[0] && hero.Owner.IsDefeated)
            EndGame(GameOutcome.Defeat);
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

    /// <summary>Prévision d'un combat contre ce monstre, sans rien modifier (pour l'interface).</summary>
    public BattleReport? PredictBattle(MonsterGuard guard) =>
        CurrentHero is { } hero ? Battle.Resolve(hero.Army.Clone(), guard.Army.Clone()) : null;
}
