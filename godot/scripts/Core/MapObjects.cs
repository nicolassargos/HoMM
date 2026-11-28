using System;
using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>
/// Les 7 ressources (comme Heroes 2 et 3) : l'or, le bois et le minerai, communs ; le mercure, le cristal, le soufre et
/// les gemmes, rares. Les nouvelles s'ajoutent à la fin (les sauvegardes gardent les noms).
/// </summary>
public enum ResourceType { Gold, Wood, Ore, Gems, Mercury, Crystal, Sulfur }

public static class ResourceInfo
{
    public static readonly ResourceType[] All = Enum.GetValues<ResourceType>();

    /// <summary>
    /// Les ressources rares : chaque faction en demande deux (data/classes.json : la principale pour son habitation et sa
    /// créature du niveau 7, la secondaire pour celles des niveaux 5 et 6), et la guilde des mages un peu de chacune.
    /// </summary>
    public static readonly ResourceType[] Rare = { ResourceType.Mercury, ResourceType.Crystal, ResourceType.Sulfur, ResourceType.Gems };

    public static bool IsRare(ResourceType resource) => Array.IndexOf(Rare, resource) >= 0;

    /// <summary>
    /// Ce qu'une mine produit chaque jour : 1000 or (comme Heroes 3 ; 250 avant le 2026-11-11, 500 avant le 2026-11-20, quand
    /// les mines d'or sont devenues moins nombreuses), 2 bois ou minerai, 1 d'une ressource rare.
    /// </summary>
    public static int MineProduction(ResourceType resource) => resource switch
    {
        ResourceType.Gold => 1000,
        _ when IsRare(resource) => 1,
        _ => 2,
    };
}

/// <summary>Un objet que le héros peut visiter en s'arrêtant sur sa case.</summary>
public abstract class MapObject
{
    protected MapObject(Cell position) => Position = position;

    /// <summary>Identifiant stable sur la carte, donné à l'ajout (voir <see cref="AdventureMap.AddObject"/>).</summary>
    public int Id { get; internal set; }

    /// <summary>La case où le héros s'arrête pour visiter l'objet (l'entrée, pour une ville).</summary>
    public Cell Position { get; }

    /// <summary>
    /// Toutes les cases occupées par l'objet. Par défaut, seulement sa case ; une ville en occupe 5 × 4, une mine 3 × 2,
    /// un lieu haut 2 (<see cref="TallSite"/>).
    /// Les cases autres que <see cref="Position"/> sont infranchissables.
    /// </summary>
    public virtual IEnumerable<Cell> Footprint
    {
        get { yield return Position; }
    }

    /// <summary>L'objet disparaît-il de la carte après la visite ?</summary>
    public virtual bool RemovedOnVisit => true;

    /// <summary>Applique l'effet de l'objet au héros (et à son joueur) et renvoie ce qui s'est passé.</summary>
    public abstract ObjectVisited Visit(Hero hero);
}

/// <summary>
/// Un héros attaqué par un héros d'un autre joueur (étape 8), vu comme l'adversaire d'un combat (comme un monstre ou
/// une ville) : il n'est pas posé sur la carte, et on ne le visite pas.
/// </summary>
public sealed class HeroDefender : MapObject
{
    public HeroDefender(Hero hero) : base(hero.Position) => Hero = hero;

    public Hero Hero { get; }

    public override bool RemovedOnVisit => false;

    public override ObjectVisited Visit(Hero hero) =>
        throw new InvalidOperationException("Un héros se combat, il ne se visite pas.");
}

/// <summary>Un héros a visité un objet. Chaque type d'objet a son propre événement, qui décrit l'effet.</summary>
public abstract record ObjectVisited(Hero Hero, MapObject Object) : GameEvent;

/// <summary>Le héros a ramassé des ressources (tas ou coffre).</summary>
public sealed record ResourcesFound(Hero Hero, MapObject Object, ResourceType Resource, int Amount) : ObjectVisited(Hero, Object);

public sealed class ResourcePile : MapObject
{
    public ResourcePile(Cell position, ResourceType resource, Random rng)
        : this(position, resource, resource switch
        {
            ResourceType.Gold => rng.Next(5, 11) * 100,
            _ when ResourceInfo.IsRare(resource) => rng.Next(3, 7),
            _ => rng.Next(5, 11),
        })
    {
    }

    public ResourcePile(Cell position, ResourceType resource, int amount) : base(position)
    {
        Resource = resource;
        Amount = amount;
    }

    public ResourceType Resource { get; }
    public int Amount { get; }

    public override ObjectVisited Visit(Hero hero)
    {
        hero.Owner.Resources[Resource] = hero.Owner.Resources.GetValueOrDefault(Resource) + Amount;
        return new ResourcesFound(hero, this, Resource, Amount);
    }
}

/// <summary>
/// Le héros a ouvert un coffre : son joueur choisit l'or ou l'expérience (<see cref="GameState.ChooseChest"/>). En
/// attendant, le choix reste dans <see cref="GameState.PendingChests"/>.
/// </summary>
public sealed record ChestOpened(Hero Hero, TreasureChest Chest) : ObjectVisited(Hero, Chest);

