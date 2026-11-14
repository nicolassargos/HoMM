using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Un bateau amarré sur l'eau (la mer, conçue avec l'utilisateur le 2026-11-14, comme Heroes 3). Un héros y monte en
/// entrant sur sa case depuis la terre : le bateau quitte la carte et le porte (un héros sur l'eau est en bateau) ; en
/// débarquant, il le laisse sur la case d'eau qu'il quitte. Monter à bord ou débarquer prend tout le mouvement qui reste.
/// Un bateau n'appartient à personne : le premier qui monte le prend.
/// </summary>
public sealed class Boat : MapObject
{
    public Boat(Cell position) : base(position)
    {
    }

    public override bool RemovedOnVisit => true;

    /// <summary>Monter à bord se fait par <see cref="GameState"/> (le héros part sur l'eau), pas par une visite.</summary>
    public override ObjectVisited Visit(Hero hero) =>
        throw new InvalidOperationException("On monte dans un bateau (GameState), on ne le visite pas.");
}

/// <summary>Le héros est monté dans le bateau de cette case : il est en mer, son mouvement du jour est fini.</summary>
public sealed record BoatBoarded(Hero Hero, Cell Position) : GameEvent;

/// <summary>Le héros a débarqué : son bateau reste amarré sur <paramref name="BoatPosition"/>.</summary>
public sealed record BoatLeft(Hero Hero, Cell BoatPosition) : GameEvent;

/// <summary>
/// Un chantier naval sur la côte (un lieu de la carte) : on y achète un bateau, qui apparaît sur l'eau à côté
/// (<see cref="BoatSpot"/>). Il reste sur la carte.
/// </summary>
public sealed class Shipyard : MapObject
{
    public Shipyard(Cell position, Cell boatSpot) : base(position) => BoatSpot = boatSpot;

    /// <summary>La case d'eau où le chantier met ses bateaux.</summary>
    public Cell BoatSpot { get; }

    public override bool RemovedOnVisit => false;

    public override ObjectVisited Visit(Hero hero) => new ShipyardVisited(hero, this);
}

/// <summary>Le héros passe au chantier naval de la côte : son joueur peut y acheter un bateau (<see cref="GameState.BuyBoat"/>).</summary>
public sealed record ShipyardVisited(Hero Hero, Shipyard Shipyard) : ObjectVisited(Hero, Shipyard);

/// <summary>Un bateau construit par un chantier naval (d'une ville ou de la côte), sur <paramref name="Position"/>.</summary>
public sealed record BoatBuilt(Player Player, Cell Position) : GameEvent;

/// <summary>Pourquoi un bateau peut (ou ne peut pas) se construire.</summary>
public enum BoatResult { Ok, NoShipyard, NotOwner, SpotTaken, NotEnoughResources }

/// <summary>Les règles des bateaux qui ne dépendent pas de la partie.</summary>
public static class Boats
{
    /// <summary>Le prix d'un bateau, comme Heroes 3.</summary>
    public static readonly IReadOnlyDictionary<ResourceType, int> Cost = new Dictionary<ResourceType, int>
    {
        [ResourceType.Gold] = 1000,
        [ResourceType.Wood] = 10,
    };

    /// <summary>
    /// Le prix d'un bateau, moins la baisse de la Navigation (en pourcentage : celle du meilleur marin du joueur).
    /// </summary>
    public static IReadOnlyDictionary<ResourceType, int> CostWith(int discount) =>
        Cost.ToDictionary(c => c.Key, c => c.Value * (100 - discount) / 100);

    /// <summary>
    /// La case d'eau où une ville met ses bateaux : une case d'eau qui touche ses murs ou la case devant sa porte, la plus
    /// proche de sa porte (null si la ville n'est pas au bord de l'eau).
    /// </summary>
    public static Cell? SpotFor(AdventureMap map, Town town)
    {
        var around = town.Footprint.Append(town.Approach).ToHashSet();
        return around.SelectMany(c => new[] { c.Offset(1, 0), c.Offset(-1, 0), c.Offset(0, 1), c.Offset(0, -1) })
            .Where(c => !around.Contains(c) && map.IsWater(c))
            .Distinct()
            .OrderBy(c => Math.Abs(c.X - town.Approach.X) + Math.Abs(c.Y - town.Approach.Y)).ThenBy(c => c.Y).ThenBy(c => c.X)
            .Cast<Cell?>().FirstOrDefault();
    }
}

// ------------------------------------------------------------------ les objets de la mer (étape 3 de la mer, 2026-11-14)

/// <summary>Le héros a repêché une épave flottante : du bois et de l'or (parfois rien).</summary>
public sealed record FlotsamFound(Hero Hero, Flotsam Flotsam) : ObjectVisited(Hero, Flotsam);

/// <summary>
/// Une épave flottante, sur l'eau, comme Heroes 3 : rien, 5 bois, 5 bois et 200 or, ou 10 bois et 500 or (une chance sur
/// quatre chacun). Elle disparaît une fois repêchée.
/// </summary>
public sealed class Flotsam : MapObject
{
    private static readonly (int Wood, int Gold)[] Possible = { (0, 0), (5, 0), (5, 200), (10, 500) };

    public Flotsam(Cell position, Random rng) : this(position, Possible[rng.Next(Possible.Length)])
    {
    }

