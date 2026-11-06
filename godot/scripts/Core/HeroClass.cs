using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Un héros que l'on peut choisir ou recruter (data/heroes.json) : seulement un nom (son portrait est trouvé d'après son
/// nom). Tout le reste vient de sa classe (étape 9, choix de l'utilisateur).
/// </summary>
public sealed record HeroType
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
}

/// <summary>Tous les héros du jeu, dans l'ordre (celui de la taverne ; les adversaires prennent la fin de la liste).</summary>
public sealed class HeroCatalog
{
    private readonly List<HeroType> _all;

    public HeroCatalog(IEnumerable<HeroType> heroes) => _all = heroes.ToList();

    public static readonly HeroCatalog Empty = new(new HeroType[0]);

    public IReadOnlyList<HeroType> All => _all;

    public HeroType? ByName(string name) => _all.FirstOrDefault(h => h.Name == name);

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
}

/// <summary>Toutes les classes de héros.</summary>
public sealed class ClassCatalog
{
    private readonly List<HeroClass> _all;

    public ClassCatalog(IEnumerable<HeroClass> classes) => _all = classes.ToList();

    public static readonly ClassCatalog Empty = new(new HeroClass[0]);

    public IReadOnlyList<HeroClass> All => _all;

    public HeroClass? Get(string id) => _all.FirstOrDefault(c => c.Id == id);

    /// <summary>La classe des héros de cette faction (null s'il n'y en a pas : le héros garde les valeurs de base).</summary>
    public HeroClass? ForFaction(string faction) => _all.FirstOrDefault(c => c.Faction == faction);

    private sealed record CatalogFile(List<HeroClass> Classes);

    public static ClassCatalog FromJson(string json) => new(GameData.Parse<CatalogFile>(json, "classes").Classes);
}
