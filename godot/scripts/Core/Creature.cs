using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Un type de créature (Paysan, Loup…). Les valeurs viennent de data/creatures.json.</summary>
public sealed record CreatureType
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Plural { get; init; } = "";
    public string Faction { get; init; } = "";
    public int Tier { get; init; }
    public int Attack { get; init; }
    public int Defense { get; init; }
    public int MinDamage { get; init; }
    public int MaxDamage { get; init; }
    public int HitPoints { get; init; }
    public int Speed { get; init; }

    /// <summary>Points de commandement qu'occupe une créature (étape 3 : charisme). Plus elle est forte, plus elle coûte.</summary>
    public int CommandCost { get; init; }

    /// <summary>Créatures produites chaque semaine par leur habitation (étape 2 : villes).</summary>
    public int Growth { get; init; }

    public Dictionary<ResourceType, int> Cost { get; init; } = new();

    /// <summary>Capacités au combat tactique (voir <see cref="Ability"/>) : « shoots »…</summary>
    public List<string> Abilities { get; init; } = new();

    /// <summary>
    /// Les sorts qu'une créature à bénédiction (le Djinn) lance au hasard sur une troupe amie au début de son tour (leurs
    /// identifiants dans data/spells.json ; reliés à <see cref="BlessingSpells"/> au chargement des données).
    /// </summary>
    public List<string> Blessings { get; init; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<SpellType> BlessingSpells { get; internal set; } = System.Array.Empty<SpellType>();

    /// <summary>
    /// Les sorts néfastes que lancent les rayons d'une créature (le Tyran œil), un au hasard à chaque tir (leurs
    /// identifiants dans data/spells.json ; reliés à <see cref="RaySpells"/> au chargement des données).
    /// </summary>
    public List<string> Rays { get; init; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<SpellType> RaySpells { get; internal set; } = System.Array.Empty<SpellType>();

    public double AverageDamage => (MinDamage + MaxDamage) / 2.0;
}

/// <summary>Toutes les créatures du jeu, lues depuis le fichier de données.</summary>
public sealed class CreatureCatalog
{
    private readonly Dictionary<string, CreatureType> _byId;
    private readonly List<CreatureType> _all;

    public CreatureCatalog(IEnumerable<CreatureType> creatures)
    {
        _all = creatures.ToList();
        _byId = _all.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>Toutes les entrées du fichier, y compris les doublons (vérifiés par <see cref="DataValidation"/>).</summary>
    public IReadOnlyCollection<CreatureType> All => _all;

    public CreatureType Get(string id) =>
        _byId.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException($"Créature inconnue : {id}");

    public static CreatureCatalog FromJson(string json) =>
        new(GameData.Parse<CatalogFile>(json, "créatures").Creatures);

    private sealed record CatalogFile(List<CreatureType> Creatures);
}
