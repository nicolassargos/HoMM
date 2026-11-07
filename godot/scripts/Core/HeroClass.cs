using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Un héros que l'on peut choisir ou recruter (data/heroes.json) : un nom (son portrait est trouvé d'après son nom) et sa
/// faction. Tout le reste vient de sa classe (étape 9, choix de l'utilisateur).
/// </summary>
public sealed record HeroType
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>
    /// La faction du héros : on le choisit avec elle, et les tavernes de ses villes le proposent d'abord. Les héros d'une
    /// faction pas encore jouable (Barbares, Forêt, Tour, faction sombre) l'attendent. Null : de toutes les factions.
    /// </summary>
    public string? Faction { get; init; }
}

/// <summary>Tous les héros du jeu, dans l'ordre (celui de la taverne ; les adversaires prennent la fin de la liste).</summary>
public sealed class HeroCatalog
{
    private readonly List<HeroType> _all;

    public HeroCatalog(IEnumerable<HeroType> heroes) => _all = heroes.ToList();

    public static readonly HeroCatalog Empty = new(new HeroType[0]);

    public IReadOnlyList<HeroType> All => _all;

    public HeroType? ByName(string name) => _all.FirstOrDefault(h => h.Name == name);

    /// <summary>Les héros de cette faction (et ceux de toutes les factions), dans l'ordre.</summary>
    public IReadOnlyList<HeroType> OfFaction(string faction) =>
        _all.Where(h => h.Faction == null || h.Faction == faction).ToList();

    /// <summary>Tous les héros, ceux de cette faction d'abord : une taverne ou un adversaire qui n'en a plus prend les autres.</summary>
    public IReadOnlyList<HeroType> FactionFirst(string faction) =>
        OfFaction(faction).Concat(_all.Where(h => h.Faction != null && h.Faction != faction)).ToList();

    private sealed record CatalogFile(List<HeroType> Heroes);

    public static HeroCatalog FromJson(string json) => new(GameData.Parse<CatalogFile>(json, "héros").Heroes);
}

/// <summary>
/// Une classe de héros (data/classes.json) : ses caractéristiques de départ et ses sorts de départ. Pour l'instant, une
/// classe par faction, et la classe d'un héros vient de la faction qui le recrute (la ville de départ, ou la taverne).
/// Le Paladin (le Château) est équilibré : il se bat bien, lance un peu de magie sainte, et a le plus de charisme.
/// </summary>
public sealed record HeroClass
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Faction { get; init; } = "";
    public int Attack { get; init; } = 1;
    public int Defense { get; init; } = 1;
    public int Power { get; init; } = 1;
    public int Knowledge { get; init; } = 1;
    public int Charisma { get; init; } = Hero.StartingCharisma;

    /// <summary>Les sorts que le héros connaît dès le départ (identifiants de data/spells.json).</summary>
    public List<string> Spells { get; init; } = new();

    /// <summary>L'armée d'un héros de départ de cette classe (les créatures de sa faction).</summary>
    public List<StartingStack> Army { get; init; } = new();

    /// <summary>Le talent de la classe (null : aucun) ; pour l'instant, <see cref="Talents.Necromancy"/>.</summary>
    public string? Talent { get; init; }
}

/// <summary>Une troupe de l'armée de départ d'une classe.</summary>
public sealed record StartingStack(string Creature, int Count);

/// <summary>Les talents des classes (le champ « talent » de data/classes.json).</summary>
public static class Talents
{
    /// <summary>
    /// Nécromancie (le Nécromancien) : après une victoire, une part des ennemis tués se relève en Squelettes dans son
    /// armée, sans dépasser ce que son charisme commande.
    /// </summary>
    public const string Necromancy = "necromancy";

    /// <summary>La créature que relève la nécromancie, et la part relevée : 10 %, plus 2 % par niveau.</summary>
    public const string RaisedCreature = "squelette";
    public const double NecromancyBase = 0.10, NecromancyPerLevel = 0.02;
}

/// <summary>La nécromancie a relevé des créatures dans l'armée d'un héros après sa victoire.</summary>
public sealed record DeadRaised(Hero Hero, CreatureType Type, int Count) : GameEvent;

/// <summary>Toutes les classes de héros.</summary>
public sealed class ClassCatalog
{
    private readonly List<HeroClass> _all;

    public ClassCatalog(IEnumerable<HeroClass> classes) => _all = classes.ToList();

    public static readonly ClassCatalog Empty = new(new HeroClass[0]);

    public IReadOnlyList<HeroClass> All => _all;

    public HeroClass? Get(string id) => _all.FirstOrDefault(c => c.Id == id);

    /// <summary>Les factions qu'on peut jouer : celles qui ont une classe (le Château d'abord).</summary>
    public IReadOnlyList<string> PlayableFactions => _all.Select(c => c.Faction).Distinct().ToList();

    /// <summary>La classe des héros de cette faction (null s'il n'y en a pas : le héros garde les valeurs de base).</summary>
    public HeroClass? ForFaction(string faction) => _all.FirstOrDefault(c => c.Faction == faction);

    private sealed record CatalogFile(List<HeroClass> Classes);

    public static ClassCatalog FromJson(string json) => new(GameData.Parse<CatalogFile>(json, "classes").Classes);
}
