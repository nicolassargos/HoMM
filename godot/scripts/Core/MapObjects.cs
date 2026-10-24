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

    /// <summary>Applique l'effet de l'objet et renvoie le message à afficher.</summary>
    public abstract string Visit(Player player);
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

    public override string Visit(Player player)
    {
        player.Resources[Resource] += Amount;
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

    public override string Visit(Player player)
    {
        player.Resources[ResourceType.Gold] += Gold;
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

    public override string Visit(Player player)
    {
        if (Owner == player)
            return $"{Name} vous appartient déjà.";
        Owner?.Mines.Remove(this);
        Owner = player;
        player.Mines.Add(this);
        return $"{Name} capturée ! +{DailyProduction} {ResourceInfo.Name(Resource)} par jour.";
    }
}
