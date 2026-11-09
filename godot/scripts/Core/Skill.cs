using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Les trois arbres de compétences (étape 9).</summary>
public enum SkillTree { Aventure, Commandement, Magie }

/// <summary>
/// Une compétence de héros (étape 9, data/skills.json) : trois rangs (base, avancé, expert). <see cref="Values"/> donne
/// son effet à chaque rang (un pourcentage, des cases, de l'or…), <see cref="Values2"/> un second effet s'il y en a un
/// (la force des sorts d'une école, la vitesse d'Inspiration après le premier round). Une école de magie a sa
/// <see cref="School"/>.
/// </summary>
public sealed record SkillType
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public SkillTree Tree { get; init; }

    /// <summary>La rangée dans son arbre (1 à 3) : voir <see cref="Skills.PointsToOpenRow"/>.</summary>
    public int Row { get; init; } = 1;

    public List<int> Values { get; init; } = new();
    public List<int> Values2 { get; init; } = new();
    public string? School { get; init; }

    /// <summary>L'effet au rang donné (1 à 3), 0 au rang 0.</summary>
    public int ValueAt(int rank) => rank <= 0 || Values.Count == 0 ? 0 : Values[Math.Min(rank, Values.Count) - 1];

    public int Value2At(int rank) => rank <= 0 || Values2.Count == 0 ? 0 : Values2[Math.Min(rank, Values2.Count) - 1];
}

/// <summary>Les règles des compétences.</summary>
public static class Skills
{
    /// <summary>Trois rangs : base, avancé, expert.</summary>
    public const int MaxRank = 3;

    /// <summary>Six compétences au plus par héros.</summary>
    public const int MaxSkills = 6;

    /// <summary>Les points dépensés dans un arbre pour ouvrir chaque rangée (1, 2, 3).</summary>
    public static int PointsToOpenRow(int row) => row switch { <= 1 => 0, 2 => 3, _ => 6 };

    public const string Logistics = "logistique", Scouting = "eclaireur", Pathfinding = "orientation", Estates = "intendance",
        Leadership = "commandement", Discipline = "discipline", Recruiter = "recruteur", Inspiration = "inspiration",
        Mysticism = "mysticisme";
}

/// <summary>Pourquoi un héros ne peut pas apprendre une compétence (ou <see cref="Ok"/>).</summary>
public enum LearnSkillResult { Ok, NoPoint, MaxRank, TooManySkills, RowLocked }

/// <summary>Toutes les compétences du jeu.</summary>
public sealed class SkillCatalog
{
    private readonly Dictionary<string, SkillType> _byId;
    private readonly List<SkillType> _all;

    public SkillCatalog(IEnumerable<SkillType> skills)
    {
        _all = skills.ToList();
        _byId = _all.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
    }

    public static readonly SkillCatalog Empty = new(Array.Empty<SkillType>());

    public IReadOnlyList<SkillType> All => _all;

    public SkillType Get(string id) =>
        _byId.TryGetValue(id, out var skill) ? skill : throw new KeyNotFoundException($"compétence inconnue : {id}");

    public SkillType? Find(string id) => _byId.GetValueOrDefault(id);

    public IEnumerable<SkillType> InTree(SkillTree tree) => _all.Where(s => s.Tree == tree).OrderBy(s => s.Row);

    private sealed record CatalogFile(List<SkillType> Skills);

    public static SkillCatalog FromJson(string json) => new(GameData.Parse<CatalogFile>(json, "compétences").Skills);
}

/// <summary>Le héros a appris une compétence, ou un rang de plus (<paramref name="Rank"/> : 1 à 3).</summary>
public sealed record SkillLearned(Hero Hero, SkillType Skill, int Rank) : GameEvent;
