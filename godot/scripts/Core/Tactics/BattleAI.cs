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
    public static (SpellType Spell, Hex Target, Hex? Destination)? ChooseSpell(TacticalBattle battle)
    {
        if (battle.CasterSide is not { } side || battle.HasCast(side))
            return null;
        (SpellType Spell, Hex Target, Hex? Destination)? best = null;
        double bestValue = 0;
        foreach (var spell in battle.HeroOf(side)!.Spells.Where(battle.CanCast))
        {
            if (spell.Effect == SpellEffect.Teleport)
            {
                if (Teleport(battle) is { } jump && jump.Value > bestValue)
                    (best, bestValue) = ((spell, jump.From, jump.To), jump.Value);
                continue;
            }
            foreach (var target in battle.SpellTargets(spell))
            {
                double value = SpellValue(battle, spell, target, side);
                if (value > bestValue)
                    (best, bestValue) = ((spell, target, null), value);
            }
        }
        return best;
    }

    /// <summary>
    /// La téléportation que l'IA envisage : la troupe active, si elle se bat au corps à corps et n'atteint aucun ennemi
    /// ce tour-ci, va juste à côté de l'ennemi le plus dangereux (pour le frapper aussitôt). Vaut une part de sa valeur.
    /// </summary>
    private static (Hex From, Hex To, double Value)? Teleport(TacticalBattle battle)
    {
        if (battle.Active is not { } unit || unit.CanShoot)
            return null;
        var enemies = battle.Units.Where(e => TacticalBattle.AreEnemies(unit, e)).OrderByDescending(Threat).ThenBy(e => e.Id).ToList();
        if (enemies.Any(e => battle.AttackPositions(unit, e).Any()))
            return null;
        var destinations = battle.TeleportDestinations(unit).ToList();
        foreach (var enemy in enemies)
            if (destinations.Where(h => TacticalBattle.Touches(unit, h, enemy)).OrderBy(h => h.Col).ThenBy(h => h.Row)
                    .Cast<Hex?>().FirstOrDefault() is { } to)
                return (unit.Position, to, 0.3 * unit.Count * unit.Type.Cost.GetValueOrDefault(ResourceType.Gold));
        return null;
    }

    /// <summary>
    /// Ce que vaut un sort, en or (comme l'expérience) : les points de vie ôtés aux ennemis (moins ceux ôtés aux amis)
    /// ou rendus, ou une part de la valeur de la troupe aidée ou gênée pour un sort qui dure. Une estimation simple.
    /// </summary>
    public static double SpellValue(TacticalBattle battle, SpellType spell, Hex target, BattleSide side)
    {
        double value = 0;
        bool enemyShooters = battle.Units.Any(u => u.IsAlive && u.Side != side && u.CanShoot);
        foreach (var (unit, strength) in battle.SpellHits(spell, target, side))
        {
            double gold = unit.Type.Cost.GetValueOrDefault(ResourceType.Gold);
            double troop = unit.Count * gold;
            double perHitPoint = gold / unit.Type.HitPoints;
            int sign = unit.Side == side ? -1 : 1;
            var type = unit.Type;
            // les sorts qui durent sur la troupe, utiles ou néfastes (pour la dissipation)
            int good = unit.Spells.Count(s => !s.Spell.Harmful), bad = unit.Spells.Count(s => s.Spell.Harmful);
            value += spell.Effect switch
            {
                SpellEffect.Damage => sign * Math.Min(strength, unit.TotalHitPoints) * perHitPoint,
                SpellEffect.Cure => Math.Min(strength, type.HitPoints - unit.TopHitPoints) * perHitPoint
                                    + (unit.Spells.Any(s => s.Spell.Harmful) ? 0.2 * troop : 0),
                SpellEffect.Resurrection => strength * perHitPoint,
                SpellEffect.Dispel => unit.Side == side ? 0.2 * troop * (bad - good) : 0.2 * troop * (good - bad),
                _ when unit.IsUnder(spell.Effect) => 0,
                SpellEffect.Bless or SpellEffect.Curse => troop * spell.Amount / 100.0,
                SpellEffect.StoneSkin => troop * 0.03 * spell.Amount,
                SpellEffect.Weakness => troop * 0.04 * Math.Min(spell.Amount, unit.Attack),
                SpellEffect.Bloodlust => troop * 0.04 * spell.Amount,
                SpellEffect.ArrowShield => enemyShooters ? troop * 0.25 : 0,
                SpellEffect.Blind => troop * 0.5,
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
            battle.Cast(cast.Spell, cast.Target, cast.Destination);
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
