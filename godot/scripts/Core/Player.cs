using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>Un joueur : son trésor, ses mines, ses héros et son brouillard de guerre.</summary>
public sealed class Player
{
    public Player(string name, AdventureMap map)
    {
        Name = name;
        Fog = new FogOfWar(map.Width, map.Height);
        Resources = new Dictionary<ResourceType, int>
        {
            [ResourceType.Gold] = 2500,
            [ResourceType.Wood] = 10,
            [ResourceType.Ore] = 10,
            [ResourceType.Gems] = 2,
        };
    }

    public string Name { get; }
    public FogOfWar Fog { get; }
    public Dictionary<ResourceType, int> Resources { get; }
    public List<Mine> Mines { get; } = new();
    public List<Hero> Heroes { get; } = new();

    /// <summary>Un joueur sans héros a perdu (les villes compteront aussi à l'étape 2).</summary>
    public bool IsDefeated => Heroes.Count == 0;

    public Dictionary<ResourceType, int> DailyIncome()
    {
        var income = new Dictionary<ResourceType, int>();
        foreach (var mine in Mines)
            income[mine.Resource] = income.GetValueOrDefault(mine.Resource) + mine.DailyProduction;
        return income;
    }
}

/// <summary>Un joueur a reçu les revenus de ses mines au début du jour.</summary>
public sealed record IncomeReceived(Player Player, IReadOnlyDictionary<ResourceType, int> Income) : GameEvent;
