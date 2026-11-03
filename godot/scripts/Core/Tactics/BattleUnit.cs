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

    /// <summary>Vole : passe au-dessus des obstacles et des troupes (sa vitesse, à vol d'oiseau).</summary>
    public const string Flies = "flies";

    /// <summary>Riposte deux fois par round (Griffon).</summary>
    public const string DoubleRetaliation = "double-retaliation";

    /// <summary>Sa cible ne peut pas riposter (Ange).</summary>
    public const string NoRetaliation = "no-retaliation";

    /// <summary>Souffle : l'attaque touche aussi la troupe juste derrière la cible, amie ou ennemie (Dragon).</summary>
    public const string Breath = "breath";

    /// <summary>Les tirs contre elle font moitié moins de dégâts (bouclier de l'Épéiste, os du Squelette).</summary>
    public const string RangedResistance = "ranged-resistance";

    /// <summary>Charge : +5 % de dégâts par case parcourue avant d'attaquer (Cavalier).</summary>
    public const string Charge = "charge";

    /// <summary>+2 de vitesse au premier round (Gobelin : une embuscade).</summary>
    public const string QuickStart = "quick-start";

    /// <summary>Frappe deux fois (Loup) ; la cible ne riposte qu'une fois.</summary>
    public const string DoubleAttack = "double-attack";

    /// <summary>Tir court : moitié des dégâts au-delà de 3 cases au lieu de 6 (haches de l'Orc).</summary>
    public const string ShortRange = "short-range";

    /// <summary>Coup écrasant : ignore 40 % de la défense de la cible (Ogre).</summary>
    public const string Crushing = "crushing";

    /// <summary>Se régénère : sa créature de tête retrouve tous ses points de vie au début de chacun de ses tours (Troll).</summary>
    public const string Regenerates = "regenerates";

    public static readonly string[] Known =
    {
        Shoots, NoRangePenalty, Flies, DoubleRetaliation, NoRetaliation, Breath, RangedResistance,
        Charge, QuickStart, DoubleAttack, ShortRange, Crushing, Regenerates,
    };
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
    public bool CanShoot => Has(Ability.Shoots);

    /// <summary>La troupe a-t-elle cette capacité (voir <see cref="Ability"/>) ?</summary>
    public bool Has(string ability) => Type.Abilities.Contains(ability);
    public bool HasRangePenalty => !Has(Ability.NoRangePenalty);

    /// <summary>Ripostes par round : une, deux pour <see cref="Ability.DoubleRetaliation"/>.</summary>
    public int RetaliationsPerRound => Has(Ability.DoubleRetaliation) ? 2 : 1;

    /// <summary>Remet sa créature de tête à pleine santé (régénération).</summary>
    internal bool Regenerate()
    {
        if (!IsAlive || TopHitPoints >= Type.HitPoints)
            return false;
        TopHitPoints = Type.HitPoints;
        return true;
    }

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
