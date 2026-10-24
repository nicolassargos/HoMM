using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoMM.Core;

/// <summary>Les données du jeu, lues depuis les fichiers du dossier data/ (créatures, bâtiments…).</summary>
public sealed class GameData
{
    public GameData(CreatureCatalog creatures, BuildingCatalog buildings)
    {
        Creatures = creatures;
        Buildings = buildings;
    }

    public CreatureCatalog Creatures { get; }
    public BuildingCatalog Buildings { get; }

    public static GameData FromJson(string creaturesJson, string buildingsJson) =>
        new(CreatureCatalog.FromJson(creaturesJson), BuildingCatalog.FromJson(buildingsJson));

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static T Parse<T>(string json, string what) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new FormatException($"Fichier de {what} vide.");
}

/// <summary>Un type de bâtiment de ville. Les valeurs viennent de data/buildings.json.</summary>
public sealed record BuildingType
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Faction { get; init; } = "";
    public Dictionary<ResourceType, int> Cost { get; init; } = new();

    /// <summary>Les bâtiments à construire avant celui-ci.</summary>
    public List<string> Requires { get; init; } = new();

    /// <summary>Or rapporté chaque jour (salle du village, hôtel de ville…). Seul le plus élevé de la ville compte.</summary>
    public int DailyGold { get; init; }

    /// <summary>Identifiant de la créature produite, si c'est une habitation.</summary>
    public string? Dwelling { get; init; }

    /// <summary>La taverne permet de recruter des héros.</summary>
    public bool Tavern { get; init; }
}

/// <summary>Tous les bâtiments du jeu.</summary>
public sealed class BuildingCatalog
{
    private readonly Dictionary<string, BuildingType> _byId;

    public BuildingCatalog(IEnumerable<BuildingType> buildings) =>
        _byId = buildings.ToDictionary(b => b.Id);

    public IReadOnlyCollection<BuildingType> All => _byId.Values;

    public BuildingType Get(string id) =>
        _byId.TryGetValue(id, out var b) ? b : throw new KeyNotFoundException($"Bâtiment inconnu : {id}");

    public IEnumerable<BuildingType> OfFaction(string faction) => _byId.Values.Where(b => b.Faction == faction);

    public static BuildingCatalog FromJson(string json) =>
        new(GameData.Parse<CatalogFile>(json, "bâtiments").Buildings);

    private sealed record CatalogFile(List<BuildingType> Buildings);
}