/// <summary>Le coffre a donné de l'or (<paramref name="Gold"/>) ou de l'expérience.</summary>
public sealed record ChestChosen(Hero Hero, bool Gold, int Amount) : GameEvent;

/// <summary>
/// Un coffre au trésor : comme dans Heroes 2 et 3, le joueur choisit 1000, 1500 ou 2000 or, ou 500 de moins en
/// expérience (choix de l'utilisateur, 2026-11-08).
/// </summary>
public sealed class TreasureChest : MapObject
{
    private static readonly int[] PossibleGold = { 1000, 1500, 2000 };

    public TreasureChest(Cell position, Random rng) : this(position, PossibleGold[rng.Next(PossibleGold.Length)])
    {
    }

    public TreasureChest(Cell position, int gold, ArtifactType? artifact = null) : base(position)
    {
        Gold = gold;
        Artifact = artifact;
    }

    public int Gold { get; }

    /// <summary>L'expérience offerte à la place de l'or.</summary>
    public int Experience => Gold - 500;

    /// <summary>Un artefact mineur, dans certains coffres : le héros le prend, sans choix (comme dans Heroes 3).</summary>
    public ArtifactType? Artifact { get; internal set; }

    /// <summary>Rien n'est donné tout de suite : le joueur choisit (voir <see cref="ChestOpened"/>) ; sauf l'artefact.</summary>
    public override ObjectVisited Visit(Hero hero)
    {
        if (Artifact == null)
            return new ChestOpened(hero, this);
        hero.Equip(Artifact);
        return new ArtifactFound(hero, this, Artifact);
    }
}

/// <summary>Le héros a pris un artefact (posé sur la carte, ou dans un coffre).</summary>
public sealed record ArtifactFound(Hero Hero, MapObject Object, ArtifactType Artifact) : ObjectVisited(Hero, Object);

/// <summary>
/// Un artefact posé sur la carte (choix de l'utilisateur, 2026-11-21) : les mineurs seuls, les majeurs et les reliques
/// sous la garde d'un monstre. Le héros qui s'arrête dessus le prend.
/// </summary>
public sealed class ArtifactPile : MapObject
{
    public ArtifactPile(Cell position, ArtifactType artifact) : base(position) => Artifact = artifact;

    public ArtifactType Artifact { get; }

    public override ObjectVisited Visit(Hero hero)
    {
        hero.Equip(Artifact);
        return new ArtifactFound(hero, this, Artifact);
    }
}

/// <summary>Le héros a trouvé un feu de camp abandonné : son or et sa ressource.</summary>
public sealed record CampfireFound(Hero Hero, Campfire Campfire) : ObjectVisited(Hero, Campfire);

/// <summary>
/// Un feu de camp abandonné, comme Heroes 3 (2026-11-28) : 400 à 600 or et une ressource (4 à 6 bois ou minerai, 2 à 4
/// d'une rare). Sans gardien.
/// </summary>
public sealed class Campfire : MapObject
{
    public Campfire(Cell position, ResourceType resource, Random rng)
        : this(position, rng.Next(4, 7) * 100, resource, ResourceInfo.IsRare(resource) ? rng.Next(2, 5) : rng.Next(4, 7))
    {
    }

    public Campfire(Cell position, int gold, ResourceType resource, int amount) : base(position)
    {
        Gold = gold;
        Resource = resource;
        Amount = amount;
    }

    public int Gold { get; }
    public ResourceType Resource { get; }
    public int Amount { get; }

    public override ObjectVisited Visit(Hero hero)
    {
        var resources = hero.Owner.Resources;
        resources[ResourceType.Gold] = resources.GetValueOrDefault(ResourceType.Gold) + Gold;
        resources[Resource] = resources.GetValueOrDefault(Resource) + Amount;
        return new CampfireFound(hero, this);
    }
}

/// <summary>Le héros a lu un parchemin. <paramref name="Learned"/> : faux s'il connaissait déjà le sort (le parchemin reste).</summary>
public sealed record ScrollRead(Hero Hero, SpellScroll Scroll, bool Learned) : ObjectVisited(Hero, Scroll);

/// <summary>
/// Un parchemin (2026-11-28) : il enseigne un sort, tiré à la création de la carte selon le danger de sa case. Il
/// disparaît une fois appris ; un héros qui connaît déjà le sort le laisse à un autre.
/// </summary>
public sealed class SpellScroll : MapObject
{
    public SpellScroll(Cell position, SpellType spell) : base(position) => Spell = spell;

    public SpellType Spell { get; }

    private bool _read;

    public override bool RemovedOnVisit => _read;

    public override ObjectVisited Visit(Hero hero)
    {
        _read = hero.Learn(Spell);
        return new ScrollRead(hero, this, _read);
    }
}

/// <summary>
/// La tombe d'un aventurier (2026-11-28) : un artefact mineur une fois sur deux, sinon 300 à 500 or. Sans gardien.
/// </summary>
public sealed class Remains : MapObject
{
    public Remains(Cell position, int gold, ArtifactType? artifact = null) : base(position)
    {
        Gold = gold;
        Artifact = artifact;
    }

