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

    /// <summary>Les points de sort gagnés (prix du sang, vols du Diablotin) moins ceux volés, et ce qu'a rendu le prix du sang.</summary>
    private readonly int[] _spellPointsGained = new int[2];
    private readonly int[] _bloodPaid = new int[2];
    private readonly bool[] _castThisRound = new bool[2];

    private void SetUpMagic(Hero? attacker, Hero? defender)
    {
        _heroes[(int)BattleSide.Attacker] = attacker;
        _heroes[(int)BattleSide.Defender] = defender;
        _spellPoints[(int)BattleSide.Attacker] = attacker?.SpellPoints ?? 0;
        _spellPoints[(int)BattleSide.Defender] = defender?.SpellPoints ?? 0;
    }

    /// <summary>Au début d'un round : chaque héros peut de nouveau lancer un sort, et les sorts finis s'arrêtent (les murs de feu aussi).</summary>
    private void StartRoundMagic()
    {
        Array.Clear(_castThisRound);
        foreach (var unit in _units)
            unit.ExpireSpells(Round);
        var faded = _fire.Where(f => f.Value.LastRound < Round).Select(f => f.Key).ToList();
        foreach (var hex in faded)
            _fire.Remove(hex);
        if (faded.Count > 0)
            _log.Add(new FireWallFaded(faded));
    }

    /// <summary>Une case d'un mur de feu : la force des brûlures et le dernier round où elle brûle.</summary>
    public sealed record FireCell(BattleSide Side, int Strength, int LastRound);

    private readonly Dictionary<Hex, FireCell> _fire = new();

    /// <summary>Les cases qui brûlent (murs de feu).</summary>
    public IReadOnlyDictionary<Hex, FireCell> Fire => _fire;

    /// <summary>
    /// Les cases d'un mur de feu visé sur cette case : <see cref="SpellType.WallLength"/> cases de la même colonne, la
    /// visée au milieu (une ligne qui zigzague d'une demi-case, comme les rangées), sans les obstacles ni le hors-champ.
    /// </summary>
    public IReadOnlyList<Hex> WallCells(Hex target) =>
        Enumerable.Range(target.Row - SpellType.WallLength / 2, SpellType.WallLength)
            .Select(r => new Hex(target.Col, r)).Where(Field.IsOpen).ToList();

    /// <summary>
    /// Une troupe vient de passer par ces cases (son chemin, puis les cases où elle se tient) : si l'une brûle, elle
    /// perd une fois la force du mur le plus fort (moitié pour une créature façonnée ; rien pour une créature de
    /// l'Enfer, insensible au feu).
    /// </summary>
    private void CrossFire(BattleUnit unit, IEnumerable<Hex> cells)
    {
        if (_fire.Count == 0 || unit.IsInfernal || !unit.IsAlive)
            return;
        int strength = cells.Concat(unit.Hexes).Select(h => _fire.TryGetValue(h, out var f) ? f.Strength : 0).Max();
        if (strength <= 0)
            return;
        int damage = unit.IsMade ? Math.Max(1, strength / 2) : strength;
        int killed = unit.TakeDamage(damage);
        unit.Wake();
        _log.Add(new UnitCrossedFire(unit, damage, killed));
        BloodPrice(unit, killed);
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
        !IsOver && !spell.OnMap && CasterSide is { } side && !HasCast(side) && HeroOf(side)!.Knows(spell) && SpellPointsOf(side) >= HeroOf(side)!.SpellCost(spell);

    /// <summary>La force du sort lancé par le héros de ce camp : dégâts, points de vie soignés.</summary>
    /// <summary>La force du sort : base + par point de puissance, plus ce qu'ajoute l'école de magie du héros.</summary>
    public int SpellStrength(SpellType spell, BattleSide side) =>
        (spell.Base + spell.PerPower * (HeroOf(side)?.Power ?? 0)) * (100 + (HeroOf(side)?.SpellBonus(spell) ?? 0)) / 100;

    /// <summary>Combien de rounds dure un sort du héros de ce camp (le round en cours compris) : puissance + 1.</summary>
    public int SpellDuration(BattleSide side) => (HeroOf(side)?.Power ?? 0) + 1;

    /// <summary>Peut-on viser cette case avec ce sort (pour le camp actif) ?</summary>
    public bool IsValidTarget(SpellType spell, Hex target)
    {
        if (CasterSide is not { } side || !Battlefield.InBounds(target))
            return false;
        if (spell.Target == SpellTarget.Area)
            return true;
        if (spell.Target == SpellTarget.Wall)
            return Field.IsOpen(target);
        if (UnitAt(target) is not { } unit)
            return false;
        if (unit.IsUndead && !spell.AffectsUndead)
            return false; // un mort-vivant n'y est pas sensible
        if (unit.IsMade && spell.Harmful)
            return false; // une créature façonnée résiste aux sorts néfastes
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
    /// chaque fois, <see cref="SpellType.ChainTargets"/> au plus, à <see cref="SpellType.ChainReach"/> cases au plus de la
    /// précédente).
    /// </summary>
    public IReadOnlyList<(BattleUnit Unit, int Strength)> SpellHits(SpellType spell, Hex target, BattleSide side)
    {
        int strength = SpellStrength(spell, side);
        if (spell.Target == SpellTarget.Area)
            return new[] { target }.Concat(target.Neighbours())
                .Select(UnitAt).OfType<BattleUnit>().Distinct().Where(u => !Burns(spell, u)).Select(u => (u, strength)).ToList();
        if (spell.Target == SpellTarget.Wall)
            return WallCells(target).Select(UnitAt).OfType<BattleUnit>().Distinct().Where(u => !Burns(spell, u))
                .Select(u => (u, strength)).ToList();
        if (UnitAt(target) is not { } first)
            return Array.Empty<(BattleUnit, int)>();
        if (Burns(spell, first))
            return Array.Empty<(BattleUnit, int)>();
        if (Mirror(spell, first, side) is { } caster)
            return new[] { (caster, strength) };
        var hits = new List<(BattleUnit, int)> { (first, strength) };
        if (spell.Target != SpellTarget.Chain)
            return hits;
        var current = first;
        while (hits.Count < SpellType.ChainTargets && strength > 1)
        {
            strength /= 2;
            var next = _units.Where(u => u.IsAlive && hits.All(h => h.Item1 != u) && Distance(current, u) <= SpellType.ChainReach)
                .OrderBy(u => Distance(current, u)).ThenBy(u => u.Id).FirstOrDefault();
            if (next == null)
                break;
            hits.Add((next, strength));
            current = next;
        }
        return hits;
    }

    /// <summary>Un sort de dégâts de l'école du feu (un mur de feu aussi) sur une créature de l'Enfer : elle y est insensible.</summary>
    private static bool Burns(SpellType spell, BattleUnit unit) =>
        spell.Effect is SpellEffect.Damage or SpellEffect.FireWall && spell.School == FireSchool && unit.IsInfernal;

    /// <summary>
    /// Le miroir de cristal (<see cref="Ability.CrystalMirror"/>) : un sort néfaste lancé sur un Roi de cristal frappe la
    /// troupe active du lanceur à sa place. Renvoie cette troupe, ou rien si le sort n'est pas renvoyé.
    /// </summary>
    private BattleUnit? Mirror(SpellType spell, BattleUnit? aimed, BattleSide side) =>
        spell.Harmful && aimed is { } king && king.Side != side && king.Has(Ability.CrystalMirror)
        && Active is { IsAlive: true } caster && caster.Side == side ? caster : null;

    /// <summary>L'école du feu (data/spells.json).</summary>
    public const string FireSchool = "feu";

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
        int cost = HeroOf(side)!.SpellCost(spell);
        _spellPoints[s] -= cost;
        _spellPointsSpent[s] += cost;
        _castThisRound[s] = true;
        var hits = SpellHits(spell, target, side);
        _log.Add(new SpellCast(HeroOf(side)!, side, spell, target));
        if (spell.Effect == SpellEffect.FireWall)
        {
            var cells = WallCells(target);
            int strength = SpellStrength(spell, side), last = Round + SpellType.WallRounds - 1;
            foreach (var hex in cells)
                if (!_fire.TryGetValue(hex, out var old) || old.Strength <= strength)
                    _fire[hex] = new FireCell(side, strength, last);
            _log.Add(new FireWallRaised(spell, side, cells, last));
        }
        if (UnitAt(target) is { } aimed && Mirror(spell, aimed, side) is { } caster)
            _log.Add(new SpellReflected(aimed, caster, spell));
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
            case SpellEffect.FireWall:
                if (!unit.IsAlive)
                    break;
                // une créature façonnée résiste à la magie : moitié moins de dégâts
                int damage = unit.IsMade ? Math.Max(1, strength / 2) : strength;
                int killed = unit.TakeDamage(damage);
                unit.Wake();
                _log.Add(new SpellDamaged(unit, spell, damage, killed));
                BloodPrice(unit, killed);
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
                unit.CurePoison();
                _log.Add(new UnitHealed(unit, spell, healed, 0));
                break;
            case SpellEffect.Resurrection:
                var (hitPoints, revived) = unit.Revive(strength, _startCounts[unit]);
                _log.Add(new UnitHealed(unit, spell, hitPoints, revived));
                break;
            default:
                // (un sort renvoyé par le miroir de cristal peut tomber sur une troupe qui n'y est pas sensible)
                if (unit.IsMade && spell.Harmful || unit.IsUndead && !spell.AffectsUndead)
                    break;
                unit.Enchant(spell, Round + SpellDuration(side) - 1);
                _log.Add(new SpellApplied(unit, spell));
                break;
        }
    }
}
