using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoMM.Core;

/// <summary>Les données du jeu, lues depuis les fichiers du dossier data/ (créatures, bâtiments…).</summary>
public sealed class GameData
{
    public GameData(CreatureCatalog creatures, BuildingCatalog buildings, ArtifactCatalog? artifacts = null, SpellCatalog? spells = null,
        HeroCatalog? heroes = null, ClassCatalog? classes = null, SkillCatalog? skills = null)
    {
        var errors = DataValidation.Check(creatures.All, buildings.All);
        DataValidation.CheckSpells((spells ?? SpellCatalog.Empty).All, errors);
        DataValidation.CheckHeroes((heroes ?? HeroCatalog.Empty).All, (classes ?? ClassCatalog.Empty).All, creatures.All,
            (spells ?? SpellCatalog.Empty).All, errors);
        DataValidation.CheckSkills((skills ?? SkillCatalog.Empty).All, (classes ?? ClassCatalog.Empty).All,
            (spells ?? SpellCatalog.Empty).All, errors);
        // les bénédictions d'une créature (le Djinn) : des sorts qui existent
        foreach (var creature in creatures.All)
            foreach (var id in creature.Blessings.Where(id => (spells ?? SpellCatalog.Empty).Find(id) == null))
                errors.Add($"{creature.Id} : bénédiction inconnue ({id})");
        foreach (var creature in creatures.All)
            foreach (var id in creature.Rays.Where(id => (spells ?? SpellCatalog.Empty).Find(id) == null))
                errors.Add($"{creature.Id} : rayon inconnu ({id})");
        foreach (var creature in creatures.All)
            if (creature.Abilities.Contains(Ability.Summons) != (creature.Summon != null)
                || creature.Summon is { } summoned && creatures.All.All(c => c.Id != summoned))
                errors.Add($"{creature.Id} : invocation invalide ({creature.Summon})");
        if (errors.Count > 0)
            throw new FormatException("Données du jeu invalides :" + System.Environment.NewLine + "- " + string.Join(System.Environment.NewLine + "- ", errors));
        Creatures = creatures;
        Buildings = buildings;
        Artifacts = artifacts ?? ArtifactCatalog.Empty;
        Spells = spells ?? SpellCatalog.Empty;
        Heroes = heroes ?? HeroCatalog.Empty;
        Classes = classes ?? ClassCatalog.Empty;
        Skills = skills ?? SkillCatalog.Empty;
        foreach (var creature in creatures.All.Where(c => c.Blessings.Count > 0))
            creature.BlessingSpells = creature.Blessings.Select(Spells.Get).ToList();
        foreach (var creature in creatures.All.Where(c => c.Rays.Count > 0))
            creature.RaySpells = creature.Rays.Select(Spells.Get).ToList();
        foreach (var creature in creatures.All.Where(c => c.Summon != null))
            creature.SummonType = creatures.All.First(c => c.Id == creature.Summon);
    }

    public CreatureCatalog Creatures { get; }
    public BuildingCatalog Buildings { get; }
    public ArtifactCatalog Artifacts { get; }
    public SpellCatalog Spells { get; }
    public HeroCatalog Heroes { get; }
    public ClassCatalog Classes { get; }
    public SkillCatalog Skills { get; }

    public static GameData FromJson(string creaturesJson, string buildingsJson, string? artifactsJson = null, string? spellsJson = null,
        string? heroesJson = null, string? classesJson = null, string? skillsJson = null) =>
        new(CreatureCatalog.FromJson(creaturesJson), BuildingCatalog.FromJson(buildingsJson),
            artifactsJson != null ? ArtifactCatalog.FromJson(artifactsJson) : null,
            spellsJson != null ? SpellCatalog.FromJson(spellsJson) : null,
            heroesJson != null ? HeroCatalog.FromJson(heroesJson) : null,
            classesJson != null ? ClassCatalog.FromJson(classesJson) : null,
            skillsJson != null ? SkillCatalog.FromJson(skillsJson) : null);

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

    /// <summary>
    /// Les bâtiments à construire avant celui-ci. « a|b » : l'un ou l'autre (un bâtiment commun à toutes les factions
    /// qui demande une habitation : celle du Château ou celle des morts-vivants).
    /// </summary>
    public List<string> Requires { get; init; } = new();

    /// <summary>Chaque prérequis, avec ses choix (« a|b » : a ou b).</summary>
    public IEnumerable<string[]> RequirementChoices => Requires.Select(r => r.Split('|'));

    /// <summary>Un bâtiment commun à toutes les factions (salle du village, taverne, guilde…).</summary>
    public const string AnyFaction = "*";

    /// <summary>Or rapporté chaque jour (salle du village, hôtel de ville…). Seul le plus élevé de la ville compte.</summary>
    public int DailyGold { get; init; }

    /// <summary>Identifiant de la créature produite, si c'est une habitation.</summary>
    public string? Dwelling { get; init; }

    /// <summary>La taverne permet de recruter des héros.</summary>
    public bool Tavern { get; init; }

    /// <summary>Le niveau de guilde des mages que donne ce bâtiment (0 : ce n'est pas une guilde ; étape 9).</summary>
    public int MageGuild { get; init; }

    /// <summary>Le marché : on y échange des ressources (<see cref="Core.Market"/>) ; chaque marché améliore les taux.</summary>
    public bool Market { get; init; }

    /// <summary>Le chantier naval : il construit des bateaux (seulement dans une ville au bord de l'eau, <see cref="Town.BoatSpot"/>).</summary>
    public bool Shipyard { get; init; }

    /// <summary>
    /// Le bâtiment que celui-ci remplace (l'hôtel de ville remplace la salle du village) : à l'écran, seul le plus récent
    /// est dessiné, à la même place.
    /// </summary>
    public string? Replaces { get; init; }

    /// <summary>
    /// Sa place dans la vue de la ville (étape 7) : x, y, largeur, hauteur, en pixels, dans une vue de 1280 × 390.
    /// Une donnée d'affichage, sans effet sur les règles.
    /// </summary>
    public List<int> View { get; init; } = new();
}

/// <summary>Tous les bâtiments du jeu.</summary>
public sealed class BuildingCatalog
{
    private readonly Dictionary<string, BuildingType> _byId;
    private readonly List<BuildingType> _all;

    public BuildingCatalog(IEnumerable<BuildingType> buildings)
    {
        _all = buildings.ToList();
        _byId = _all.GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>Toutes les entrées du fichier, y compris les doublons (vérifiés par <see cref="DataValidation"/>).</summary>
    public IReadOnlyCollection<BuildingType> All => _all;

    public BuildingType Get(string id) =>
        _byId.TryGetValue(id, out var b) ? b : throw new KeyNotFoundException($"Bâtiment inconnu : {id}");

    /// <summary>Les bâtiments d'une ville de cette faction : les siens, et les bâtiments communs.</summary>
    public IEnumerable<BuildingType> OfFaction(string faction) =>
        _all.Where(b => b.Faction == faction || b.Faction == BuildingType.AnyFaction).GroupBy(b => b.Id).Select(g => g.First());

    public static BuildingCatalog FromJson(string json) =>
        new(GameData.Parse<CatalogFile>(json, "bâtiments").Buildings);

    private sealed record CatalogFile(List<BuildingType> Buildings);
}
