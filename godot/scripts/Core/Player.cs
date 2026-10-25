using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Un joueur : son trésor, ses mines, ses villes, ses héros et son brouillard de guerre.</summary>
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
    public List<Town> Towns { get; } = new();
    public List<Hero> Heroes { get; } = new();

    /// <summary>Un joueur sans héros ni ville a perdu.</summary>
    public bool IsDefeated => Heroes.Count == 0 && Towns.Count == 0;

    public bool CanAfford(IReadOnlyDictionary<ResourceType, int> cost) =>
        cost.All(c => Resources.GetValueOrDefault(c.Key) >= c.Value);

    /// <summary>Paie un coût (à vérifier avant avec <see cref="CanAfford"/>).</summary>
    public void Pay(IReadOnlyDictionary<ResourceType, int> cost)
    {
        foreach (var (resource, amount) in cost)
            Resources[resource] = Resources.GetValueOrDefault(resource) - amount;
    }

    /// <summary>Ce que rapportent chaque jour les mines et les villes.</summary>
    public Dictionary<ResourceType, int> DailyIncome()
    {
        var income = new Dictionary<ResourceType, int>();
        foreach (var mine in Mines)
            income[mine.Resource] = income.GetValueOrDefault(mine.Resource) + mine.DailyProduction;
        int townGold = Towns.Sum(t => t.DailyGold);
        if (townGold > 0)
            income[ResourceType.Gold] = income.GetValueOrDefault(ResourceType.Gold) + townGold;
        return income;
    }
}

/// <summary>Un joueur a reçu les revenus de ses mines et de ses villes au début du jour.</summary>
public sealed record IncomeReceived(Player Player, IReadOnlyDictionary<ResourceType, int> Income) : GameEvent;
