using System;

namespace HoMM.Core;

/// <summary>Le camp d'une troupe : l'attaquant (le héros) à gauche, le défenseur (monstres, garnison) à droite.</summary>
public enum BattleSide { Attacker, Defender }

/// <summary>Les capacités des créatures (liste « abilities » de data/creatures.json).</summary>
public static class Ability
{
    /// <summary>Tire de loin (archers, moines, orcs…).</summary>
    public const string Shoots = "shoots";

    /// <summary>Tire aussi bien de près que de loin : pas de dégâts réduits à longue distance (elfes, sorts…).</summary>
    public const string NoRangePenalty = "no-range-penalty";

    public static readonly string[] Known = { Shoots, NoRangePenalty };
}

/// <summary>
/// Une troupe sur le champ de bataille. Elle partage sa <see cref="CreatureStack"/> avec l'armée : les créatures
/// tuées disparaissent de l'armée au fil du combat.
/// </summary>
public sealed class BattleUnit
{
    internal BattleUnit(int id, CreatureStack stack, BattleSide side, int slot, Hex position,
        int attackBonus = 0, int defenseBonus = 0, bool isRebel = false)
    {
        Id = id;
        Stack = stack;
        Side = side;
        Slot = slot;
        Position = position;
        AttackBonus = attackBonus;
        DefenseBonus = defenseBonus;
        IsRebel = isRebel;
        TopHitPoints = stack.Type.HitPoints;
    }

    public int Id { get; }
    public CreatureStack Stack { get; }
    public CreatureType Type => Stack.Type;
    public int Count => Stack.Count;
    public BattleSide Side { get; }

    /// <summary>Place de la troupe dans son armée (départage l'ordre de jeu).</summary>
    public int Slot { get; }
    public Hex Position { get; internal set; }

    /// <summary>Une troupe révoltée : elle combat dans le camp du défenseur, sans le bonus du héros.</summary>
    public bool IsRebel { get; }

    /// <summary>Bonus du héros (attaque et défense).</summary>
    public int AttackBonus { get; }
    public int DefenseBonus { get; }

    /// <summary>Points de vie de la créature de tête (les autres sont intactes).</summary>
    public int TopHitPoints { get; private set; }

    public bool IsAlive => Stack.Count > 0;
    public bool CanShoot => Type.Abilities.Contains(Ability.Shoots);
    public bool HasRangePenalty => !Type.Abilities.Contains(Ability.NoRangePenalty);

    /// <summary>Ripostes encore possibles dans ce round (une par round).</summary>
    public int RetaliationsLeft { get; internal set; } = 1;

    /// <summary>La troupe se défend jusqu'à son prochain tour : sa défense augmente.</summary>
    public bool IsDefending { get; internal set; }

    /// <summary>La troupe a choisi d'attendre : elle jouera après les autres dans ce round.</summary>
    public bool HasWaited { get; internal set; }

    public int Attack => Type.Attack + AttackBonus;

    /// <summary>Défense, bonus du héros compris, et +20 % (arrondi au-dessus) quand la troupe se défend.</summary>
    public int Defense
    {
        get
        {
            int defense = Type.Defense + DefenseBonus;
            return IsDefending ? (int)Math.Ceiling(defense * (1 + TacticalBattle.DefendBonus)) : defense;
        }
    }

    /// <summary>Points de vie de toute la troupe.</summary>
    public int TotalHitPoints => IsAlive ? (Count - 1) * Type.HitPoints + TopHitPoints : 0;

    /// <summary>Retire des points de vie ; renvoie le nombre de créatures tuées.</summary>
    internal int TakeDamage(int damage)
    {
        int before = Count;
        int total = TotalHitPoints - damage;
        if (total <= 0)
        {
            Stack.Count = 0;
            TopHitPoints = 0;
            return before;
        }
        int hp = Type.HitPoints;
        Stack.Count = (total + hp - 1) / hp;
        TopHitPoints = total - (Stack.Count - 1) * hp;
        return before - Stack.Count;
    }

    /// <summary>Retire des créatures entières (fuite).</summary>
    internal void Lose(int creatures)
    {
        Stack.Count = Math.Max(0, Stack.Count - creatures);
        if (Stack.Count == 0)
            TopHitPoints = 0;
    }

    public override string ToString() => $"{Count} {Type.Id} {Position}";
}
