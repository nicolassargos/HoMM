using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

public enum GameOutcome { Playing, Victory, Defeat }

/// <summary>
/// L'état d'une partie : carte, joueurs, calendrier, chemin choisi, journal.
/// Prévu pour plusieurs joueurs (tour par tour), même s'il n'y a pour l'instant qu'un humain.
/// </summary>
public sealed class GameState
{
    private static readonly string[] DayNames = { "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche" };
    private const int MaxMessages = 6;

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

        AddMessage("Bienvenue, Sire Roland ! Chassez les monstres de la région.");
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

    /// <summary>Le dernier combat, en attendant que l'interface l'affiche (voir <see cref="TakeLastBattle"/>).</summary>
    public BattleReport? LastBattle { get; private set; }

    public List<string> Messages { get; } = new();

    public IEnumerable<Hero> AllHeroes => Players.SelectMany(p => p.Heroes);

    public string DateText =>
        $"Mois {Day / 28 + 1}, semaine {Day / 7 % 4 + 1}, {DayNames[Day % 7]}";

    public void AddMessage(string text)
    {
        Messages.Add(text);
        if (Messages.Count > MaxMessages)
            Messages.RemoveAt(0);
    }

    public BattleReport? TakeLastBattle()
    {
        var report = LastBattle;
        LastBattle = null;
        return report;
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

        foreach (var player in Players)
        {
            var income = player.DailyIncome();
            foreach (var (resource, amount) in income)
                player.Resources[resource] += amount;
            if (player != CurrentPlayer)
                continue;

            string text = $"Nouveau jour : {DateText}.";
            if (income.Count > 0)
                text += " Revenus : " + string.Join(", ", income.Select(i => $"+{i.Value} {ResourceInfo.Name(i.Key)}"));
            if (newWeek)
                text = "Une nouvelle semaine commence, les monstres sont plus nombreux ! " + text;
            AddMessage(text);
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
    /// Entrer à côté d'un monstre déclenche un combat (voir <see cref="LastBattle"/>) et arrête la marche.
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
        hero.StepTo(Map, next);

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
            AddMessage(obj.Visit(hero));
            if (obj.RemovedOnVisit)
                Map.RemoveObject(obj);
        }
        if (Path is { Count: 0 })
            Path = null;
        return !fought;
    }

    /// <summary>Combat automatique. Renvoie true si le héros a gagné.</summary>
    private bool Fight(Hero hero, MonsterGuard guard)
    {
        string enemy = guard.Name;
        var report = Battle.Resolve(hero.Army, guard.Army);
        report.Title = $"Combat contre {LowerFirst(enemy)}";
        LastBattle = report;

        if (report.AttackerWon)
        {
            Map.RemoveObject(guard);
            AddMessage($"Victoire contre {LowerFirst(enemy)} ! Pertes : {BattleReport.DescribeLosses(report.AttackerLosses)}.");
            if (!Map.Objects.OfType<MonsterGuard>().Any())
            {
                Outcome = GameOutcome.Victory;
                AddMessage("La région est libérée de ses monstres. Victoire !");
            }
            return true;
        }

        AddMessage($"{hero.Name} a été vaincu par {LowerFirst(enemy)}.");
        RemoveHero(hero);
        return false;
    }

    private void RemoveHero(Hero hero)
    {
        hero.Owner.Heroes.Remove(hero);
        Path = null;
        if (CurrentHero == hero)
            CurrentHero = hero.Owner.Heroes.FirstOrDefault();
        if (hero.Owner == Players[0] && hero.Owner.IsDefeated)
        {
            Outcome = GameOutcome.Defeat;
            AddMessage("Vous n'avez plus de héros. Défaite...");
        }
    }

    private static string LowerFirst(string s) => s.Length == 0 ? s : char.ToLower(s[0]) + s[1..];

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
