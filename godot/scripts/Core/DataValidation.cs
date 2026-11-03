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
            foreach (var required in b.Requires.Where(r => !buildingById.ContainsKey(r)))
                errors.Add($"{b.Id} : prérequis inconnu « {required} »");
            if (b.Dwelling is { } dwelling && !creatureIds.Contains(dwelling))
                errors.Add($"{b.Id} : habitation d'une créature inconnue « {dwelling} »");
            if (b.DailyGold < 0)
                errors.Add($"{b.Id} : or quotidien négatif");
            if (ReachesItself(b, buildingById))
                errors.Add($"{b.Id} : prérequis circulaires");
        }
    }

    /// <summary>Un bâtiment est-il un prérequis de lui-même, directement ou non ?</summary>
    private static bool ReachesItself(BuildingType start, IReadOnlyDictionary<string, BuildingType> buildingById)
    {
        var toVisit = new Stack<string>(start.Requires);
        var seen = new HashSet<string>();
        while (toVisit.Count > 0)
        {
            var id = toVisit.Pop();
            if (id == start.Id)
                return true;
            if (!seen.Add(id) || !buildingById.TryGetValue(id, out var next))
                continue;
            foreach (var required in next.Requires)
                toVisit.Push(required);
        }
        return false;
    }
}
