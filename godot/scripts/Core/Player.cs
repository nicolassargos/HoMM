using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Un joueur : son trésor, ses mines, ses villes, ses héros et son brouillard de guerre.</summary>
public sealed class Player
{
    /// <summary>Identifiant stable du joueur dans la partie.</summary>
    public int Id { get; internal set; }

    public Player(string name, AdventureMap map)
    {
        Name = name;
        Fog = new FogOfWar(map.Width, map.Height);
        // au départ : 2500 or, 10 bois et minerai, 2 de chaque ressource rare
        Resources = new Dictionary<ResourceType, int>
        {
            [ResourceType.Gold] = 2500,
            [ResourceType.Wood] = 10,
            [ResourceType.Ore] = 10,
        };
        foreach (var rare in ResourceInfo.Rare)
            Resources[rare] = 2;
    }

    public string Name { get; }

    /// <summary>Un adversaire joué par l'ordinateur (voir <see cref="ComputerPlayer"/>).</summary>
    public bool IsComputer { get; init; }

    public FogOfWar Fog { get; }
    public Dictionary<ResourceType, int> Resources { get; }
    public List<Mine> Mines { get; } = new();
    public List<Town> Towns { get; } = new();
    public List<Hero> Heroes { get; } = new();

    /// <summary>Les obélisques visités (leurs identifiants) : chacun dévoile un morceau de la carte au trésor.</summary>
    public HashSet<int> Obelisks { get; } = new();

    /// <summary>Quelques chiffres de la partie (pour la fin de partie).</summary>
    public PlayerStatistics Statistics { get; } = new();

    /// <summary>Un de ses héros porte l'Œil du recenseur : il voit le nombre exact des troupes adverses.</summary>
    public bool SeesExactArmies => Heroes.Any(h => h.Artifacts.Any(a => a.ExactArmies));

    /// <summary>Un de ses héros porte la Longue-vue des guetteurs : il voit les alentours de toutes les tours.</summary>
    public bool SeesFromTowers => Heroes.Any(h => h.Artifacts.Any(a => a.RevealsTowers));

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

    /// <summary>Ce que rapportent chaque jour les mines, les villes, l'Intendance et les artefacts des héros.</summary>
    public Dictionary<ResourceType, int> DailyIncome()
    {
        var income = new Dictionary<ResourceType, int>();
        foreach (var mine in Mines)
            income[mine.Resource] = income.GetValueOrDefault(mine.Resource) + mine.DailyProduction;
        // l'Intendance de ses héros rapporte de l'or chaque jour
        int townGold = Towns.Sum(t => t.DailyGold) + Heroes.Sum(h => h.SkillValue(Skills.Estates));
        if (townGold > 0)
            income[ResourceType.Gold] = income.GetValueOrDefault(ResourceType.Gold) + townGold;
        // le silo d'une ville : 1 de la ressource principale de sa faction
        foreach (var town in Towns)
            if (town.SiloResource is { } resource)
                income[resource] = income.GetValueOrDefault(resource) + 1;
        // les artefacts des héros (bourses, brouette, creuset)
        foreach (var (resource, amount) in Heroes.SelectMany(h => h.ArtifactIncome()))
            income[resource] = income.GetValueOrDefault(resource) + amount;
        return income;
    }
}

/// <summary>Un joueur a reçu les revenus de ses mines et de ses villes au début du jour.</summary>
/// <summary>Quelques chiffres de la partie d'un joueur, montrés à la fin de la partie.</summary>
public sealed class PlayerStatistics
{
    /// <summary>Combats gagnés par ses héros.</summary>
    public int BattlesWon { get; internal set; }

    /// <summary>Créatures ennemies tuées par ses héros.</summary>
    public int CreaturesKilled { get; internal set; }

    /// <summary>Créatures perdues par ses héros (combats et fuites).</summary>
    public int CreaturesLost { get; internal set; }

    /// <summary>Villes prises (à un autre joueur ou neutres).</summary>
    public int TownsCaptured { get; internal set; }
}

public sealed record IncomeReceived(Player Player, IReadOnlyDictionary<ResourceType, int> Income) : GameEvent;
