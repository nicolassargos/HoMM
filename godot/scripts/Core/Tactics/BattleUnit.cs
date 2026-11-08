using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// Grande créature : occupe 2 cases côte à côte (Cavalier, Dragon), la tête devant et l'arrière derrière, du côté de
    /// son camp. Elle se déplace, frappe et se fait frapper par ses deux cases.
    /// </summary>
    public const string TwoHex = "two-hex";

    /// <summary>
    /// Mort-vivant (étape 9) : insensible aux sorts de l'esprit (aveuglement, malédiction, faiblesse) et au poison ; les
    /// soins et la résurrection n'ont pas d'effet sur lui.
    /// </summary>
    public const string Undead = "undead";

    /// <summary>Chaque créature ennemie qu'il tue ajoute une créature à sa troupe, pour de bon (Fantôme).</summary>
    public const string GhostGrowth = "ghost-growth";

    /// <summary>Sa morsure empoisonne : des dégâts au début de chaque tour de la cible, pendant quelques rounds (Araignée).</summary>
    public const string Poison = "poison";

    /// <summary>Draine la vie : ses dégâts soignent sa créature de tête, puis ramènent des créatures tuées (Vampire).</summary>
    public const string LifeDrain = "life-drain";

    /// <summary>Nuage de mort : son tir touche aussi les troupes autour de la cible, sauf les morts-vivants (Liche).</summary>
    public const string DeathCloud = "death-cloud";

    /// <summary>Balayage : au corps à corps, frappe aussi tous les autres ennemis voisins (Colosse d'os).</summary>
    public const string Sweep = "sweep";

    /// <summary>Rage (Barbares) : chaque coup reçu donne de l'attaque à la troupe, jusqu'à la fin du combat.</summary>
    public const string Rage = "rage";

    /// <summary>Frappe et repart (Nomades) : après son attaque, la troupe se déplace avec le mouvement qui lui reste.</summary>
    public const string HitAndRun = "hit-and-run";

    /// <summary>Foudre (Oiseau-tonnerre) : son attaque appelle parfois la foudre sur la cible (dégâts sans défense).</summary>
    public const string Lightning = "lightning";

    /// <summary>Griffes : ignore 60 % de la défense de la cible (Béhémoth).</summary>
    public const string Rending = "rending";

    public static readonly string[] Known =
    {
        Shoots, NoRangePenalty, Flies, DoubleRetaliation, NoRetaliation, Breath, RangedResistance,
        Charge, QuickStart, DoubleAttack, ShortRange, Crushing, Regenerates, TwoHex,
        Undead, GhostGrowth, Poison, LifeDrain, DeathCloud, Sweep,
        Rage, HitAndRun, Lightning, Rending,
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
    public BattleSide Side { get; private set; }

    /// <summary>Place de la troupe dans son armée (départage l'ordre de jeu).</summary>
    public int Slot { get; }

    /// <summary>La case de la troupe ; pour une grande créature, celle de sa tête (la case de devant).</summary>
    public Hex Position { get; internal set; }

    /// <summary>Une grande créature, sur 2 cases (<see cref="Ability.TwoHex"/>).</summary>
    public bool IsLarge => Has(Ability.TwoHex);

    /// <summary>
    /// Les cases qu'occuperait la troupe avec sa tête sur <paramref name="head"/> : une seule, ou deux pour une grande
    /// créature (l'arrière sur la même rangée, à gauche pour l'attaquant, à droite pour le défenseur).
    /// </summary>
    public IEnumerable<Hex> HexesAt(Hex head)
    {
        yield return head;
        if (IsLarge)
            yield return new Hex(head.Col + (Side == BattleSide.Attacker ? -1 : 1), head.Row);
    }

    /// <summary>Les cases qu'occupe la troupe.</summary>
    public IEnumerable<Hex> Hexes => HexesAt(Position);

    public bool Occupies(Hex hex) => Hexes.Contains(hex);

    /// <summary>Une troupe révoltée : elle combat dans le camp du défenseur, sans le bonus du héros.</summary>
    public bool IsRebel { get; private set; }

    /// <summary>
    /// La troupe se révolte pendant le combat : elle passe dans l'autre camp, sans le bonus de son héros. Une grande
    /// créature garde ses deux cases (sa tête devient la case de derrière, vue de son nouveau camp).
    /// </summary>
    internal void Turn()
    {
        var back = Hexes.Last();
        Side = Side == BattleSide.Attacker ? BattleSide.Defender : BattleSide.Attacker;
        IsRebel = true;
        AttackBonus = DefenseBonus = 0;
        if (IsLarge)
            Position = back;
    }

    /// <summary>Bonus du héros (attaque et défense).</summary>
    public int AttackBonus { get; private set; }
    public int DefenseBonus { get; private set; }

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

    /// <summary>Attaque, bonus du héros et rage compris, moins la faiblesse.</summary>
    public int Attack => Math.Max(0, Type.Attack + AttackBonus + Rage + SpellAmount(SpellEffect.Bloodlust) - SpellAmount(SpellEffect.Weakness));

    /// <summary>L'attaque gagnée par la rage dans ce combat (<see cref="Ability.Rage"/>).</summary>
    public int Rage { get; private set; }

    /// <summary>La troupe enrage : <paramref name="amount"/> d'attaque en plus, sans dépasser <paramref name="max"/>. Renvoie false si elle est déjà au plus fort.</summary>
    internal bool Enrage(int amount, int max)
    {
        if (!IsAlive || Rage >= max)
            return false;
        Rage = Math.Min(max, Rage + amount);
        return true;
    }

    /// <summary>Défense, bonus du héros et peau de pierre compris, et +20 % (arrondi au-dessus) quand la troupe se défend.</summary>
    public int Defense
    {
        get
        {
            int defense = Type.Defense + DefenseBonus + SpellAmount(SpellEffect.StoneSkin);
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

    // ------------------------------------------------------------------ sorts (étape 9)

    private readonly Dictionary<SpellEffect, ActiveSpell> _spells = new();

    /// <summary>Les sorts qui durent, posés sur la troupe.</summary>
    public IReadOnlyCollection<ActiveSpell> Spells => _spells.Values;

    public bool IsUnder(SpellEffect effect) => _spells.ContainsKey(effect);

    /// <summary>Le bonus ou malus d'un sort posé sur la troupe (0 sans ce sort).</summary>
    public int SpellAmount(SpellEffect effect) => _spells.TryGetValue(effect, out var spell) ? spell.Amount : 0;

    /// <summary>Pose un sort qui dure jusqu'à la fin du round <paramref name="lastRound"/> ; il remplace son contraire (bénédiction et malédiction, hâte et lenteur).</summary>
    internal void Enchant(SpellType spell, int lastRound)
    {
        var opposite = spell.Effect switch
        {
            SpellEffect.Bless => SpellEffect.Curse,
            SpellEffect.Curse => SpellEffect.Bless,
            SpellEffect.Haste => SpellEffect.Slow,
            SpellEffect.Slow => SpellEffect.Haste,
            _ => (SpellEffect?)null,
        };
        if (opposite is { } other)
            _spells.Remove(other);
        _spells[spell.Effect] = new ActiveSpell(spell, spell.Amount, lastRound);
    }

    /// <summary>Retire les sorts finis avant ce round.</summary>
    internal void ExpireSpells(int round)
    {
        foreach (var effect in _spells.Where(p => p.Value.LastRound < round).Select(p => p.Key).ToList())
            _spells.Remove(effect);
    }

    /// <summary>Retire tous les sorts (dissipation).</summary>
    internal void RemoveAllSpells() => _spells.Clear();

    /// <summary>Un coup réveille une troupe aveuglée.</summary>
    internal void Wake() => _spells.Remove(SpellEffect.Blind);

    internal void RemoveHarmfulSpells()
    {
        foreach (var effect in _spells.Where(p => p.Value.Spell.Harmful).Select(p => p.Key).ToList())
            _spells.Remove(effect);
    }

    /// <summary>Soigne la créature de tête (sans ramener de créature) ; renvoie les points de vie rendus.</summary>
    internal int Heal(int hitPoints)
    {
        if (!IsAlive)
            return 0;
        int healed = Math.Min(hitPoints, Type.HitPoints - TopHitPoints);
        TopHitPoints += healed;
        return healed;
    }

    /// <summary>
    /// Rend des points de vie à la troupe, en ramenant des créatures tuées, sans dépasser <paramref name="maxCount"/>
    /// créatures (le nombre du début du combat). Renvoie les points de vie rendus et les créatures ramenées.
    /// </summary>
    internal (int HitPoints, int Revived) Revive(int hitPoints, int maxCount)
    {
        if (!IsAlive)
            return (0, 0);
        int before = Count, total = TotalHitPoints, hp = Type.HitPoints;
        int after = Math.Min(maxCount * hp, total + hitPoints);
        if (after <= total)
            return (0, 0);
        Stack.Count = (after + hp - 1) / hp;
        TopHitPoints = after - (Stack.Count - 1) * hp;
        return (after - total, Count - before);
    }

    /// <summary>Un mort-vivant (voir <see cref="Ability.Undead"/>).</summary>
    public bool IsUndead => Has(Ability.Undead);

    /// <summary>Le poison : les dégâts au début de chacun de ses tours, et pendant combien de tours encore.</summary>
    public int PoisonDamage { get; private set; }
    public int PoisonTurnsLeft { get; private set; }

    /// <summary>Empoisonne la troupe (le plus fort poison l'emporte, et la durée repart).</summary>
    internal void Poison(int damage, int turns)
    {
        if (IsUndead || !IsAlive)
            return;
        PoisonDamage = Math.Max(PoisonDamage, damage);
        PoisonTurnsLeft = turns;
    }

    internal void CurePoison() => PoisonTurnsLeft = PoisonDamage = 0;

    /// <summary>Le poison agit (début de son tour) : renvoie les dégâts et les créatures tuées.</summary>
    internal (int Damage, int Killed) TakePoison()
    {
        int damage = PoisonDamage;
        int killed = TakeDamage(damage);
        if (--PoisonTurnsLeft <= 0)
            CurePoison();
        return (damage, killed);
    }

    /// <summary>La troupe grossit de créatures neuves (Fantôme qui tue).</summary>
    internal void Grow(int creatures)
    {
        if (IsAlive && creatures > 0)
            Stack.Count += creatures;
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
