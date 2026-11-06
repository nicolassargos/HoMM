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
        if (spell.Target == SpellTarget.Any)
            return true;
        if (spell.Target is SpellTarget.Enemy or SpellTarget.Chain)
            return unit.Side != side;
        if (unit.Side != side)
            return false;
        if (spell.Effect == SpellEffect.Teleport)
            return TeleportDestinations(unit).Any();
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
    public IReadOnlyList<BattleUnit> AffectedBy(SpellType spell, Hex target) =>
        SpellHits(spell, target, CasterSide ?? BattleSide.Attacker).Select(h => h.Unit).ToList();

    /// <summary>
    /// Les troupes que toucherait ce sort, chacune avec sa force : la même pour toutes, sauf la chaîne d'éclairs (la
    /// troupe visée, puis la plus proche de la précédente qui n'a pas encore été touchée, amie comprise, à force moitié
    /// chaque fois, <see cref="SpellType.ChainTargets"/> au plus).
    /// </summary>
    public IReadOnlyList<(BattleUnit Unit, int Strength)> SpellHits(SpellType spell, Hex target, BattleSide side)
    {
        int strength = SpellStrength(spell, side);
        if (spell.Target == SpellTarget.Area)
            return new[] { target }.Concat(target.Neighbours())
                .Select(UnitAt).OfType<BattleUnit>().Distinct().Select(u => (u, strength)).ToList();
        if (UnitAt(target) is not { } first)
            return Array.Empty<(BattleUnit, int)>();
        var hits = new List<(BattleUnit, int)> { (first, strength) };
        if (spell.Target != SpellTarget.Chain)
            return hits;
        var current = first;
        while (hits.Count < SpellType.ChainTargets && strength > 1)
        {
            strength /= 2;
            var next = _units.Where(u => u.IsAlive && hits.All(h => h.Item1 != u))
                .OrderBy(u => Distance(current, u)).ThenBy(u => u.Id).FirstOrDefault();
            if (next == null)
                break;
            hits.Add((next, strength));
            current = next;
        }
        return hits;
    }

    /// <summary>Les cases où une téléportation peut envoyer cette troupe : toute case où elle tient, sauf la sienne.</summary>
    public IEnumerable<Hex> TeleportDestinations(BattleUnit unit)
    {
        for (int r = 0; r < Battlefield.Rows; r++)
        for (int c = 0; c < Battlefield.Columns; c++)
        {
            var hex = new Hex(c, r);
            if (hex != unit.Position && Fits(unit, hex))
                yield return hex;
        }
    }

    /// <summary>
    /// Le héros du camp actif lance ce sort sur cette case. La troupe active garde son tour (sauf si le sort la tue).
    /// <paramref name="destination"/> : pour une téléportation, la case où va la troupe. Renvoie false si le sort ne
    /// peut pas être lancé là.
    /// </summary>
    public bool Cast(SpellType spell, Hex target, Hex? destination = null)
    {
        if (!CanCast(spell) || !IsValidTarget(spell, target))
            return false;
        if (spell.Effect == SpellEffect.Teleport
            && (destination is not { } to || !TeleportDestinations(UnitAt(target)!).Contains(to)))
            return false;
        var side = CasterSide!.Value;
        int s = (int)side;
        _spellPoints[s] -= spell.Cost;
        _spellPointsSpent[s] += spell.Cost;
        _castThisRound[s] = true;
        var hits = SpellHits(spell, target, side);
        _log.Add(new SpellCast(HeroOf(side)!, side, spell, target));
        foreach (var (unit, strength) in hits)
            Apply(spell, unit, strength, side, destination);
        if (CheckEnd())
            return true;
        if (Active is { IsAlive: false })
            EndTurn();
        return true;
    }

    private void Apply(SpellType spell, BattleUnit unit, int strength, BattleSide side, Hex? destination)
    {
        switch (spell.Effect)
        {
            case SpellEffect.Damage:
                if (!unit.IsAlive)
                    break;
                int killed = unit.TakeDamage(strength);
                unit.Wake();
                _log.Add(new SpellDamaged(unit, spell, strength, killed));
                break;
            case SpellEffect.Teleport:
                var from = unit.Position;
                unit.Position = destination!.Value;
                _log.Add(new UnitTeleported(unit, from, unit.Position));
                break;
            case SpellEffect.Dispel:
                unit.RemoveAllSpells();
                _log.Add(new SpellsDispelled(unit, spell));
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
