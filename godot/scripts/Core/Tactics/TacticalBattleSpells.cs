using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// La magie au combat (étape 9) : le héros de chaque camp lance au plus un sort par round, pendant le tour d'une de ses
/// troupes, sans lui prendre son tour. Les points de sort sont comptés à part (le héros ne les perd qu'au bilan,
/// <see cref="BattleReport.AttackerSpellPointsSpent"/>), pour qu'une estimation ne lui coûte rien.
/// </summary>
public sealed partial class TacticalBattle
{
    private readonly Hero?[] _heroes = new Hero?[2];
    private readonly int[] _spellPoints = new int[2];
    private readonly int[] _spellPointsSpent = new int[2];
    private readonly bool[] _castThisRound = new bool[2];

    private void SetUpMagic(Hero? attacker, Hero? defender)
    {
        _heroes[(int)BattleSide.Attacker] = attacker;
        _heroes[(int)BattleSide.Defender] = defender;
        _spellPoints[(int)BattleSide.Attacker] = attacker?.SpellPoints ?? 0;
        _spellPoints[(int)BattleSide.Defender] = defender?.SpellPoints ?? 0;
    }

    /// <summary>Au début d'un round : chaque héros peut de nouveau lancer un sort, et les sorts finis s'arrêtent.</summary>
    private void StartRoundMagic()
    {
        Array.Clear(_castThisRound);
        foreach (var unit in _units)
            unit.ExpireSpells(Round);
    }

    /// <summary>Le héros de ce camp (aucun pour des monstres ou une garnison seule).</summary>
    public Hero? HeroOf(BattleSide side) => _heroes[(int)side];

    /// <summary>Les points de sort qui restent au héros de ce camp dans ce combat.</summary>
    public int SpellPointsOf(BattleSide side) => _spellPoints[(int)side];

    /// <summary>Le héros de ce camp a-t-il déjà lancé un sort dans ce round ?</summary>
    public bool HasCast(BattleSide side) => _castThisRound[(int)side];

    /// <summary>Le camp qui pourrait lancer un sort maintenant : celui de la troupe active, s'il a un héros.</summary>
    public BattleSide? CasterSide =>
        Active is { IsRebel: false } unit && HeroOf(unit.Side) != null ? unit.Side : null;

    /// <summary>Le héros du camp actif peut-il lancer ce sort maintenant (il le connaît, a assez de points, pas encore de sort ce round) ?</summary>
    public bool CanCast(SpellType spell) =>
        !IsOver && CasterSide is { } side && !HasCast(side) && HeroOf(side)!.Knows(spell) && SpellPointsOf(side) >= spell.Cost;

    /// <summary>La force du sort lancé par le héros de ce camp : dégâts, points de vie soignés.</summary>
    public int SpellStrength(SpellType spell, BattleSide side) => spell.Base + spell.PerPower * (HeroOf(side)?.Power ?? 0);

    /// <summary>Combien de rounds dure un sort du héros de ce camp (le round en cours compris) : puissance + 1.</summary>
    public int SpellDuration(BattleSide side) => (HeroOf(side)?.Power ?? 0) + 1;

    /// <summary>Peut-on viser cette case avec ce sort (pour le camp actif) ?</summary>
    public bool IsValidTarget(SpellType spell, Hex target)
    {
        if (CasterSide is not { } side || !Battlefield.InBounds(target))
            return false;
        if (spell.Target == SpellTarget.Area)
            return true;
        if (UnitAt(target) is not { } unit)
            return false;
        if (spell.Target == SpellTarget.Enemy)
            return unit.Side != side;
        if (unit.Side != side)
            return false;
        // ramener des créatures : seulement une troupe qui en a perdu (ou dont la créature de tête est blessée)
        return spell.Effect != SpellEffect.Resurrection || unit.Count < _startCounts[unit] || unit.TopHitPoints < unit.Type.HitPoints;
    }

    /// <summary>Les cases que ce sort peut viser maintenant.</summary>
    public IEnumerable<Hex> SpellTargets(SpellType spell)
    {
        for (int r = 0; r < Battlefield.Rows; r++)
        for (int c = 0; c < Battlefield.Columns; c++)
            if (IsValidTarget(spell, new Hex(c, r)))
                yield return new Hex(c, r);
    }

    /// <summary>Les troupes que toucherait ce sort lancé sur cette case (pour une zone : la case et ses 6 voisines).</summary>
    public IReadOnlyList<BattleUnit> AffectedBy(SpellType spell, Hex target)
    {
        if (spell.Target != SpellTarget.Area)
            return UnitAt(target) is { } unit ? new[] { unit } : Array.Empty<BattleUnit>();
        return new[] { target }.Concat(target.Neighbours())
            .Select(UnitAt).OfType<BattleUnit>().Distinct().ToList();
    }

    /// <summary>
    /// Le héros du camp actif lance ce sort sur cette case. La troupe active garde son tour (sauf si le sort la tue).
    /// Renvoie false si le sort ne peut pas être lancé là.
    /// </summary>
    public bool Cast(SpellType spell, Hex target)
    {
        if (!CanCast(spell) || !IsValidTarget(spell, target))
            return false;
        var side = CasterSide!.Value;
        int s = (int)side;
        _spellPoints[s] -= spell.Cost;
        _spellPointsSpent[s] += spell.Cost;
        _castThisRound[s] = true;
        _log.Add(new SpellCast(HeroOf(side)!, side, spell, target));
        int strength = SpellStrength(spell, side);
        foreach (var unit in AffectedBy(spell, target))
            Apply(spell, unit, strength, side);
        if (CheckEnd())
            return true;
        if (Active is { IsAlive: false })
            EndTurn();
        return true;
    }

    private void Apply(SpellType spell, BattleUnit unit, int strength, BattleSide side)
    {
        switch (spell.Effect)
        {
            case SpellEffect.Damage:
                int killed = unit.TakeDamage(strength);
                _log.Add(new SpellDamaged(unit, spell, strength, killed));
                break;
            case SpellEffect.Cure:
                int healed = unit.Heal(strength);
                unit.RemoveHarmfulSpells();
                _log.Add(new UnitHealed(unit, spell, healed, 0));
                break;
            case SpellEffect.Resurrection:
                var (hitPoints, revived) = unit.Revive(strength, _startCounts[unit]);
                _log.Add(new UnitHealed(unit, spell, hitPoints, revived));
                break;
            default:
                unit.Enchant(spell, Round + SpellDuration(side) - 1);
                _log.Add(new SpellApplied(unit, spell));
                break;
        }
    }
}
