using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Pourquoi un bâtiment peut (ou ne peut pas) être construit.</summary>
public enum BuildResult { Ok, NotOwner, AlreadyBuilt, AlreadyBuiltToday, MissingRequirement, NotEnoughResources }

/// <summary>Pourquoi un recrutement peut (ou ne peut pas) se faire.</summary>
public enum RecruitResult { Ok, NotOwner, NotAvailable, NotEnoughResources, NoRoom }

/// <summary>
/// Une ville : ses bâtiments, ses créatures à recruter et sa garnison.
/// Le héros y entre en s'arrêtant sur sa case. Une ville qui n'est pas à lui est prise
/// après avoir vaincu sa garnison (voir <see cref="GameState"/>).
/// </summary>
public sealed class Town : MapObject
{
    private readonly GameData _data;
    private readonly HashSet<string> _built = new();

    public Town(Cell position, string name, string faction, GameData data) : base(position)
    {
        Name = name;
        Faction = faction;
        _data = data;
    }

    /// <summary>Nom propre de la ville (donnée de jeu, comme le nom d'un héros).</summary>
    public string Name { get; }
    public string Faction { get; }
    public Player? Owner { get; private set; }
    public Army Garrison { get; } = new();
    public override bool RemovedOnVisit => false;

    /// <summary>Un seul bâtiment par jour, comme dans Heroes.</summary>
    public bool BuiltToday { get; private set; }

    /// <summary>Créatures prêtes à être recrutées, par type (elles s'accumulent de semaine en semaine).</summary>
    public Dictionary<CreatureType, int> Available { get; } = new();

    public IReadOnlyCollection<string> Built => _built;
    public bool Has(string buildingId) => _built.Contains(buildingId);

    /// <summary>Tous les bâtiments possibles dans cette ville (construits ou non).</summary>
    public IEnumerable<BuildingType> Buildings => _data.Buildings.OfFaction(Faction);

    public IEnumerable<BuildingType> BuiltBuildings => _built.Select(_data.Buildings.Get);

    /// <summary>Or rapporté chaque jour : seul le meilleur bâtiment compte (hôtel de ville remplace la salle du village).</summary>
    public int DailyGold => BuiltBuildings.Select(b => b.DailyGold).DefaultIfEmpty(0).Max();

    public BuildResult CanBuild(BuildingType building, Player player)
    {
        if (Owner != player)
            return BuildResult.NotOwner;
        if (Has(building.Id))
            return BuildResult.AlreadyBuilt;
        // Les raisons durables d'abord : c'est ce que le joueur a besoin de savoir
        if (building.Requires.Any(r => !Has(r)))
            return BuildResult.MissingRequirement;
        if (!player.CanAfford(building.Cost))
            return BuildResult.NotEnoughResources;
        if (BuiltToday)
            return BuildResult.AlreadyBuiltToday;
        return BuildResult.Ok;
    }

    /// <summary>Construit le bâtiment et le paie (à vérifier avant avec <see cref="CanBuild"/>).</summary>
    public void Build(BuildingType building, Player player)
    {
        player.Pay(building.Cost);
        BuiltToday = true;
        AddBuilding(building);
    }

    /// <summary>Ajoute un bâtiment sans le payer (bâtiments de départ).</summary>
    public void AddBuilding(BuildingType building)
    {
        if (!_built.Add(building.Id))
            return;
        // Une nouvelle habitation offre tout de suite une semaine de créatures, comme dans Heroes
        if (DwellingCreature(building) is { } creature)
            Available[creature] = Available.GetValueOrDefault(creature) + creature.Growth;
    }

    /// <summary>Combien de créatures de ce type le joueur peut recruter : limité par les disponibles et par ses ressources.</summary>
    public int MaxRecruitable(CreatureType type, Player player)
    {
        int max = Available.GetValueOrDefault(type);
        foreach (var (resource, price) in type.Cost)
            if (price > 0)
                max = System.Math.Min(max, player.Resources.GetValueOrDefault(resource) / price);
        return max;
    }

    public CreatureType? DwellingCreature(BuildingType building) =>
        building.Dwelling is { } id ? _data.Creatures.Get(id) : null;

    public void NewDay() => BuiltToday = false;

    /// <summary>Chaque semaine, chaque habitation produit de nouvelles créatures.</summary>
    public void NewWeek()
    {
        foreach (var building in BuiltBuildings)
            if (DwellingCreature(building) is { } creature)
                Available[creature] = Available.GetValueOrDefault(creature) + creature.Growth;
    }

    /// <summary>La ville change de propriétaire.</summary>
    public void SetOwner(Player? player)
    {
        Owner?.Towns.Remove(this);
        Owner = player;
        player?.Towns.Add(this);
    }

    /// <summary>
    /// Le héros s'arrête sur la ville. Sa garnison doit déjà avoir été vaincue
    /// (c'est <see cref="GameState"/> qui gère le combat).
    /// </summary>
    public override ObjectVisited Visit(Hero hero)
    {
        if (Owner == hero.Owner)
            return new TownEntered(hero, this);
        var previous = Owner;
        SetOwner(hero.Owner);
        return new TownCaptured(hero, this, previous);
    }
}

/// <summary>Un héros est entré dans une ville de son joueur.</summary>
public sealed record TownEntered(Hero Hero, Town Town) : ObjectVisited(Hero, Town);

/// <summary>Un héros a pris une ville. <paramref name="PreviousOwner"/> : son ancien propriétaire (null si elle était neutre).</summary>
public sealed record TownCaptured(Hero Hero, Town Town, Player? PreviousOwner) : ObjectVisited(Hero, Town);

/// <summary>Un bâtiment a été construit dans une ville.</summary>
public sealed record BuildingBuilt(Town Town, BuildingType Building) : GameEvent;

/// <summary>Des créatures ont été recrutées dans une ville. <paramref name="ToHero"/> : elles ont rejoint le héros présent (garnison pleine).</summary>
public sealed record CreaturesRecruited(Town Town, CreatureType Type, int Count, bool ToHero) : GameEvent;

/// <summary>Une troupe est passée entre la garnison et le héros présent. <paramref name="ToHero"/> : de la garnison vers le héros.</summary>
public sealed record StackTransferred(Town Town, Hero Hero, CreatureType Type, int Count, bool ToHero) : GameEvent;
