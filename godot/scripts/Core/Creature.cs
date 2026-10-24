using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoMM.Core;

/// <summary>Un type de créature (Piquier, Loup…). Les valeurs viennent de data/creatures.json.</summary>
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

    /// <summary>Créatures produites chaque semaine par leur habitation (étape 2 : villes).</summary>
    public int Growth { get; init; }

    public Dictionary<ResourceType, int> Cost { get; init; } = new();

    public double AverageDamage => (MinDamage + MaxDamage) / 2.0;
}

/// <summary>Toutes les créatures du jeu, lues depuis le fichier de données.</summary>
public sealed class CreatureCatalog
{
    private readonly Dictionary<string, CreatureType> _byId;

    public CreatureCatalog(IEnumerable<CreatureType> creatures) =>
        _byId = creatures.ToDictionary(c => c.Id);

    public IReadOnlyCollection<CreatureType> All => _byId.Values;

    public CreatureType Get(string id) =>
        _byId.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException($"Créature inconnue : {id}");

    public static CreatureCatalog FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };
        var file = JsonSerializer.Deserialize<CatalogFile>(json, options)
                   ?? throw new FormatException("Fichier de créatures vide.");
        return new CreatureCatalog(file.Creatures);
    }

    private sealed record CatalogFile(List<CreatureType> Creatures);
}
