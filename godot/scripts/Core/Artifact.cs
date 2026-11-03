using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Un artefact : un objet que porte un héros et qui augmente ses caractéristiques. Les valeurs viennent de
/// data/artifacts.json. Pour l'instant, il n'y en a qu'un : le trésor enfoui de chaque carte.
/// </summary>
public sealed record ArtifactType
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Attack { get; init; }
    public int Defense { get; init; }
    public int Power { get; init; }
    public int Knowledge { get; init; }
    public int Charisma { get; init; }
}

/// <summary>Tous les artefacts du jeu.</summary>
public sealed class ArtifactCatalog
{
    /// <summary>L'artefact enfoui sur chaque carte (le trésor de la condition « Trouver le trésor »).</summary>
    public const string TreasureId = "plastron-des-rois";

    private readonly Dictionary<string, ArtifactType> _byId;

    public ArtifactCatalog(IEnumerable<ArtifactType> artifacts) => _byId = artifacts.ToDictionary(a => a.Id);

    public static readonly ArtifactCatalog Empty = new(new ArtifactType[0]);

    public IReadOnlyCollection<ArtifactType> All => _byId.Values;

    public ArtifactType Get(string id) =>
        _byId.TryGetValue(id, out var artifact) ? artifact : throw new KeyNotFoundException($"artefact inconnu : {id}");

    /// <summary>L'artefact enfoui, ou null s'il n'est pas dans les données.</summary>
    public ArtifactType? Treasure => _byId.GetValueOrDefault(TreasureId);

    private sealed record CatalogFile(List<ArtifactType> Artifacts);

    public static ArtifactCatalog FromJson(string json) =>
        new(GameData.Parse<CatalogFile>(json, "artefacts").Artifacts);
}
