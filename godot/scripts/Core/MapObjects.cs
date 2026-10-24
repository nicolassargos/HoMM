using System;

namespace HoMM.Core;

public enum ResourceType { Gold, Wood, Ore, Gems }

public static class ResourceInfo
{
    public static readonly ResourceType[] All = Enum.GetValues<ResourceType>();

    public static string Name(ResourceType resource) => resource switch
    {
        ResourceType.Gold => "or",
        ResourceType.Wood => "bois",
        ResourceType.Ore => "minerai",
        ResourceType.Gems => "gemmes",
        _ => resource.ToString(),
    };
}

/// <summary>Un objet que le héros peut visiter en s'arrêtant sur sa case.</summary>
public abstract class MapObject
{
    protected MapObject(Cell position) => Position = position;

    public Cell Position { get; }

    /// <summary>L'objet disparaît-il de la carte après la visite ?</summary>
    public virtual bool RemovedOnVisit => true;

    public abstract string Name { get; }

    /// <summary>Applique l'effet de l'objet au héros (et à son joueur) et renvoie le message à afficher.</summary>
    public abstract string Visit(Hero hero);
}

public sealed class ResourcePile : MapObject
{
    public ResourcePile(Cell position, ResourceType resource, Random rng) : base(position)
    {
        Resource = resource;
        Amount = resource switch
        {
            ResourceType.Gold => rng.Next(5, 11) * 100,
            ResourceType.Gems => rng.Next(3, 7),
            _ => rng.Next(5, 11),
        };
    }

    public ResourceType Resource { get; }
    public int Amount { get; }
    public override string Name => $"Tas de {ResourceInfo.Name(Resource)}";

    public override string Visit(Hero hero)
    {
        hero.Owner.Resources[Resource] += Amount;
        return $"Vous trouvez {Amount} {ResourceInfo.Name(Resource)}.";
    }
}

public sealed class TreasureChest : MapObject
{
    private static readonly int[] PossibleGold = { 1000, 1500, 2000 };

    public TreasureChest(Cell position, Random rng) : base(position) =>
        Gold = PossibleGold[rng.Next(PossibleGold.Length)];

    public int Gold { get; }
    public override string Name => "Coffre au trésor";

    public override string Visit(Hero hero)
    {
        hero.Owner.Resources[ResourceType.Gold] += Gold;
        return $"Un coffre au trésor ! +{Gold} or.";
    }
}

/// <summary>Une mine reste sur la carte : une fois capturée, elle rapporte chaque jour.</summary>
public sealed class Mine : MapObject
{
    public Mine(Cell position, ResourceType resource) : base(position) => Resource = resource;

    public ResourceType Resource { get; }
    public Player? Owner { get; private set; }
    public override bool RemovedOnVisit => false;

    public int DailyProduction => Resource switch
    {
        ResourceType.Gold => 1000,
        ResourceType.Gems => 1,
        _ => 2,
    };

    public override string Name => Resource switch
    {
        ResourceType.Gold => "Mine d'or",
        ResourceType.Wood => "Scierie",
        ResourceType.Ore => "Mine de minerai",
        ResourceType.Gems => "Mine de gemmes",
        _ => "Mine",
    };

    public override string Visit(Hero hero)
    {
        var player = hero.Owner;
        if (Owner == player)
            return $"{Name} vous appartient déjà.";
        Owner?.Mines.Remove(this);
        Owner = player;
        player.Mines.Add(this);
        return $"{Name} capturée ! +{DailyProduction} {ResourceInfo.Name(Resource)} par jour.";
    }
}

/// <summary>
/// Une troupe de monstres neutres. Elle bloque sa case et attaque tout héros qui entre
/// sur une des 8 cases voisines. Elle grossit de 10 % chaque semaine.
/// </summary>
public sealed class MonsterGuard : MapObject
{
    public MonsterGuard(Cell position, CreatureType type, int count) : base(position)
    {
        Type = type;
        Army.Add(type, count);
    }

    public CreatureType Type { get; }
    public Army Army { get; } = new();
    public int Count => Army.TotalCount;

    /// <summary>« Quelques Loups », « Une bande d'Archers »… On ne voit pas le nombre exact, comme dans Heroes.</summary>
    public override string Name => Count switch
    {
        < 5 => $"Quelques {Type.Plural}",
        < 10 => $"Plusieurs {Type.Plural}",
        < 20 => $"Une bande {Of(Type.Plural)}",
        < 50 => $"Beaucoup {Of(Type.Plural)}",
        < 100 => $"Une horde {Of(Type.Plural)}",
        < 250 => $"Une multitude {Of(Type.Plural)}",
        < 500 => $"Une nuée {Of(Type.Plural)}",
        < 1000 => $"Des milliers {Of(Type.Plural)}",
        _ => $"Une légion {Of(Type.Plural)}",
    };

    /// <summary>Le combat est géré par <see cref="GameState"/>, pas par une simple visite.</summary>
    public override string Visit(Hero hero) =>
        throw new InvalidOperationException("Un monstre se combat (GameState), il ne se visite pas.");

    public void GrowWeekly()
    {
        foreach (var stack in Army.Stacks)
            stack.Count += Math.Max(1, stack.Count / 10);
    }

    private static string Of(string word) =>
        "AEIOUYÉÈÊaeiouyéèê".Contains(word[0]) ? $"d'{word}" : $"de {word}";
}
