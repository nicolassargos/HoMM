using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Le moral d'une troupe, et d'où il vient (décidé avec l'utilisateur, 2026-11-09). Chaque part est un nombre de points ;
/// <see cref="Total"/> les ajoute, entre −3 et +3.
/// </summary>
/// <param name="Aura">Le charisme du héros (pour toute l'armée) : +1, +2, +3 à 20, 30, 40 de charisme.</param>
/// <param name="Factions">Le mélange des factions de l'armée : une seule +1, deux 0, trois −1, quatre ou plus −2.</param>
/// <param name="Overload">La troupe dépasse la limite de commandement du héros : −1 (jusqu'à 1,5 fois), −2 (2 fois), −3.</param>
/// <param name="Skill">La compétence Moral.</param>
/// <param name="Site">Un lieu de la carte visité (temple), jusqu'au prochain combat.</param>
public readonly record struct MoraleParts(int Aura, int Factions, int Overload, int Skill, int Site)
{
    public int Total => Math.Clamp(Aura + Factions + Overload + Skill + Site, Morale.Min, Morale.Max);
}

/// <summary>La chance d'une troupe, et d'où elle vient (entre −3 et +3).</summary>
/// <param name="Skill">La compétence Chance.</param>
/// <param name="Site">Un lieu de la carte visité (cercle des fées), jusqu'au prochain combat.</param>
public readonly record struct LuckParts(int Skill, int Site)
{
    public int Total => Math.Clamp(Skill + Site, Morale.Min, Morale.Max);
}

/// <summary>
/// Le moral et la chance (étape 9), proches de Heroes 3. <b>Bon moral</b> : après avoir agi, la troupe rejoue parfois
/// aussitôt (une fois par round). <b>Mauvais moral</b> : au début de son tour, elle se fige parfois et passe son tour.
/// <b>Bonne chance</b> : un coup fait parfois deux fois plus de dégâts ; <b>malchance</b> : parfois moitié moins.
/// Seules les troupes d'un héros en ont (les monstres, les garnisons seules et les révoltés restent à 0).
/// </summary>
public static class Morale
{
    public const int Min = -3, Max = 3;

    /// <summary>Par point : 5 % de rejouer avec un bon moral, 10 % de se figer avec un mauvais.</summary>
    public const double GoodChancePerPoint = 0.05, BadChancePerPoint = 0.10;

    /// <summary>Par point de chance (bonne ou mauvaise) : 5 % qu'un coup double ou soit divisé par deux.</summary>
    public const double LuckChancePerPoint = 0.05;

    /// <summary>Le charisme qui donne au héros +1, +2, +3 de moral pour toute son armée.</summary>
    public static readonly int[] AuraCharisma = { 20, 30, 40 };

    public static int Aura(int charisma) => AuraCharisma.Count(c => charisma >= c);

    /// <summary>Le mélange des factions dans l'armée : une seule +1, deux 0, trois −1, quatre ou plus −2.</summary>
    public static int Factions(IEnumerable<CreatureType> army) => army.Select(t => t.Faction).Distinct().Count() switch
    {
        0 => 0,
        1 => 1,
        2 => 0,
        3 => -1,
        _ => -2,
    };

    /// <summary>
    /// Une troupe au-delà de la limite de commandement : −1 jusqu'à 1,5 fois la limite, −2 jusqu'à 2 fois, −3 au-delà.
    /// </summary>
    public static int Overload(CreatureType type, int count, int limit)
    {
        int cost = Command.Cost(type, count);
        if (cost <= limit)
            return 0;
        return cost * 2 <= limit * 3 ? -1 : cost <= limit * 2 ? -2 : -3;
    }

    /// <summary>
    /// Le moral d'une troupe de ce héros, dans une armée faite de <paramref name="army"/> (les créatures de toutes ses
    /// troupes). Sans héros : rien.
    /// </summary>
    public static MoraleParts Of(Hero? hero, CreatureType type, int count, IEnumerable<CreatureType> army) =>
        hero == null
            ? default
            : new MoraleParts(Aura(hero.Charisma), Factions(army), Overload(type, count, hero.CommandLimit),
                hero.SkillValue(Skills.Morale), hero.SiteMorale);

    /// <summary>Le moral d'une troupe de l'armée du héros (pour l'écran du héros).</summary>
    public static MoraleParts Of(Hero hero, CreatureStack stack) =>
        Of(hero, stack.Type, stack.Count, hero.Army.Stacks.Where(s => s.Count > 0).Select(s => s.Type));

    /// <summary>Le moral de toute l'armée du héros, sans compter le dépassement de la limite d'une troupe.</summary>
    public static MoraleParts OfArmy(Hero hero) =>
        new(Aura(hero.Charisma), Factions(hero.Army.Stacks.Where(s => s.Count > 0).Select(s => s.Type)), 0,
            hero.SkillValue(Skills.Morale), hero.SiteMorale);

    /// <summary>La chance des troupes de ce héros (la même pour toutes). Sans héros : rien.</summary>
    public static LuckParts LuckOf(Hero? hero) =>
        hero == null ? default : new LuckParts(hero.SkillValue(Skills.Luck), hero.SiteLuck);
}

/// <summary>Bon moral : la troupe rejoue aussitôt.</summary>
public sealed record UnitCheered(BattleUnit Unit) : BattleAction;

/// <summary>Mauvais moral : la troupe se fige et passe son tour.</summary>
public sealed record UnitFroze(BattleUnit Unit) : BattleAction;
