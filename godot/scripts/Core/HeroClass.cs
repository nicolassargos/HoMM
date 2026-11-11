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

    /// <summary>La compétence de départ de la classe, au rang de base (data/skills.json ; null : aucune).</summary>
    public string? Skill { get; init; }

    /// <summary>Le talent de la classe (null : aucun) ; pour l'instant, <see cref="Talents.Necromancy"/>.</summary>
    public string? Talent { get; init; }

    /// <summary>
    /// Les deux ressources rares de la faction : la principale (son habitation et sa créature du niveau 7 ; une mine près
    /// du départ de chaque joueur de cette faction) et la secondaire (ses habitations des niveaux 5 et 6). Choix de
    /// l'utilisateur (2026-11-07) : chaque faction a sa paire, 8 factions pour 4 ressources rares.
    /// </summary>
    public ResourceType? MainResource { get; init; }
    public ResourceType? SecondaryResource { get; init; }
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

    /// <summary>Croisé (le Paladin) : ses troupes font 2 % de dégâts en plus par niveau du héros aux morts-vivants.</summary>
    public const string Crusader = "crusader";
    public const double CrusaderPerLevel = 0.02;

    /// <summary>Chef de guerre (le Barbare) : la Rage de ses troupes monte de 1 de plus tous les 3 niveaux du héros.</summary>
    public const string Warlord = "warlord";
    public const int WarlordLevels = 3;

    /// <summary>Chasseur (le Rôdeur) : ses tireurs font 1 % de dégâts en plus par niveau du héros.</summary>
    public const string Hunter = "hunter";
    public const double HunterPerLevel = 0.01;

    /// <summary>Érudit (le Magicien) : ses sorts sont 2 % plus forts par niveau du héros (dégâts, soins).</summary>
    public const string Scholar = "scholar";
    public const int ScholarPercentPerLevel = 2;

    /// <summary>
    /// Effroi (le Seigneur des abîmes) : l'armée ennemie commence le combat à −1 de moral, −1 de plus tous les 8 niveaux
    /// du héros (sauf ceux que rien n'effraie : morts-vivants, façonnés, niveau 7, braves).
    /// </summary>
    public const string Dread = "dread";
    public const int DreadLevels = 8;

    /// <summary>Tous les talents connus (pour vérifier les données).</summary>
    public static readonly string[] All = { Necromancy, Crusader, Warlord, Hunter, Scholar, Dread };

    /// <summary>Le moral que l'Effroi de ce héros retire à l'armée ennemie au début du combat (0 : aucun).</summary>
    public static int DreadMorale(Hero? hero) =>
        hero?.Class?.Talent == Dread ? 1 + hero.Level / DreadLevels : 0;

    /// <summary>La Rage au plus pour les troupes de ce héros (la Rage de base, plus le Chef de guerre).</summary>
    public static int RageMax(Hero? hero, int baseMax) =>
        baseMax + (hero?.Class?.Talent == Warlord ? hero.Level / WarlordLevels : 0);

    /// <summary>
    /// Les dégâts en plus (en part : 0,1 pour +10 %) d'une troupe de ce héros contre cette cible : le Croisé contre les
    /// morts-vivants, le Chasseur pour un tir.
    /// </summary>
    public static double DamageBonus(Hero? hero, bool ranged, bool targetUndead) => hero?.Class?.Talent switch
    {
        Crusader when targetUndead => CrusaderPerLevel * hero.Level,
        Hunter when ranged => HunterPerLevel * hero.Level,
        _ => 0,
    };
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
