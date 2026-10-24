using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Résultat d'un combat : vainqueur et pertes de chaque camp.</summary>
public sealed class BattleReport
{
    public required bool AttackerWon { get; init; }
    public required List<CreatureStack> AttackerLosses { get; init; }
    public required List<CreatureStack> DefenderLosses { get; init; }
    public int Rounds { get; init; }
}

/// <summary>
/// Combat résolu automatiquement (le combat tactique viendra plus tard).
/// Simulation simplifiée tour par tour, sans hasard : même armées = même résultat.
/// </summary>
public static class Battle
{
    /// <summary>Bonus ou malus de dégâts selon l'attaque et la défense (règle de Heroes 3).</summary>
    public static double DamageModifier(int attack, int defense) =>
        attack >= defense
            ? Math.Min(1 + 0.05 * (attack - defense), 4.0)
            : Math.Max(1 - 0.025 * (defense - attack), 0.3);

    /// <summary>
    /// Fait combattre les deux armées et les modifie (créatures tuées retirées).
    /// À chaque round, les troupes agissent de la plus rapide à la plus lente. Chacune attaque
    /// la troupe ennemie la plus dangereuse, qui riposte une fois par round.
    /// </summary>
    public static BattleReport Resolve(Army attacker, Army defender)
    {
        var fighters = attacker.Stacks.Where(s => s.Count > 0).Select((s, i) => new Fighter(s, 0, i))
            .Concat(defender.Stacks.Where(s => s.Count > 0).Select((s, i) => new Fighter(s, 1, i)))
            .ToList();
        var startCounts = fighters.ToDictionary(f => f, f => f.Stack.Count);

        int rounds = 0;
        while (Alive(fighters, 0) && Alive(fighters, 1) && rounds < 10_000)
        {
            rounds++;
            foreach (var f in fighters)
                f.HasRetaliated = false;

            var order = fighters.OrderByDescending(f => f.Stack.Type.Speed).ThenBy(f => f.Side).ThenBy(f => f.Slot).ToList();
            foreach (var f in order)
            {
                if (!f.IsAlive)
                    continue;
                var target = fighters.Where(e => e.Side != f.Side && e.IsAlive)
                    .OrderByDescending(e => e.Stack.Count * e.Stack.Type.AverageDamage)
                    .FirstOrDefault();
                if (target == null)
                    break;

                target.TakeDamage(Damage(f, target));
                if (target.IsAlive && !target.HasRetaliated)
                {
                    f.TakeDamage(Damage(target, f));
                    target.HasRetaliated = true;
                }
            }
        }

        bool attackerWon = Alive(fighters, 0) && !Alive(fighters, 1);
        List<CreatureStack> Losses(int side) => fighters
            .Where(f => f.Side == side && startCounts[f] > f.Stack.Count)
            .Select(f => new CreatureStack(f.Stack.Type, startCounts[f] - f.Stack.Count))
            .ToList();
        var report = new BattleReport
        {
            AttackerWon = attackerWon,
            AttackerLosses = Losses(0),
            DefenderLosses = Losses(1),
            Rounds = rounds,
        };
        attacker.RemoveEmpty();
        defender.RemoveEmpty();
        return report;
    }

    private static bool Alive(List<Fighter> fighters, int side) => fighters.Any(f => f.Side == side && f.IsAlive);

    private static int Damage(Fighter from, Fighter to)
    {
        var a = from.Stack.Type;
        double damage = from.Stack.Count * a.AverageDamage * DamageModifier(a.Attack, to.Stack.Type.Defense);
        return Math.Max(1, (int)Math.Round(damage));
    }

    /// <summary>Une troupe pendant le combat, avec les points de vie de sa créature de tête.</summary>
    private sealed class Fighter
    {
        public Fighter(CreatureStack stack, int side, int slot)
        {
            Stack = stack;
            Side = side;
            Slot = slot;
            TopHitPoints = stack.Type.HitPoints;
        }

        public CreatureStack Stack { get; }
        public int Side { get; }
        public int Slot { get; }
        public int TopHitPoints { get; private set; }
        public bool HasRetaliated { get; set; }
        public bool IsAlive => Stack.Count > 0;

        public void TakeDamage(int damage)
        {
            int hp = Stack.Type.HitPoints;
            int total = (Stack.Count - 1) * hp + TopHitPoints - damage;
            if (total <= 0)
            {
                Stack.Count = 0;
                TopHitPoints = 0;
                return;
            }
            Stack.Count = (total + hp - 1) / hp;
            TopHitPoints = total - (Stack.Count - 1) * hp;
        }
    }
}

/// <summary>
/// Un combat a eu lieu. <paramref name="EnemyCount"/> est le nombre de monstres au début du combat
/// (le monstre lui-même a pu être modifié ou retiré depuis).
/// </summary>
public sealed record BattleFought(Hero Hero, MonsterGuard Guard, int EnemyCount, BattleReport Report) : GameEvent;
