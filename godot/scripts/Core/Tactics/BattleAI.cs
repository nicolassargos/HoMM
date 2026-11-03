using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// L'IA des troupes au combat tactique (étape 6), simple et sans hasard : un tireur tire sur l'ennemi le plus
/// dangereux ; une autre troupe attaque l'ennemi le plus dangereux qu'elle peut atteindre ce tour-ci, sinon
/// s'approche de l'ennemi le plus proche. Elle joue les monstres, et les deux camps en combat rapide.
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

    /// <summary>Fait jouer la troupe active.</summary>
    public static void PlayTurn(TacticalBattle battle)
    {
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
        var target = enemies.OrderBy(e => e.Position.DistanceTo(unit.Position)).ThenByDescending(Threat).ThenBy(e => e.Id).First();
        var closer = battle.Reachable(unit).Keys
            .OrderBy(h => h.DistanceTo(target.Position)).ThenBy(h => h.DistanceTo(unit.Position)).ThenBy(h => h.Col).ThenBy(h => h.Row)
            .First();
        if (closer == unit.Position || !battle.Move(closer))
            battle.Defend();
    }
}
