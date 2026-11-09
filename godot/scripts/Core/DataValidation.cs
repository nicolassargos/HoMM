using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Vérifie les données (data/) au chargement : une faute est signalée au démarrage,
/// avec toutes les erreurs trouvées, et pas au milieu d'une partie.
/// </summary>
public static class DataValidation
{
    public static List<string> Check(IReadOnlyCollection<CreatureType> creatures, IReadOnlyCollection<BuildingType> buildings)
    {
        var errors = new List<string>();
        CheckCreatures(creatures, errors);
        CheckBuildings(buildings, creatures, errors);
        return errors;
    }

    /// <summary>Les sorts (data/spells.json) : identifiants uniques, niveau de 1 à 4, coût positif, force cohérente.</summary>
    /// <summary>Les compétences (rangs, rangées, écoles connues) et la compétence de départ de chaque classe.</summary>
    public static void CheckSkills(IReadOnlyCollection<SkillType> skills, IReadOnlyCollection<HeroClass> classes,
        IReadOnlyCollection<SpellType> spells, List<string> errors)
    {
        foreach (var group in skills.GroupBy(s => s.Id).Where(g => g.Count() > 1))
            errors.Add($"compétence en double : {group.Key}");
        foreach (var s in skills)
        {
            if (s.Values.Count != Skills.MaxRank || s.Values2.Count is not (0 or Skills.MaxRank))
                errors.Add($"{s.Id} : il faut une valeur par rang ({Skills.MaxRank})");
            if (s.Row is < 1 or > 3)
                errors.Add($"{s.Id} : rangée 1 à 3");
            if (s.School is { } school && spells.All(sp => sp.School != school))
                errors.Add($"{s.Id} : aucun sort de l'école {school}");
        }
        foreach (var c in classes.Where(c => c.Skill != null && skills.Count > 0 && skills.All(s => s.Id != c.Skill)))
            errors.Add($"{c.Id} : compétence de départ inconnue ({c.Skill})");
    }

    public static void CheckSpells(IReadOnlyCollection<SpellType> spells, List<string> errors)
    {
        foreach (var group in spells.GroupBy(s => s.Id).Where(g => g.Count() > 1))
            errors.Add($"sort en double : {group.Key}");
        foreach (var s in spells)
        {
            if (string.IsNullOrWhiteSpace(s.Id))
                errors.Add($"sort sans identifiant ({s.Name})");
            if (s.Level is < 1 or > 4)
                errors.Add($"{s.Id} : niveau {s.Level} hors de 1 à 4");
            if (s.Cost < 1)
                errors.Add($"{s.Id} : coût invalide ({s.Cost})");
            if (s.Effect is SpellEffect.Damage or SpellEffect.Cure or SpellEffect.Resurrection && s.Base + s.PerPower < 1)
                errors.Add($"{s.Id} : force nulle");
            if (s.Target is SpellTarget.Area or SpellTarget.Chain && s.Effect != SpellEffect.Damage)
                errors.Add($"{s.Id} : seuls les sorts de dégâts visent une zone ou font une chaîne");
            if (s.Effect == SpellEffect.Teleport && s.Target != SpellTarget.Ally)
                errors.Add($"{s.Id} : on ne téléporte que ses troupes");
        }
    }

    /// <summary>Les factions prévues, sans créatures pour l'instant : leurs héros attendent (data/heroes.json).</summary>
    public static readonly IReadOnlySet<string> PlannedFactions = new HashSet<string> { "tour", "sombre" };

    /// <summary>Les héros (noms uniques, faction connue ou prévue) et les classes (une faction connue, des sorts connus, des caractéristiques positives).</summary>
    public static void CheckHeroes(IReadOnlyCollection<HeroType> heroes, IReadOnlyCollection<HeroClass> classes,
        IReadOnlyCollection<CreatureType> creatures, IReadOnlyCollection<SpellType> spells, List<string> errors)
    {
        foreach (var group in heroes.GroupBy(h => h.Name).Where(g => g.Count() > 1))
            errors.Add($"héros en double : {group.Key}");
        foreach (var h in heroes.Where(h => string.IsNullOrWhiteSpace(h.Name)))
            errors.Add($"héros sans nom ({h.Id})");
        var known = creatures.Select(c => c.Faction).ToHashSet();
        foreach (var h in heroes.Where(h => h.Faction != null && !known.Contains(h.Faction) && !PlannedFactions.Contains(h.Faction)))
            errors.Add($"{h.Id} : faction inconnue ({h.Faction})");
        var factions = creatures.Select(c => c.Faction).ToHashSet();
        foreach (var group in classes.GroupBy(c => c.Id).Where(g => g.Count() > 1))
            errors.Add($"classe en double : {group.Key}");
        foreach (var c in classes)
        {
            if (!factions.Contains(c.Faction))
                errors.Add($"{c.Id} : faction inconnue ({c.Faction})");
            if (c.Attack < 0 || c.Defense < 0 || c.Power < 0 || c.Knowledge < 0 || c.Charisma < 1)
                errors.Add($"{c.Id} : caractéristiques invalides");
            foreach (var spell in c.Spells.Where(id => spells.All(s => s.Id != id)))
                errors.Add($"{c.Id} : sort inconnu ({spell})");
            foreach (var stack in c.Army.Where(s => creatures.All(cr => cr.Id != s.Creature) || s.Count < 1))
                errors.Add($"{c.Id} : armée de départ invalide ({stack.Creature})");
            if (c.MainResource is { } main && !ResourceInfo.IsRare(main) || c.SecondaryResource is { } second && !ResourceInfo.IsRare(second))
                errors.Add($"{c.Id} : ses ressources principale et secondaire doivent être rares");
            if (c.MainResource != null && c.MainResource == c.SecondaryResource)
                errors.Add($"{c.Id} : la ressource principale et la secondaire doivent être différentes");
            if (c.Talent is { } talent && talent != Talents.Necromancy)
                errors.Add($"{c.Id} : talent inconnu ({talent})");
        }
    }

