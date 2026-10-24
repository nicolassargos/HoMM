using System;

namespace HoMM.Core;

public enum ResourceType { Gold, Wood, Ore, Gems }

public static class ResourceInfo
{
    public static readonly ResourceType[] All = Enum.GetValues<ResourceType>();
}

/// <summary>Un objet que le héros peut visiter en s'arrêtant sur sa case.</summary>
public abstract class MapObject
{
    protected MapObject(Cell position) => Position = position;

    public Cell Position { get; }

    /// <summary>L'objet disparaît-il de la carte après la visite ?</summary>
    public virtual bool RemovedOnVisit => true;

    /// <summary>Applique l'effet de l'objet au héros (et à son joueur) et renvoie ce qui s'est passé.</summary>
    public abstract ObjectVisited Visit(Hero hero);
}

/// <summary>Un héros a visité un objet. Chaque type d'objet a son propre événement, qui décrit l'effet.</summary>
public abstract record ObjectVisited(Hero Hero, MapObject Object) : GameEvent;

/// <summary>Le héros a ramassé des ressources (tas ou coffre).</summary>
public sealed record ResourcesFound(Hero Hero, MapObject Object, ResourceType Resource, int Amount) : ObjectVisited(Hero, Object);

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

    public override ObjectVisited Visit(Hero hero)
    {
        hero.Owner.Resources[Resource] += Amount;
        return new ResourcesFound(hero, this, Resource, Amount);
    }
}

public sealed class TreasureChest : MapObject
{
    private static readonly int[] PossibleGold = { 1000, 1500, 2000 };

    public TreasureChest(Cell position, Random rng) : base(position) =>
        Gold = PossibleGold[rng.Next(PossibleGold.Length)];

    public int Gold { get; }

    public override ObjectVisited Visit(Hero hero)
    {
        hero.Owner.Resources[ResourceType.Gold] += Gold;
        return new ResourcesFound(hero, this, ResourceType.Gold, Gold);
    }
}

/// <summary>Le héros a pris une mine. <paramref name="PreviousOwner"/> : son ancien propriétaire, s'il y en avait un.</summary>
public sealed record MineCaptured(Hero Hero, Mine Mine, Player? PreviousOwner) : ObjectVisited(Hero, Mine);

/// <summary>Le héros est passé par une mine qui appartenait déjà à son joueur.</summary>
public sealed record MineAlreadyOwned(Hero Hero, Mine Mine) : ObjectVisited(Hero, Mine);

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

    public override ObjectVisited Visit(Hero hero)
    {
        var player = hero.Owner;
        if (Owner == player)
            return new MineAlreadyOwned(hero, this);
        var previous = Owner;
        previous?.Mines.Remove(this);
        Owner = player;
        player.Mines.Add(this);
        return new MineCaptured(hero, this, previous);
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

    /// <summary>Le combat est géré par <see cref="GameState"/>, pas par une simple visite.</summary>
    public override ObjectVisited Visit(Hero hero) =>
        throw new InvalidOperationException("Un monstre se combat (GameState), il ne se visite pas.");

    public void GrowWeekly()
    {
        foreach (var stack in Army.Stacks)
            stack.Count += Math.Max(1, stack.Count / 10);
    }
}
