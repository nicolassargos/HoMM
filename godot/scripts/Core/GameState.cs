using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>L'état d'une partie : carte, joueur, héros, calendrier, chemin choisi, journal.</summary>
public sealed class GameState
{
    private static readonly string[] DayNames = { "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche" };
    private const int MaxMessages = 6;

    public GameState(int seed)
    {
        Seed = seed;
        Map = new MapGenerator(seed).Generate();
        Player = new Player("Rouge");
        Hero = new Hero("Sire Roland", Player, Map.Start);
        Map.Reveal(Hero.Position, Hero.VisionRadius);
        AddMessage("Bienvenue, Sire Roland ! Explorez la carte.");
    }

    public int Seed { get; }
    public AdventureMap Map { get; }
    public Player Player { get; }
    public Hero Hero { get; }

    /// <summary>Jour écoulé depuis le début (0 = premier jour).</summary>
    public int Day { get; private set; }

    /// <summary>Chemin prévu pour le héros, ou null.</summary>
    public List<Cell>? Path { get; private set; }

    public List<string> Messages { get; } = new();

    public string DateText =>
        $"Mois {Day / 28 + 1}, semaine {Day / 7 % 4 + 1}, {DayNames[Day % 7]}";

    public void AddMessage(string text)
    {
        Messages.Add(text);
        if (Messages.Count > MaxMessages)
            Messages.RemoveAt(0);
    }

    public void EndTurn()
    {
        Day++;
        Hero.NewDay();
        var income = Player.DailyIncome();
        foreach (var (resource, amount) in income)
            Player.Resources[resource] += amount;

        string text = $"Nouveau jour : {DateText}.";
        if (income.Count > 0)
            text += " Revenus : " + string.Join(", ", income.Select(i => $"+{i.Value} {ResourceInfo.Name(i.Key)}"));
        if (Day % 7 == 0)
            text = "Une nouvelle semaine commence ! " + text;
        AddMessage(text);
    }

    public void SetDestination(Cell target) => Path = Pathfinder.FindPath(Map, Hero.Position, target);

    public void ClearPath() => Path = null;

    public bool CanStartWalking => Path is { Count: > 0 } && Hero.CanStepTo(Map, Path[0]);

    /// <summary>Fait avancer le héros d'une case. Renvoie false s'il ne peut plus avancer.</summary>
    public bool StepAlongPath()
    {
        if (Path is not { Count: > 0 } || !Hero.CanStepTo(Map, Path[0]))
            return false;

        var next = Path[0];
        Path.RemoveAt(0);
        Hero.StepTo(Map, next);

        if (Map.ObjectAt(next) is { } obj)
        {
            AddMessage(obj.Visit(Player));
            if (obj.RemovedOnVisit)
                Map.RemoveObject(obj);
        }
        if (Path.Count == 0)
            Path = null;
        return true;
    }

    /// <summary>Pour chaque case du chemin : est-elle atteignable aujourd'hui ?</summary>
    public List<bool> PathReachability()
    {
        var result = new List<bool>();
        if (Path == null)
            return result;
        int movement = Hero.Movement;
        var previous = Hero.Position;
        foreach (var cell in Path)
        {
            movement -= Pathfinder.StepCost(Map, previous, cell);
            result.Add(movement >= 0);
            previous = cell;
        }
        return result;
    }
}