    private static void CheckCreatures(IReadOnlyCollection<CreatureType> creatures, List<string> errors)
    {
        foreach (var group in creatures.GroupBy(c => c.Id).Where(g => g.Count() > 1))
            errors.Add($"créature en double : {group.Key}");

        foreach (var c in creatures)
        {
            if (string.IsNullOrWhiteSpace(c.Id))
                errors.Add($"créature sans identifiant ({c.Name})");
            if (string.IsNullOrWhiteSpace(c.Faction))
                errors.Add($"{c.Id} : faction manquante");
            if (c.Tier is < 1 or > 7)
                errors.Add($"{c.Id} : palier {c.Tier} hors de 1 à 7");
            if (c.Attack < 0 || c.Defense < 0)
                errors.Add($"{c.Id} : attaque ou défense négative");
            if (c.MinDamage < 0 || c.MaxDamage < c.MinDamage)
                errors.Add($"{c.Id} : dégâts invalides ({c.MinDamage}-{c.MaxDamage})");
            if (c.HitPoints < 1)
                errors.Add($"{c.Id} : points de vie invalides ({c.HitPoints})");
            if (c.Speed < 1)
                errors.Add($"{c.Id} : vitesse invalide ({c.Speed})");
            foreach (var ability in c.Abilities.Where(a => !Ability.Known.Contains(a)))
                errors.Add($"{c.Id} : capacité inconnue ({ability})");
            if (c.Growth < 0)
                errors.Add($"{c.Id} : croissance négative ({c.Growth})");
            if (c.CommandCost < 1)
                errors.Add($"{c.Id} : coût de commandement invalide ({c.CommandCost})");
            if (c.Cost.Values.Any(v => v < 0))
                errors.Add($"{c.Id} : coût négatif");
        }
    }

    private static void CheckBuildings(IReadOnlyCollection<BuildingType> buildings, IReadOnlyCollection<CreatureType> creatures, List<string> errors)
    {
        foreach (var group in buildings.GroupBy(b => b.Id).Where(g => g.Count() > 1))
            errors.Add($"bâtiment en double : {group.Key}");

        var buildingById = buildings.GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());
        var creatureIds = creatures.Select(c => c.Id).ToHashSet();

        foreach (var b in buildings)
        {
            if (string.IsNullOrWhiteSpace(b.Id))
                errors.Add($"bâtiment sans identifiant ({b.Name})");
            foreach (var required in b.RequirementChoices.SelectMany(c => c).Where(r => !buildingById.ContainsKey(r)))
                errors.Add($"{b.Id} : prérequis inconnu « {required} »");
            if (b.Dwelling is { } dwelling && !creatureIds.Contains(dwelling))
                errors.Add($"{b.Id} : habitation d'une créature inconnue « {dwelling} »");
            if (b.DailyGold < 0)
                errors.Add($"{b.Id} : or quotidien négatif");
            if (ReachesItself(b, buildingById))
                errors.Add($"{b.Id} : prérequis circulaires");
            if (b.Replaces is { } replaced && !buildingById.ContainsKey(replaced))
                errors.Add($"{b.Id} : remplace un bâtiment inconnu « {replaced} »");
            if (b.View.Count is not (0 or 4) || b.View.Skip(2).Any(v => v <= 0))
                errors.Add($"{b.Id} : place dans la ville invalide (x, y, largeur, hauteur)");
        }
    }

    /// <summary>Un bâtiment est-il un prérequis de lui-même, directement ou non ?</summary>
    private static bool ReachesItself(BuildingType start, IReadOnlyDictionary<string, BuildingType> buildingById)
    {
        var toVisit = new Stack<string>(start.RequirementChoices.SelectMany(c => c));
        var seen = new HashSet<string>();
        while (toVisit.Count > 0)
        {
            var id = toVisit.Pop();
            if (id == start.Id)
                return true;
            if (!seen.Add(id) || !buildingById.TryGetValue(id, out var next))
                continue;
            foreach (var required in next.RequirementChoices.SelectMany(c => c))
                toVisit.Push(required);
        }
        return false;
    }
}
