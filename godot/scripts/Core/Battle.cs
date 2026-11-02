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

    /// <summary>Troupes révoltées avant le combat : elles combattent dans le camp ennemi.</summary>
    public List<CreatureStack> Rebels { get; init; } = new();

    /// <summary>Survivants des troupes révoltées : ils quittent l'armée après le combat.</summary>
    public List<CreatureStack> RebelSurvivors { get; init; } = new();

    /// <summary>Expérience gagnée : la valeur en or des créatures ennemies tuées.</summary>
    public int Experience => Battle.ExperienceFor(DefenderLosses);
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
    /// <paramref name="hero"/> (celui de l'armée attaquante) ajoute son attaque et sa défense à ses troupes.
    /// <paramref name="rebels"/> sont des troupes révoltées : elles combattent avec le camp défenseur,
    /// sans le bonus du héros. Leurs survivants sont retirés de <paramref name="rebels"/>.
    /// </summary>
    public static BattleReport Resolve(Army attacker, Army defender, Hero? hero = null, Army? rebels = null)
    {
        int heroAttack = hero?.Attack ?? 0;
        int heroDefense = hero?.Defense ?? 0;
        var rebelsBefore = Copy(rebels);

        var fighters = attacker.Stacks.Where(s => s.Count > 0).Select((s, i) => new Fighter(s, 0, i, heroAttack, heroDefense))
            .Concat(defender.Stacks.Where(s => s.Count > 0).Select((s, i) => new Fighter(s, 1, i)))
            .Concat((rebels?.Stacks ?? Enumerable.Empty<CreatureStack>()).Where(s => s.Count > 0)
                .Select((s, i) => new Fighter(s, 1, i, isRebel: true)))
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
        // Les pertes des troupes révoltées ne comptent ni pour l'expérience ni dans les pertes du héros
        List<CreatureStack> Losses(int side) => fighters
            .Where(f => f.Side == side && !f.IsRebel && startCounts[f] > f.Stack.Count)
            .Select(f => new CreatureStack(f.Stack.Type, startCounts[f] - f.Stack.Count))
            .ToList();
        var report = new BattleReport
        {
            AttackerWon = attackerWon,
            AttackerLosses = Losses(0),
            DefenderLosses = Losses(1),
            Rounds = rounds,
            Rebels = rebelsBefore,
        };
        attacker.RemoveEmpty();
        defender.RemoveEmpty();
        rebels?.RemoveEmpty();
        report.RebelSurvivors.AddRange(Copy(rebels));
        return report;
    }

    /// <summary>Valeur en or des créatures tuées : c'est l'expérience gagnée par le héros.</summary>
    public static int ExperienceFor(IEnumerable<CreatureStack> losses) =>
        losses.Sum(l => l.Count * l.Type.Cost.GetValueOrDefault(ResourceType.Gold));

    private static bool Alive(List<Fighter> fighters, int side) => fighters.Any(f => f.Side == side && f.IsAlive);

    private static List<CreatureStack> Copy(Army? army) =>
        army?.Stacks.Where(s => s.Count > 0).Select(s => new CreatureStack(s.Type, s.Count)).ToList() ?? new();

    private static int Damage(Fighter from, Fighter to)
    {
        double damage = from.Stack.Count * from.Stack.Type.AverageDamage * DamageModifier(from.Attack, to.Defense);
        return Math.Max(1, (int)Math.Round(damage));
    }

    /// <summary>Une troupe pendant le combat, avec les points de vie de sa créature de tête.</summary>
    private sealed class Fighter
    {
        public Fighter(CreatureStack stack, int side, int slot, int attackBonus = 0, int defenseBonus = 0, bool isRebel = false)
        {
            Stack = stack;
            Side = side;
            Slot = slot;
            Attack = stack.Type.Attack + attackBonus;
            Defense = stack.Type.Defense + defenseBonus;
            IsRebel = isRebel;
            TopHitPoints = stack.Type.HitPoints;
        }

        public CreatureStack Stack { get; }
        public int Side { get; }
        public int Slot { get; }
        /// <summary>Attaque et défense, bonus du héros compris.</summary>
        public int Attack { get; }
        public int Defense { get; }
        public bool IsRebel { get; }
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
/// Un combat a eu lieu contre <paramref name="Defender"/> (un monstre, ou la garnison d'une ville).
/// <paramref name="DefenderArmy"/> est l'armée adverse au début du combat (elle a pu changer depuis).
/// </summary>
public sealed record BattleFought(Hero Hero, MapObject Defender, IReadOnlyList<CreatureStack> DefenderArmy, BattleReport Report) : GameEvent;