    private Flotsam(Cell position, (int Wood, int Gold) loot) : this(position, loot.Wood, loot.Gold)
    {
    }

    public Flotsam(Cell position, int wood, int gold) : base(position)
    {
        Wood = wood;
        Gold = gold;
    }

    public int Wood { get; }
    public int Gold { get; }

    public override ObjectVisited Visit(Hero hero)
    {
        var resources = hero.Owner.Resources;
        resources[ResourceType.Wood] = resources.GetValueOrDefault(ResourceType.Wood) + Wood;
        resources[ResourceType.Gold] = resources.GetValueOrDefault(ResourceType.Gold) + Gold;
        return new FlotsamFound(hero, this);
    }
}

/// <summary>Le héros a ouvert un coffre de la mer : <paramref name="Gold"/> or (0 : il était vide).</summary>
public sealed record SeaChestOpened(Hero Hero, SeaChest Chest, int Gold) : ObjectVisited(Hero, Chest);

/// <summary>
/// Un coffre de la mer, qui flotte : vide une fois sur cinq, sinon 1000 ou 1500 or (plus tard, parfois un artefact, comme
/// Heroes 3). Il disparaît une fois ouvert.
/// </summary>
public sealed class SeaChest : MapObject
{
    public SeaChest(Cell position, Random rng) : this(position, rng.Next(5) == 0 ? 0 : rng.Next(2) == 0 ? 1000 : 1500)
    {
    }

    public SeaChest(Cell position, int gold) : base(position) => Gold = gold;

    public int Gold { get; }

    public override ObjectVisited Visit(Hero hero)
    {
        hero.Owner.Resources[ResourceType.Gold] = hero.Owner.Resources.GetValueOrDefault(ResourceType.Gold) + Gold;
        return new SeaChestOpened(hero, this, Gold);
    }
}

/// <summary>
/// Le bateau est passé par un tourbillon : il ressort par <paramref name="Exit"/> (null : un héros y était déjà, il reste),
/// et la troupe la plus faible a perdu <paramref name="Lost"/> créatures (<paramref name="LostType"/>).
/// </summary>
public sealed record WhirlpoolCrossed(Hero Hero, Whirlpool Whirlpool, Cell? Exit, CreatureType? LostType, int Lost)
    : ObjectVisited(Hero, Whirlpool);

/// <summary>
/// Un tourbillon, sur l'eau : il emporte le bateau jusqu'à l'autre tourbillon de sa paire (<see cref="Exit"/>), et coûte
/// quelques créatures de la troupe la plus faible (choix de l'utilisateur, 2026-11-14 : un dixième, au moins une, jamais
/// la dernière créature de l'armée). Il reste sur la carte.
/// </summary>
public sealed class Whirlpool : MapObject
{
    /// <summary>La part de la troupe la plus faible que le tourbillon emporte (en pourcentage).</summary>
    public const int LossPercent = 10;

    public Whirlpool(Cell position, Cell exit) : base(position) => Exit = exit;

    /// <summary>Le tourbillon où l'on ressort.</summary>
    public Cell Exit { get; }

    public override bool RemovedOnVisit => false;

    /// <summary>Les pertes seulement : le bateau est déplacé par <see cref="GameState"/> (s'il n'y a personne à la sortie).</summary>
    public override ObjectVisited Visit(Hero hero)
    {
        var weakest = hero.Army.Stacks.Where(s => s.Count > 0)
            .OrderBy(s => s.Type.Tier).ThenBy(s => s.Count).FirstOrDefault();
        if (weakest == null || hero.Army.TotalCount <= 1)
            return new WhirlpoolCrossed(hero, this, Exit, null, 0);
        int lost = Math.Min(Math.Max(1, (int)Math.Round(weakest.Count * LossPercent / 100.0)), hero.Army.TotalCount - 1);
        weakest.Count -= lost;
        hero.Army.RemoveEmpty();
        return new WhirlpoolCrossed(hero, this, Exit, weakest.Type, lost);
    }
}

/// <summary>Le héros est passé au phare : il appartient maintenant à son joueur (<paramref name="PreviousOwner"/> : l'ancien).</summary>
public sealed record LighthouseVisited(Hero Hero, Lighthouse Lighthouse, Player? PreviousOwner, bool Captured)
    : ObjectVisited(Hero, Lighthouse);

/// <summary>
/// Un phare, sur la côte, comme Heroes 3 : on le prend en y passant, et les héros de son propriétaire vont plus loin en mer
/// (<see cref="SeaMovementBonus"/> de plus chaque jour commencé en mer, par phare). Un lieu haut (la porte et la case au-dessus).
/// </summary>
public sealed class Lighthouse : MapObject
{
    public const int SeaMovementBonus = 500;

    public Lighthouse(Cell position) : base(position)
    {
    }

    public override IEnumerable<Cell> Footprint => TallSite.FootprintAt(Position);

    /// <summary>Le propriétaire, changé par une visite (ou au chargement d'une partie).</summary>
    public Player? Owner { get; internal set; }

    public override bool RemovedOnVisit => false;

    public override ObjectVisited Visit(Hero hero)
    {
        var previous = Owner;
        Owner = hero.Owner;
        return new LighthouseVisited(hero, this, previous, previous != hero.Owner);
    }
}
