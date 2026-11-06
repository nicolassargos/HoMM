using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// L'IA des troupes au combat tactique (étape 6), simple et sans hasard : un tireur tire sur l'ennemi le plus
/// dangereux ; une autre troupe attaque l'ennemi le plus dangereux qu'elle peut atteindre ce tour-ci, sinon
/// s'approche de l'ennemi le plus proche. Elle joue les monstres, et les deux camps en combat rapide. Un camp qui a
/// un héros lance d'abord le sort qui vaut le plus (étape 9, <see cref="ChooseSpell"/>).
/// </summary>
public static class BattleAI
{
    /// <summary>Joue tout le combat (combat rapide).</summary>
    public static void PlayOut(TacticalBattle battle)
    {
        for (int turns = 0; !battle.IsOver && turns < 10_000; turns++)
            PlayTurn(battle);
    }

    /// <summary>
    /// Le danger d'une troupe ennemie : les dégâts qu'elle peut faire, un peu plus pour un tireur (qui frappe de loin
    /// sans riposte).
    /// </summary>
    public static double Threat(BattleUnit unit) =>
        unit.Count * unit.Type.AverageDamage * (unit.CanShoot ? 1.5 : 1.0);

    /// <summary>
    /// Le sort que le héros du camp actif a intérêt à lancer maintenant, et sa cible : celui qui vaut le plus, en or
    /// (<see cref="SpellValue"/>), s'il vaut quelque chose ; null s'il ne peut ou ne doit rien lancer.
    /// </summary>
    public static (SpellType Spell, Hex Target)? ChooseSpell(TacticalBattle battle)
    {
        if (battle.CasterSide is not { } side || battle.HasCast(side))
            return null;
        (SpellType Spell, Hex Target)? best = null;
        double bestValue = 0;
        foreach (var spell in battle.HeroOf(side)!.Spells.Where(battle.CanCast))
        foreach (var target in battle.SpellTargets(spell))
        {
            double value = SpellValue(battle, spell, target, side);
            if (value > bestValue)
                (best, bestValue) = ((spell, target), value);
        }
        return best;
    }

    /// <summary>
    /// Ce que vaut un sort, en or (comme l'expérience) : les points de vie ôtés aux ennemis (moins ceux ôtés aux amis)
    /// ou rendus, ou une part de la valeur de la troupe aidée ou gênée pour un sort qui dure. Une estimation simple.
    /// </summary>
    public static double SpellValue(TacticalBattle battle, SpellType spell, Hex target, BattleSide side)
    {
        int strength = battle.SpellStrength(spell, side);
        double value = 0;
        foreach (var unit in battle.AffectedBy(spell, target))
        {
            double gold = unit.Type.Cost.GetValueOrDefault(ResourceType.Gold);
            double troop = unit.Count * gold;
            double perHitPoint = gold / unit.Type.HitPoints;
            int sign = unit.Side == side ? -1 : 1;
            var type = unit.Type;
            double average = (type.MinDamage + type.MaxDamage) / 2.0;
            value += spell.Effect switch
            {
                SpellEffect.Damage => sign * Math.Min(strength, unit.TotalHitPoints) * perHitPoint,
                SpellEffect.Cure => Math.Min(strength, type.HitPoints - unit.TopHitPoints) * perHitPoint
                                    + (unit.Spells.Any(s => s.Spell.Harmful) ? 0.2 * troop : 0),
                SpellEffect.Resurrection => strength * perHitPoint,
                _ when unit.IsUnder(spell.Effect) => 0,
                SpellEffect.Bless => average > 0 ? troop * (type.MaxDamage - average) / average : 0,
                SpellEffect.Curse => average > 0 ? troop * (average - type.MinDamage) / average : 0,
                SpellEffect.StoneSkin => troop * 0.03 * spell.Amount,
                SpellEffect.Weakness => troop * 0.04 * Math.Min(spell.Amount, unit.Attack),
                SpellEffect.Haste => troop * 0.15,
                SpellEffect.Slow => troop * 0.25,
                _ => 0,
            };
        }
        return value;
    }

    /// <summary>Fait jouer la troupe active (son héros lance d'abord un sort, s'il en vaut la peine).</summary>
    public static void PlayTurn(TacticalBattle battle)
    {
        if (ChooseSpell(battle) is { } cast)
            battle.Cast(cast.Spell, cast.Target);
        if (battle.Active is not { } unit)
            return;
        var enemies = battle.Units.Where(e => TacticalBattle.AreEnemies(unit, e))
            .OrderByDescending(Threat).ThenBy(e => e.Id).ToList();
        if (enemies.Count == 0)
            return;

        // Tirer, si possible
        if (unit.CanShoot && !battle.IsEngaged(unit))
        {
            battle.Shoot(enemies[0]);
            return;
        }

        // Attaquer ce tour-ci : l'ennemi le plus dangereux qu'on peut atteindre, en bougeant le moins possible
        // (une troupe qui charge, au contraire, vient de le plus loin possible)
        bool charges = unit.Has(Ability.Charge);
        foreach (var enemy in enemies)
        {
            var positions = battle.AttackPositions(unit, enemy).ToList();
            var from = (charges ? positions.OrderByDescending(h => h.DistanceTo(unit.Position)) : positions.OrderBy(h => h.DistanceTo(unit.Position)))
                .ThenBy(h => h.Col).ThenBy(h => h.Row)
                .Cast<Hex?>().FirstOrDefault();
            if (from is { } hex && battle.Attack(enemy, hex))
                return;
        }

        // Sinon, s'approcher de l'ennemi le plus proche (le plus dangereux s'il y en a plusieurs)
        var target = enemies.OrderBy(e => TacticalBattle.Distance(unit, e)).ThenByDescending(Threat).ThenBy(e => e.Id).First();
        var closer = battle.Reachable(unit).Keys
            .OrderBy(h => unit.HexesAt(h).Min(x => target.Hexes.Min(x.DistanceTo))).ThenBy(h => h.DistanceTo(unit.Position))
            .ThenBy(h => h.Col).ThenBy(h => h.Row)
            .First();
        if (closer == unit.Position || !battle.Move(closer))
            battle.Defend();
    }
}