    public int Gold { get; }

    /// <summary>L'artefact mineur qu'il portait, sinon de l'or.</summary>
    public ArtifactType? Artifact { get; internal set; }

    public override ObjectVisited Visit(Hero hero)
    {
        if (Artifact != null)
        {
            hero.Equip(Artifact);
            return new ArtifactFound(hero, this, Artifact);
        }
        hero.Owner.Resources[ResourceType.Gold] = hero.Owner.Resources.GetValueOrDefault(ResourceType.Gold) + Gold;
        return new ResourcesFound(hero, this, ResourceType.Gold, Gold);
    }
}

/// <summary>Le héros a visité un obélisque. <paramref name="FirstVisit"/> : un nouveau morceau de la carte au trésor.</summary>
public sealed record ObeliskVisited(Hero Hero, Obelisk Obelisk, bool FirstVisit) : ObjectVisited(Hero, Obelisk);

/// <summary>
/// Un obélisque : la première visite d'un joueur dévoile un morceau de la carte au trésor (voir
/// <see cref="GameState.TreasureMapPieces"/>). Il reste sur la carte.
/// </summary>
public sealed class Obelisk : MapObject
{
    public Obelisk(Cell position) : base(position)
    {
    }

    public override bool RemovedOnVisit => false;

    public override ObjectVisited Visit(Hero hero) => new ObeliskVisited(hero, this, hero.Owner.Obelisks.Add(Id));
}

/// <summary>Le héros a pris une mine. <paramref name="PreviousOwner"/> : son ancien propriétaire, s'il y en avait un.</summary>
public sealed record MineCaptured(Hero Hero, Mine Mine, Player? PreviousOwner) : ObjectVisited(Hero, Mine);

/// <summary>Le héros est passé par une mine qui appartenait déjà à son joueur.</summary>
public sealed record MineAlreadyOwned(Hero Hero, Mine Mine) : ObjectVisited(Hero, Mine);

/// <summary>Une mine reste sur la carte : une fois capturée, elle rapporte chaque jour.</summary>
public sealed class Mine : MapObject
{
    public Mine(Cell position, ResourceType resource) : base(position) => Resource = resource;

    /// <summary>
    /// La mine occupe 3 × 2 cases, comme dans Heroes 3 (choix de l'utilisateur, 2026-11-07) : <see cref="MapObject.Position"/>
    /// est l'entrée, au milieu de la rangée du bas ; le héros y entre par la case juste en dessous, les 5 autres cases
    /// bloquent le passage. Les mines des sauvegardes d'avant (version 2) gardent une seule case (<see cref="Large"/> faux).
    /// </summary>
    public override IEnumerable<Cell> Footprint => Large ? FootprintAt(Position) : base.Footprint;

    /// <summary>Une mine sur 3 × 2 cases (sinon une seule, celle des anciennes sauvegardes).</summary>
    public bool Large { get; init; } = true;

    /// <summary>Les 6 cases d'une mine dont l'entrée serait en <paramref name="entrance"/>.</summary>
    public static IEnumerable<Cell> FootprintAt(Cell entrance)
    {
        for (int dy = -1; dy <= 0; dy++)
        for (int dx = -1; dx <= 1; dx++)
            yield return entrance.Offset(dx, dy);
    }

    /// <summary>La case d'où l'on entre dans la mine (juste sous l'entrée).</summary>
    public Cell Approach => Position.Offset(0, 1);

    public ResourceType Resource { get; }
    /// <summary>Le propriétaire, changé par une visite (ou au chargement d'une partie).</summary>
    public Player? Owner { get; internal set; }
    public override bool RemovedOnVisit => false;

    public int DailyProduction => ResourceInfo.MineProduction(Resource);

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
/// sur une des 8 cases voisines. Elle grossit de 10 % chaque semaine, jusqu'au double de sa taille de départ.
/// </summary>
public sealed class MonsterGuard : MapObject
{
    public MonsterGuard(Cell position, CreatureType type, int count) : this(position, type, count, count)
    {
    }

    /// <summary>Une troupe qui a déjà grossi ou subi des pertes (partie chargée).</summary>
    public MonsterGuard(Cell position, CreatureType type, int initialCount, int count) : base(position)
    {
        Type = type;
        Army.Add(type, count);
        InitialCount = initialCount;
    }

    public CreatureType Type { get; }
    public Army Army { get; } = new();
    public int Count => Army.TotalCount;

    /// <summary>Le combat est géré par <see cref="GameState"/>, pas par une simple visite.</summary>
    public override ObjectVisited Visit(Hero hero) =>
        throw new InvalidOperationException("Un monstre se combat (GameState), il ne se visite pas.");

    /// <summary>Taille de la troupe à sa création (la croissance s'arrête au double).</summary>
    public int InitialCount { get; }

    public void GrowWeekly()
    {
        foreach (var stack in Army.Stacks)
            stack.Count = Math.Min(stack.Count + Math.Max(1, stack.Count / 10), 2 * InitialCount);
    }
}
