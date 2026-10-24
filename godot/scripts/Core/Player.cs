using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>Un joueur : son trésor et ses mines.</summary>
public sealed class Player
{
    public Player(string name)
    {
        Name = name;
        Resources = new Dictionary<ResourceType, int>
        {
            [ResourceType.Gold] = 2500,
            [ResourceType.Wood] = 10,
            [ResourceType.Ore] = 10,
            [ResourceType.Gems] = 2,
        };
    }

    public string Name { get; }
    public Dictionary<ResourceType, int> Resources { get; }
    public List<Mine> Mines { get; } = new();

    public Dictionary<ResourceType, int> DailyIncome()
    {
        var income = new Dictionary<ResourceType, int>();
        foreach (var mine in Mines)
            income[mine.Resource] = income.GetValueOrDefault(mine.Resource) + mine.DailyProduction;
        return income;
    }
}
