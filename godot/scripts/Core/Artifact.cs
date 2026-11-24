using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Un artefact : un objet que porte un héros et qui augmente ses caractéristiques. Les valeurs viennent de
/// data/artifacts.json. La liste (46, en trois rangs) a été choisie avec l'utilisateur le 2026-11-21.
/// </summary>
public sealed record ArtifactType
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public ArtifactRank Rank { get; init; } = ArtifactRank.Minor;
    public int Attack { get; init; }
    public int Defense { get; init; }
    public int Power { get; init; }
    public int Knowledge { get; init; }
    public int Charisma { get; init; }

    /// <summary>Où il se porte (un anneau va à l'une des deux mains ; sans place précise : divers).</summary>
    public ArtifactSlot Slot { get; init; } = ArtifactSlot.Misc;

    public int Morale { get; init; }
    public int Luck { get; init; }

    /// <summary>Cases de vue en plus sur la carte.</summary>
    public int Vision { get; init; }

    /// <summary>Mouvement en plus sur terre, en pourcentage.</summary>
    public int LandMovement { get; init; }

    /// <summary>Mouvement en plus en mer, en pourcentage.</summary>
    public int SeaMovement { get; init; }

    /// <summary>Le surcoût des terrains difficiles disparaît (sable, neige, marais…).</summary>
    public bool NoTerrainPenalty { get; init; }

    /// <summary>Points de sort maximum en plus.</summary>
    public int SpellPoints { get; init; }

    /// <summary>Une école de magie (« feu », « eau », « terre », « air ») dont les sorts sont plus forts…</summary>
    public string? School { get; init; }

    /// <summary>… de ce pourcentage.</summary>
    public int SchoolBonus { get; init; }

    /// <summary>Ce qu'il rapporte chaque jour.</summary>
    public Dictionary<ResourceType, int> Income { get; init; } = new();

    /// <summary>Chaque jour, autant de la ressource principale de la faction du héros.</summary>
    public int FactionResource { get; init; }

    /// <summary>Chaque jour, et dès qu'il le prend, le joueur voit les alentours de toutes les tours d'observation.</summary>
    public bool RevealsTowers { get; init; }

    /// <summary>Le joueur voit le nombre exact des troupes adverses (au lieu de « une bande de… ») et leur composition.</summary>
    public bool ExactArmies { get; init; }

    /// <summary>Nécromancie en plus, en points de pourcentage (sans effet pour un héros qui n'a pas ce talent).</summary>
    public int Necromancy { get; init; }

    /// <summary>Un pouvoir unique (il change une règle), décrit ici ; pas encore en jeu.</summary>
    public string? PowerText { get; init; }
}

/// <summary>Le rang d'un artefact : mineur (courant), majeur (gardé), relique (rare).</summary>
public enum ArtifactRank { Minor, Major, Relic }

/// <summary>Les places d'un artefact sur le héros (décidées avec l'utilisateur, 2026-11-09, proches de Heroes 3).</summary>
public enum ArtifactSlot { Head, Neck, Shoulders, Torso, Weapon, Shield, Ring, Belt, Feet, Misc }

/// <summary>
/// L'équipement du héros : 11 emplacements (deux pour les anneaux) et un coffre de 16 places pour ce qu'il porte sans
/// le mettre. Pour l'instant, chaque artefact va à la première place libre de son genre, sinon au coffre (on ne les
/// déplace pas encore à la main).
/// </summary>
public static class Equipment
{
    /// <summary>Les 11 emplacements, dans l'ordre de l'écran du héros : six à sa gauche, puis cinq à sa droite.</summary>
    public static readonly ArtifactSlot[] Slots =
    {
        ArtifactSlot.Head, ArtifactSlot.Shoulders, ArtifactSlot.Weapon, ArtifactSlot.Ring, ArtifactSlot.Belt, ArtifactSlot.Feet,
        ArtifactSlot.Neck, ArtifactSlot.Torso, ArtifactSlot.Shield, ArtifactSlot.Ring, ArtifactSlot.Misc,
    };

    public const int ChestSize = 16;

    /// <summary>Ce que porte chaque emplacement (dans l'ordre de <see cref="Slots"/>), et le reste dans le coffre.</summary>
    public static (ArtifactType?[] Worn, List<ArtifactType> Chest) Arrange(IEnumerable<ArtifactType> artifacts)
    {
        var worn = new ArtifactType?[Slots.Length];
        var chest = new List<ArtifactType>();
        foreach (var artifact in artifacts)
        {
            int free = Enumerable.Range(0, Slots.Length).FirstOrDefault(i => Slots[i] == artifact.Slot && worn[i] == null, -1);
            if (free >= 0)
                worn[free] = artifact;
            else
                chest.Add(artifact);
        }
        return (worn, chest);
    }
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
