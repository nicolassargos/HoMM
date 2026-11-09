using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Où en est le combat.</summary>
public enum BattleOutcome { Ongoing, AttackerWon, DefenderWon, AttackerFled }

/// <summary>Ce qui vient de se passer dans le combat (l'écran de combat le montre, dans l'ordre).</summary>
public abstract record BattleAction;

/// <summary>Un nouveau round commence : chaque troupe va jouer une fois.</summary>
public sealed record RoundStarted(int Round) : BattleAction;

/// <summary>C'est au tour de cette troupe.</summary>
public sealed record UnitTurn(BattleUnit Unit) : BattleAction;

/// <summary>La troupe s'est déplacée, case par case.</summary>
public sealed record UnitMoved(BattleUnit Unit, IReadOnlyList<Hex> Path) : BattleAction;

/// <summary>
/// Un coup : <paramref name="Damage"/> points de dégâts, <paramref name="Killed"/> créatures tuées.
/// <paramref name="Ranged"/> : un tir ; <paramref name="Retaliation"/> : une riposte.
/// </summary>
public sealed record UnitStruck(BattleUnit Attacker, BattleUnit Target, int Damage, int Killed, bool Ranged, bool Retaliation) : BattleAction;

public sealed record UnitWaited(BattleUnit Unit) : BattleAction;

/// <summary>Le poison agit au début du tour de la troupe.</summary>
public sealed record UnitPoisoned(BattleUnit Unit, int Damage, int Killed) : BattleAction;

/// <summary>Une troupe grossit en tuant (Fantôme) : <paramref name="Added"/> créatures de plus.</summary>
public sealed record UnitGrew(BattleUnit Unit, int Added) : BattleAction;

/// <summary>Une troupe draine la vie (Vampire) : points de vie rendus, créatures ramenées.</summary>
public sealed record UnitDrained(BattleUnit Unit, int HitPoints, int Revived) : BattleAction;

/// <summary>La troupe s'est régénérée : sa créature de tête a retrouvé tous ses points de vie.</summary>
public sealed record UnitRegenerated(BattleUnit Unit) : BattleAction;
public sealed record UnitDefended(BattleUnit Unit) : BattleAction;

/// <summary>La troupe enrage (Barbares) : son attaque gagnée par la rage vaut maintenant <paramref name="Rage"/>.</summary>
public sealed record UnitEnraged(BattleUnit Unit, int Rage) : BattleAction;

/// <summary>La foudre appelée par <paramref name="Attacker"/> (Oiseau-tonnerre) frappe la cible.</summary>
public sealed record LightningStruck(BattleUnit Attacker, BattleUnit Target, int Damage, int Killed) : BattleAction;

/// <summary>Un Druide soigne une troupe amie voisine : <paramref name="HitPoints"/> points de vie rendus.</summary>
public sealed record UnitSoothed(BattleUnit Healer, BattleUnit Unit, int HitPoints) : BattleAction;

/// <summary>La troupe est enracinée (Tréant) : elle ne pourra pas se déplacer à son prochain tour.</summary>
public sealed record UnitRooted(BattleUnit Unit) : BattleAction;

/// <summary>La troupe a trop grossi pour le charisme de son héros : elle se révolte et change de camp.</summary>
public sealed record UnitRevolted(BattleUnit Unit) : BattleAction;

/// <summary>La troupe a frappé et peut repartir (Nomades) : <paramref name="Steps"/> cases de mouvement lui restent.</summary>
public sealed record UnitMayRetreat(BattleUnit Unit, int Steps) : BattleAction;

/// <summary>Le héros s'enfuit : <paramref name="Losses"/>, les créatures perdues dans la fuite.</summary>
public sealed record AttackerFled(IReadOnlyList<CreatureStack> Losses) : BattleAction;

public sealed record BattleEnded(BattleOutcome Outcome) : BattleAction;

/// <summary>
/// Un combat tactique sur un champ de bataille de 11 × 9 cases (étape 6). L'attaquant (le héros) est à gauche, le
/// défenseur à droite. À chaque round, chaque troupe joue une fois, de la plus rapide à la plus lente : elle se
/// déplace, attaque une troupe voisine (qui riposte une fois par round), tire si elle sait tirer, attend (elle jouera
/// à la fin du round) ou se défend. Le héros peut aussi s'enfuir, en perdant une grande partie de son armée.
/// Les dégâts sont tirés au sort (entre les dégâts min et max) avec le hasard de la partie : même graine, même combat.
/// Les armées sont modifiées pendant le combat (créatures tuées) ; <see cref="Report"/> donne le bilan.
/// Le combat rapide (<see cref="Battle.Resolve"/>) le fait jouer d'un coup par <see cref="BattleAI"/>.
/// </summary>
public sealed partial class TacticalBattle
{
    /// <summary>Au-delà de cette distance, un tireur (sauf capacité <see cref="Ability.NoRangePenalty"/>) fait moitié moins de dégâts.</summary>
    public const int LongRange = 6;

    /// <summary>La même limite pour un tir court (<see cref="Ability.ShortRange"/>).</summary>
    public const int ShortRange = 3;

    /// <summary>Bonus de charge par case parcourue avant l'attaque (+5 %).</summary>
    public const double ChargeBonus = 0.05;

    /// <summary>Part de la défense ignorée par un coup écrasant (40 %) et par les griffes (60 %).</summary>
    public const double CrushingShare = 0.4, RendingShare = 0.6;

    /// <summary>La rage (Barbares) : attaque gagnée à chaque coup reçu, et au plus dans un combat.</summary>
    public const int RagePerHit = 2, RageMax = 6;

    /// <summary>La foudre de l'Oiseau-tonnerre : une chance sur cinq à chaque attaque, tant de dégâts par oiseau.</summary>
    public const double LightningChance = 0.2;

    /// <summary>La régénération de la nature : la part des points de vie de la créature de tête retrouvée à chaque tour.</summary>
    public const double NatureRegenerationShare = 0.25;

    /// <summary>L'aura de soin : points de vie rendus par Druide à la créature de tête de chaque troupe amie voisine.</summary>
    public const int HealingAuraPerCreature = 5;
    public const int LightningPerCreature = 15;

    /// <summary>Le poison de l'Araignée : dégâts par créature qui mord, et pendant combien de tours de la cible.</summary>
    public const int PoisonPerCreature = 2, PoisonTurns = 3;

    /// <summary>Vitesse en plus au premier round (<see cref="Ability.QuickStart"/>).</summary>
    public const int QuickStartBonus = 2;

    /// <summary>Défense en plus quand une troupe se défend (+20 %).</summary>
    public const double DefendBonus = 0.2;

    /// <summary>Part de chaque troupe perdue dans une fuite : tirée entre ces deux valeurs.</summary>
    public const double MinFleeLoss = 0.5, MaxFleeLoss = 0.9;

    /// <summary>Nombre d'obstacles tirés au sort sur le champ de bataille.</summary>
    public const int MinObstacles = 2, MaxObstacles = 5;

    /// <summary>Au-delà, le combat s'arrête (il ne peut pas durer toujours) : le défenseur garde le terrain.</summary>
    public const int MaxRounds = 100;

    private readonly GameRandom _rng;
    private readonly List<BattleUnit> _units = new();
    private readonly Queue<BattleUnit> _queue = new();
    private readonly List<BattleUnit> _waiting = new();
    private readonly List<BattleAction> _log = new();
    private readonly Dictionary<BattleUnit, int> _startCounts = new();

    /// <summary>Les troupes révoltées pendant le combat : leur camp d'avant, et leur nombre au moment de la révolte.</summary>
    private readonly Dictionary<BattleUnit, (BattleSide Side, int Count)> _turned = new();
    private readonly List<CreatureStack> _fleeLosses = new();
    private readonly List<CreatureStack> _rebelsBefore;
    private readonly List<CreatureStack> _defenderRebelsBefore;
    private readonly Army? _defenderRebels;
    private readonly Army _attacker;
    private readonly Army _defender;
    private readonly Army? _rebels;

    /// <summary>
    /// Prépare le combat : les troupes de chaque camp sur leur colonne, quelques obstacles au milieu, puis le premier
    /// round. <paramref name="hero"/> (celui de l'attaquant) ajoute son attaque et sa défense à ses troupes.
    /// <paramref name="rebels"/> : troupes révoltées, qui combattent avec le défenseur, sans le bonus du héros.
    /// </summary>
    /// <param name="defenderHero">Le héros qui se défend (combat entre héros, étape 8) : son attaque et sa défense
    /// s'ajoutent à celles de ses troupes, comme pour l'attaquant.</param>
    /// <param name="defenderRebels">Les troupes révoltées du héros qui se défend : elles combattent avec l'attaquant, sans
    /// bonus de héros. Elles ne sauvent pas l'attaquant : il a perdu quand ses propres troupes sont toutes tombées.</param>
    public TacticalBattle(Army attacker, Army defender, GameRandom rng, Hero? hero = null, Army? rebels = null, Hero? defenderHero = null,
        Army? defenderRebels = null)
    {
        _rng = rng;
        _attacker = attacker;
        _defender = defender;
        _rebels = rebels;
        _rebelsBefore = rebels?.Stacks.Where(s => s.Count > 0).Select(s => new CreatureStack(s.Type, s.Count)).ToList() ?? new();
        _defenderRebels = defenderRebels;
        _defenderRebelsBefore = defenderRebels?.Stacks.Where(s => s.Count > 0).Select(s => new CreatureStack(s.Type, s.Count)).ToList() ?? new();

        var attackers = attacker.Stacks.Where(s => s.Count > 0).Select(s => (Stack: s, Rebel: false))
            .Concat((defenderRebels?.Stacks ?? new List<CreatureStack>()).Where(s => s.Count > 0).Select(s => (Stack: s, Rebel: true)))
            .ToList();
        PlaceColumn(attackers, BattleSide.Attacker, 0, hero?.Attack ?? 0, hero?.Defense ?? 0);
        var defenders = defender.Stacks.Where(s => s.Count > 0).Select(s => (Stack: s, Rebel: false))
            .Concat((rebels?.Stacks ?? new List<CreatureStack>()).Where(s => s.Count > 0).Select(s => (Stack: s, Rebel: true)))
            .ToList();
        PlaceColumn(defenders, BattleSide.Defender, Battlefield.Columns - 1, defenderHero?.Attack ?? 0, defenderHero?.Defense ?? 0);
        foreach (var unit in _units)
            _startCounts[unit] = unit.Count;
        SetUpMagic(hero, defenderHero);
        PlaceObstacles();
        StartRound();
    }

    public Battlefield Field { get; } = new();
    public IReadOnlyList<BattleUnit> Units => _units;
    public int Round { get; private set; }
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
    public bool IsOver => Outcome != BattleOutcome.Ongoing;

    /// <summary>La troupe qui doit jouer (null quand le combat est fini).</summary>
    public BattleUnit? Active { get; private set; }

    /// <summary>
    /// La troupe active a frappé et peut repartir (<see cref="Ability.HitAndRun"/>) : le mouvement qui lui reste, sinon
    /// null. Elle ne peut alors que se déplacer (<see cref="Move"/>) ou rester (<see cref="Defend"/>, sans le bonus).
    /// </summary>
    public int? RetreatSteps { get; private set; }

    public bool IsRetreating => RetreatSteps != null;

    /// <summary>La troupe active est enracinée pour ce tour : elle ne peut pas se déplacer (elle peut frapper ses voisins).</summary>
    private bool _activeRooted;

    /// <summary>Ce qui s'est passé depuis le dernier appel (pour l'écran de combat).</summary>
    public List<BattleAction> TakeLog()
    {
        var log = _log.ToList();
        _log.Clear();
        return log;
    }

    public IEnumerable<BattleUnit> Alive(BattleSide side) => _units.Where(u => u.IsAlive && u.Side == side);

    /// <summary>Toutes les troupes vivantes dans l'ordre d'initiative : de la plus rapide à la plus lente (l'ordre d'un round).</summary>
    public IReadOnlyList<BattleUnit> InitiativeOrder =>
        _units.Where(u => u.IsAlive).OrderByDescending(SpeedOf).ThenBy(u => u.Side).ThenBy(u => u.Slot).ToList();

    /// <summary>Qui joue encore dans ce round, dans l'ordre : la troupe active, puis les suivantes, puis celles qui attendent.</summary>
    public IReadOnlyList<BattleUnit> TurnOrder =>
        (Active is { } active ? new[] { active } : Array.Empty<BattleUnit>())
            .Concat(_queue.Where(u => u.IsAlive && u != Active))
            .Concat(_waiting.Where(u => u.IsAlive && u != Active))
            .Distinct().ToList();

    /// <summary>La troupe vivante sur cette case (une grande créature est sur ses deux cases).</summary>
    public BattleUnit? UnitAt(Hex hex) => _units.FirstOrDefault(u => u.IsAlive && u.Occupies(hex));

    /// <summary>La distance entre deux troupes : entre leurs cases les plus proches.</summary>
    public static int Distance(BattleUnit a, BattleUnit b) => Distance(a.Hexes, b.Hexes);

    private static int Distance(IEnumerable<Hex> a, IEnumerable<Hex> b) => a.Min(x => b.Min(x.DistanceTo));

    /// <summary>La troupe tiendrait-elle avec sa tête sur cette case (cases libres, dans le champ, sans autre troupe) ?</summary>
    public bool Fits(BattleUnit unit, Hex head) =>
        unit.HexesAt(head).All(h => Field.IsOpen(h) && (UnitAt(h) is not { } other || other == unit));

    /// <summary>Avec sa tête sur <paramref name="head"/>, la troupe serait-elle à côté de la cible (pour l'attaquer) ?</summary>
    public static bool Touches(BattleUnit unit, Hex head, BattleUnit target) => Distance(unit.HexesAt(head), target.Hexes) == 1;

    // ------------------------------------------------------------------ placement

    private void Place(IReadOnlyList<CreatureStack> stacks, BattleSide side, int column, int attackBonus, int defenseBonus, bool isRebel) =>
        PlaceColumn(stacks.Where(s => s.Count > 0).Select(s => (s, isRebel)).ToList(), side, column, attackBonus, defenseBonus);

    /// <summary>Les troupes d'un camp, réparties de haut en bas sur leur colonne.</summary>
    private void PlaceColumn(List<(CreatureStack Stack, bool Rebel)> stacks, BattleSide side, int column, int attackBonus = 0, int defenseBonus = 0)
    {
        for (int i = 0; i < stacks.Count; i++)
        {
            int row = stacks.Count == 1 ? Battlefield.Rows / 2
                : (int)Math.Round(i * (Battlefield.Rows - 1) / (double)(stacks.Count - 1));
            var (stack, rebel) = stacks[i];
            // une grande créature a son arrière sur la colonne de son camp, sa tête un pas devant
            int col = stack.Type.Abilities.Contains(Ability.TwoHex) ? column + (side == BattleSide.Attacker ? 1 : -1) : column;
            _units.Add(new BattleUnit(_units.Count + 1, stack, side, i, new Hex(col, row),
                rebel ? 0 : attackBonus, rebel ? 0 : defenseBonus, rebel));
        }
    }

    /// <summary>Quelques obstacles entre les deux camps, sans jamais couper le champ en deux.</summary>
    private void PlaceObstacles()
    {
        int count = MinObstacles + Next(MaxObstacles - MinObstacles + 1);
        for (int tries = 0; Field.Obstacles.Count < count && tries < 100; tries++)
        {
            var hex = new Hex(2 + Next(Battlefield.Columns - 4), Next(Battlefield.Rows));
            if (Field.Obstacles.Contains(hex))
                continue;
            Field.Obstacles.Add(hex);
            if (!AllConnected())
                Field.Obstacles.Remove(hex);
        }
    }

    /// <summary>Toutes les cases libres sont-elles reliées entre elles ?</summary>
    private bool AllConnected()
    {
        var open = new List<Hex>();
        for (int r = 0; r < Battlefield.Rows; r++)
        for (int c = 0; c < Battlefield.Columns; c++)
            if (Field.IsOpen(new Hex(c, r)))
                open.Add(new Hex(c, r));
        var seen = new HashSet<Hex> { open[0] };
        var queue = new Queue<Hex>(seen);
        while (queue.Count > 0)
            foreach (var n in queue.Dequeue().Neighbours())
                if (Field.IsOpen(n) && seen.Add(n))
                    queue.Enqueue(n);
        return seen.Count == open.Count;
    }

    // ------------------------------------------------------------------ tours

    private void StartRound()
    {
        Round++;
        _log.Add(new RoundStarted(Round));
        StartRoundMagic();
        foreach (var unit in _units)
        {
            unit.RetaliationsLeft = unit.RetaliationsPerRound;
            unit.HasWaited = false;
        }
        _queue.Clear();
        _waiting.Clear();
        foreach (var unit in _units.Where(u => u.IsAlive)
                     .OrderByDescending(SpeedOf).ThenBy(u => u.Side).ThenBy(u => u.Slot))
            _queue.Enqueue(unit);
        NextUnit();
    }

    /// <summary>Passe à la troupe suivante : d'abord la file du round, puis celles qui ont attendu, puis un nouveau round.</summary>
    private void NextUnit()
    {
        if (CheckEnd())
            return;
        while (true)
        {
            BattleUnit? next = null;
            while (_queue.Count > 0 && next == null)
            {
                var unit = _queue.Dequeue();
                if (unit.IsAlive && unit.IsUnder(SpellEffect.Blind))
                    _log.Add(new UnitSkipped(unit)); // aveuglée : elle ne joue pas
                else if (unit.IsAlive)
                    next = unit;
            }
            if (next == null && _waiting.Count > 0)
            {
                // celles qui ont attendu jouent à la fin du round, dans l'ordre où elles ont attendu
                foreach (var unit in _waiting.Where(u => u.IsAlive))
                    _queue.Enqueue(unit);
                _waiting.Clear();
                continue;
            }
            if (next != null && next.PoisonTurnsLeft > 0)
            {
                var (damage, killed) = next.TakePoison();
                _log.Add(new UnitPoisoned(next, damage, killed));
                if (!next.IsAlive)
                {
                    if (CheckEnd())
                        return;
                    continue; // le poison l'a tuée : la suivante joue
                }
            }
            if (next != null)
            {
                Active = next;
                next.IsDefending = false; // la défense dure jusqu'à son prochain tour
                _activeRooted = next.IsRooted; // enracinée : ce tour-ci, elle ne bouge pas
                next.IsRooted = false;
                _log.Add(new UnitTurn(next));
                if (next.Has(Ability.Regenerates) && next.Regenerate())
                    _log.Add(new UnitRegenerated(next));
                else if (next.Has(Ability.NatureRegeneration) && next.Heal((int)Math.Ceiling(next.Type.HitPoints * NatureRegenerationShare)) > 0)
                    _log.Add(new UnitRegenerated(next));
                if (next.Has(Ability.HealingAura))
                    foreach (var ally in Neighbours(next).Where(u => u.Side == next.Side && u.IsAlive && !u.IsUndead).ToList())
                        if (ally.Heal(HealingAuraPerCreature * next.Count) is > 0 and var healed)
                            _log.Add(new UnitSoothed(next, ally, healed));
                return;
            }
            if (Round >= MaxRounds)
            {
                End(BattleOutcome.DefenderWon);
                return;
            }
            StartRound();
            return;
        }
    }

    private bool CheckEnd()
    {
        if (IsOver)
            return true;
        if (!Alive(BattleSide.Defender).Any())
            End(BattleOutcome.AttackerWon);
        else if (!Alive(BattleSide.Attacker).Any(u => !u.IsRebel))
            End(BattleOutcome.DefenderWon); // les révoltés du défenseur ne gagnent pas pour l'attaquant
        return IsOver;
    }

    private void End(BattleOutcome outcome)
    {
        Outcome = outcome;
        Active = null;
        RetreatSteps = null;
        _log.Add(new BattleEnded(outcome));
    }

    // ------------------------------------------------------------------ ce que peut faire la troupe active

    /// <summary>
    /// La vitesse de la troupe dans ce round : plus rapide au premier round avec <see cref="Ability.QuickStart"/>, plus
    /// rapide avec la hâte, deux fois plus lente avec la lenteur.
    /// </summary>
    public int SpeedOf(BattleUnit unit)
    {
        int speed = unit.Type.Speed + (Round == 1 && unit.Has(Ability.QuickStart) ? QuickStartBonus : 0)
                    + unit.SpellAmount(SpellEffect.Haste);
        return unit.IsUnder(SpellEffect.Slow) ? Math.Max(1, speed / 2) : speed;
    }

    /// <summary>
    /// Les cases où la troupe peut aller ce tour-ci, avec le chemin pour y aller : sa vitesse en pas, sans traverser
    /// obstacles ni troupes ; une troupe qui vole va à vol d'oiseau sur toute case libre à sa portée (le chemin est
    /// alors la seule case d'arrivée). Pour une grande créature, ce sont les cases de sa tête, où ses deux cases tiennent.
    /// </summary>
    public Dictionary<Hex, List<Hex>> Reachable(BattleUnit unit)
    {
        int speed = unit == Active && RetreatSteps is { } steps ? steps : SpeedOf(unit);
        if (unit == Active && _activeRooted)
            return new Dictionary<Hex, List<Hex>> { [unit.Position] = new List<Hex>() };
        if (unit.Has(Ability.Flies) || unit.Has(Ability.Leaps))
        {
            var flights = new Dictionary<Hex, List<Hex>> { [unit.Position] = new List<Hex>() };
            for (int r = 0; r < Battlefield.Rows; r++)
            for (int c = 0; c < Battlefield.Columns; c++)
            {
                var hex = new Hex(c, r);
                if (hex != unit.Position && Fits(unit, hex) && unit.Position.DistanceTo(hex) <= speed)
                    flights[hex] = new List<Hex> { hex };
            }
            return flights;
        }
        var paths = new Dictionary<Hex, List<Hex>> { [unit.Position] = new List<Hex>() };
        var queue = new Queue<Hex>();
        queue.Enqueue(unit.Position);
        while (queue.Count > 0)
        {
            var hex = queue.Dequeue();
            var path = paths[hex];
            if (path.Count >= speed)
                continue;
            foreach (var next in hex.Neighbours())
            {
                if (paths.ContainsKey(next) || !Fits(unit, next))
                    continue;
                paths[next] = new List<Hex>(path) { next };
                queue.Enqueue(next);
            }
        }
        return paths;
    }

    /// <summary>Une troupe ennemie vivante ?</summary>
    public static bool AreEnemies(BattleUnit a, BattleUnit b) => a.Side != b.Side && a.IsAlive && b.IsAlive;

    /// <summary>Un tireur peut tirer s'il a une cible ennemie et qu'aucun ennemi ne se tient à côté de lui.</summary>
    public bool CanShoot(BattleUnit unit, BattleUnit target) =>
        unit.CanShoot && AreEnemies(unit, target) && !IsEngaged(unit) && !(unit == Active && IsRetreating);

    /// <summary>Un ennemi se tient-il à côté de la troupe ?</summary>
    public bool IsEngaged(BattleUnit unit) =>
        unit.Hexes.SelectMany(h => h.Neighbours()).Any(h => UnitAt(h) is { } other && AreEnemies(unit, other));

    /// <summary>
    /// Les cases d'où la troupe peut attaquer cette cible ce tour-ci (où elle peut aller, à côté d'elle) ; pour une
    /// grande créature, la case de sa tête, une de ses deux cases touchant la cible.
    /// </summary>
    public IEnumerable<Hex> AttackPositions(BattleUnit unit, BattleUnit target)
    {
        if (!AreEnemies(unit, target) || unit == Active && IsRetreating)
            return Enumerable.Empty<Hex>();
        var reachable = Reachable(unit);
        return reachable.Keys.Where(h => Touches(unit, h, target));
    }

    // ------------------------------------------------------------------ actions

    /// <summary>La troupe active va sur cette case (son tour est fini). Renvoie false si elle ne peut pas.</summary>
    public bool Move(Hex destination)
    {
        if (Active is not { } unit || destination == unit.Position || !Reachable(unit).TryGetValue(destination, out var path))
            return false;
        MoveAlong(unit, path);
        EndTurn();
        return true;
    }

    /// <summary>
    /// La troupe active va sur <paramref name="from"/> (à côté de la cible) et attaque ; la cible riposte si elle le
    /// peut encore dans ce round. Renvoie false si l'attaque est impossible.
    /// </summary>
    public bool Attack(BattleUnit target, Hex from)
    {
        if (Active is not { } unit || IsRetreating || !AreEnemies(unit, target) || !Touches(unit, from, target))
            return false;
        if (!Reachable(unit).TryGetValue(from, out var path))
            return false;
        var start = unit.Position;
        MoveAlong(unit, path);
        // la charge compte les cases parcourues (à vol d'oiseau pour une troupe qui vole)
        int moved = unit.Has(Ability.Flies) || unit.Has(Ability.Leaps) ? start.DistanceTo(from) : path.Count;
        bool blinded = target.IsUnder(SpellEffect.Blind); // le coup la réveille, mais elle ne riposte pas
        Melee(unit, target, moved);
        if (target.IsAlive && unit.IsAlive && !blinded && target.RetaliationsLeft > 0 && !unit.Has(Ability.NoRetaliation))
        {
            target.RetaliationsLeft--;
            Strike(target, unit, ranged: false, retaliation: true);
        }
        // deux attaques : la seconde après la riposte
        if (unit.Has(Ability.DoubleAttack) && target.IsAlive && unit.IsAlive)
            Melee(unit, target, moved);
        // frappe et repart : la troupe garde le mouvement qui lui reste
        int left = SpeedOf(unit) - moved;
        if (unit.Has(Ability.HitAndRun) && unit.IsAlive && left > 0 && _units.Any(u => AreEnemies(unit, u)))
        {
            RetreatSteps = left;
            _log.Add(new UnitMayRetreat(unit, left));
            return true;
        }
        EndTurn();
        return true;
    }

    /// <summary>
    /// Un coup au corps à corps ; avec le souffle, la troupe juste derrière la cible est touchée aussi (dans la direction
    /// du coup, depuis la case de l'attaquant qui touche la cible, la tête d'abord).
    /// </summary>
    private void Melee(BattleUnit unit, BattleUnit target, int moved)
    {
        var (from, hit) = unit.Hexes.SelectMany(a => target.Hexes.Select(t => (a, t))).First(p => p.a.IsNextTo(p.t));
        var behind = Behind(from, hit);
        Strike(unit, target, ranged: false, retaliation: false, moved);
        if (unit.Has(Ability.Breath) && behind is { } hex && UnitAt(hex) is { } second && second != unit && second != target)
            Strike(unit, second, ranged: false, retaliation: false, moved);
        // le balayage : tous les autres ennemis voisins (seule la cible riposte)
        if (unit.Has(Ability.Sweep))
            foreach (var other in Neighbours(unit).Where(o => o != target && AreEnemies(unit, o)).ToList())
                Strike(unit, other, ranged: false, retaliation: false, moved);
    }

    /// <summary>Les troupes vivantes à côté de celle-ci (sur une case voisine d'une de ses cases).</summary>
    private IEnumerable<BattleUnit> Neighbours(BattleUnit unit) =>
        unit.Hexes.SelectMany(h => h.Neighbours()).Select(UnitAt).OfType<BattleUnit>().Where(u => u != unit).Distinct();

    /// <summary>La case juste derrière <paramref name="target"/>, vue depuis sa voisine <paramref name="from"/> (dans la même direction).</summary>
    public static Hex? Behind(Hex from, Hex target)
    {
        var towardTarget = from.Neighbours().ToList().IndexOf(target);
        if (towardTarget < 0)
            return null;
        var behind = target.Neighbours().ElementAt(towardTarget);
        return Battlefield.InBounds(behind) ? behind : null;
    }

    /// <summary>La troupe active tire sur la cible (pas de riposte). Renvoie false si elle ne peut pas tirer.</summary>
    public bool Shoot(BattleUnit target)
    {
        if (Active is not { } unit || IsRetreating || !CanShoot(unit, target))
            return false;
        var around = unit.Has(Ability.DeathCloud)
            ? Neighbours(target).Where(o => o != unit && !o.IsUndead).ToList()
            : new List<BattleUnit>();
        Strike(unit, target, ranged: true, retaliation: false);
        // deux flèches (Archer elfe)
        if (unit.Has(Ability.DoubleShot) && target.IsAlive && unit.IsAlive)
            Strike(unit, target, ranged: true, retaliation: false);
        // le nuage de mort : les troupes autour de la cible (amies comprises), sauf les morts-vivants
        foreach (var other in around.Where(o => o.IsAlive))
            Strike(unit, other, ranged: true, retaliation: false);
        EndTurn();
        return true;
    }

    /// <summary>La troupe active attend : elle jouera à la fin du round (une seule fois par round).</summary>
    public bool Wait()
    {
        if (Active is not { } unit || unit.HasWaited || IsRetreating)
            return false;
        unit.HasWaited = true;
        if (_activeRooted)
            unit.IsRooted = true; // toujours enracinée quand elle jouera, à la fin du round
        _waiting.Add(unit);
        _log.Add(new UnitWaited(unit));
        Active = null;
        NextUnit();
        return true;
    }

    /// <summary>
    /// La troupe active se défend : +20 % de défense jusqu'à son prochain tour. Une troupe qui a frappé et pourrait
    /// repartir reste simplement sur place (sans le bonus).
    /// </summary>
    public bool Defend()
    {
        if (Active is not { } unit)
            return false;
        if (IsRetreating)
        {
            EndTurn();
            return true;
        }
        _log.Add(new UnitDefended(unit));
        EndTurn();
        unit.IsDefending = true;
        return true;
    }

    /// <summary>
    /// Le héros s'enfuit (pendant le tour d'une de ses troupes) : chaque troupe perd entre 50 et 90 % de ses créatures,
    /// tiré au sort. Le combat s'arrête ; le défenseur garde le terrain.
    /// </summary>
    public bool Flee()
    {
        if (IsOver || Active is not { Side: BattleSide.Attacker })
            return false;
        foreach (var unit in Alive(BattleSide.Attacker).ToList())
        {
            double share = MinFleeLoss + (MaxFleeLoss - MinFleeLoss) * _rng.NextDouble();
            int lost = Math.Min(unit.Count, (int)Math.Ceiling(unit.Count * share));
            unit.Lose(lost);
            _fleeLosses.Add(new CreatureStack(unit.Type, lost));
        }
        _log.Add(new AttackerFled(_fleeLosses.ToList()));
        End(BattleOutcome.AttackerFled);
        return true;
    }

    private void MoveAlong(BattleUnit unit, List<Hex> path)
    {
        if (path.Count == 0)
            return;
        unit.Position = path[^1];
        _log.Add(new UnitMoved(unit, path));
    }

    private void EndTurn()
    {
        Active = null;
        RetreatSteps = null;
        NextUnit();
    }

    /// <summary>Pour les tests : donne le tour à cette troupe, à cette place (sans changer le reste du round).</summary>
    internal void ForceTurn(BattleUnit unit, Hex? position = null)
    {
        if (position is { } hex)
            unit.Position = hex;
        Active = unit;
    }

    // ------------------------------------------------------------------ dégâts

    /// <summary>Les dégâts d'un coup, tirés au sort, et les créatures tuées. <paramref name="moved"/> : cases parcourues (charge).</summary>
    private void Strike(BattleUnit from, BattleUnit to, bool ranged, bool retaliation, int moved = 0)
    {
        int damage = RollDamage(from, to, ranged, retaliation ? 0 : moved);
        int lost = Math.Min(damage, to.TotalHitPoints); // les points de vie vraiment ôtés
        int killed = to.TakeDamage(damage);
        to.Wake();
        _log.Add(new UnitStruck(from, to, damage, killed, ranged, retaliation));
        if (to.Has(Ability.Rage) && to.Enrage(RagePerHit, RageMax))
            _log.Add(new UnitEnraged(to, to.Rage));
        if (!from.IsAlive)
            return;
        // la foudre : une chance à chaque attaque (pas à la riposte), sans compter la défense
        if (from.Has(Ability.Lightning) && !retaliation && to.IsAlive && _rng.NextDouble() < LightningChance)
        {
            int bolt = LightningPerCreature * from.Count;
            _log.Add(new LightningStruck(from, to, bolt, to.TakeDamage(bolt)));
        }
        if (from.Has(Ability.GhostGrowth) && killed > 0)
        {
            from.Grow(killed);
            _log.Add(new UnitGrew(from, killed));
            CheckRevolt(from);
        }
        if (from.Has(Ability.LifeDrain) && lost > 0)
        {
            var (hitPoints, revived) = from.Revive(lost, _startCounts[from]);
            if (hitPoints > 0)
                _log.Add(new UnitDrained(from, hitPoints, revived));
        }
        if (from.Has(Ability.Roots) && !ranged && to.IsAlive && !to.IsRooted)
        {
            to.IsRooted = true;
            _log.Add(new UnitRooted(to));
        }
        if (from.Has(Ability.Poison) && to.IsAlive)
            to.Poison(PoisonPerCreature * from.Count, PoisonTurns);
    }

    /// <summary>
    /// Dégâts d'une troupe sur une autre : chaque créature tire ses dégâts entre min et max (au-delà de 10 créatures,
    /// on tire pour 10 et on multiplie), puis la règle attaque / défense de Heroes 3. Un tir lointain (avec pénalité)
    /// ou un tireur qui se bat au corps à corps fait moitié moins de dégâts.
    /// </summary>
    private int RollDamage(BattleUnit from, BattleUnit to, bool ranged, int moved)
    {
        int rolls = Math.Min(from.Count, 10);
        int sum = 0;
        for (int i = 0; i < rolls; i++)
            sum += from.Type.MinDamage + Next(from.Type.MaxDamage - from.Type.MinDamage + 1);
        double damage = from.Count <= 10 ? sum : sum * (double)from.Count / 10;
        // un coup écrasant ignore une partie de la défense
        double ignored = ranged ? 0 : from.Has(Ability.Rending) ? RendingShare : from.Has(Ability.Crushing) ? CrushingShare : 0;
        int defense = (int)Math.Round(to.Defense * (1 - ignored));
        damage *= Battle.DamageModifier(from.Attack, defense);
        int range = from.Has(Ability.ShortRange) ? ShortRange : LongRange;
        if (ranged && from.HasRangePenalty && Distance(from, to) > range)
            damage *= 0.5;
        if (ranged && to.Has(Ability.RangedResistance))
            damage *= 0.5;
        // les sorts : bénédiction et malédiction (en pourcentage), bouclier contre les tirs
        damage *= 1 + (from.SpellAmount(SpellEffect.Bless) - from.SpellAmount(SpellEffect.Curse)) / 100.0;
        if (ranged)
            damage *= 1 - to.SpellAmount(SpellEffect.ArrowShield) / 100.0;
        if (!ranged && from.CanShoot)
            damage *= 0.5;
        if (!ranged && from.Has(Ability.Charge))
            damage *= 1 + ChargeBonus * moved;
        return Math.Max(1, (int)Math.Round(damage));
    }

    /// <summary>
    /// Une troupe qui grossit pendant le combat (Fantôme) peut dépasser le charisme de son héros : elle peut alors se
    /// révolter aussitôt, comme au début d'un combat (<see cref="Command.RevoltChance"/>), et passer dans l'autre camp.
    /// </summary>
    private void CheckRevolt(BattleUnit unit)
    {
        if (unit.IsRebel || !unit.IsAlive || HeroOf(unit.Side) is not { } hero)
            return;
        double chance = Command.RevoltChance(unit.Type, unit.Count, hero.Charisma);
        if (chance <= 0 || _rng.NextDouble() >= chance)
            return;
        _turned[unit] = (unit.Side, unit.Count);
        unit.Turn();
        _log.Add(new UnitRevolted(unit));
    }

    /// <summary>Un nombre entier entre 0 (compris) et <paramref name="max"/> (exclu).</summary>
    private int Next(int max) => max <= 1 ? 0 : (int)(_rng.NextDouble() * max);

    // ------------------------------------------------------------------ bilan

    /// <summary>
    /// Le bilan du combat (une fois fini) : vainqueur, pertes de chaque camp, troupes révoltées. Les troupes vides sont
    /// retirées des armées. Après une fuite, le héros ne gagne pas d'expérience.
    /// </summary>
    public BattleReport Report()
    {
        // une troupe révoltée pendant le combat compte pour son ancien camp ce qu'elle a perdu avant de se révolter
        int Lost(BattleUnit u, BattleSide side) => _turned.TryGetValue(u, out var before)
            ? (before.Side == side ? Math.Max(0, _startCounts[u] - before.Count) : 0)
            : (u.Side == side && !u.IsRebel ? Math.Max(0, _startCounts[u] - u.Count) : 0);
        List<CreatureStack> Losses(BattleSide side) => _units
            .Where(u => Lost(u, side) > 0)
            .GroupBy(u => u.Type)
            .Select(g => new CreatureStack(g.Key, g.Sum(u => Lost(u, side))))
            .ToList();
        var report = new BattleReport
        {
            AttackerWon = Outcome == BattleOutcome.AttackerWon,
            Fled = Outcome == BattleOutcome.AttackerFled,
            AttackerLosses = Losses(BattleSide.Attacker),
            DefenderLosses = Losses(BattleSide.Defender),
            Rounds = Round,
            Rebels = _rebelsBefore.ToList(),
            DefenderRebels = _defenderRebelsBefore.ToList(),
            AttackerSpellPointsSpent = _spellPointsSpent[(int)BattleSide.Attacker],
            DefenderSpellPointsSpent = _spellPointsSpent[(int)BattleSide.Defender],
        };
        // les révoltés pendant le combat quittent l'armée de leur héros
        foreach (var (unit, before) in _turned)
        {
            var army = before.Side == BattleSide.Attacker ? _attacker : _defender;
            int index = army.Stacks.ToList().IndexOf(unit.Stack);
            if (index < 0)
                continue;
            army.MoveStackTo(index, new Army());
            (before.Side == BattleSide.Attacker ? report.Rebels : report.DefenderRebels).Add(new CreatureStack(unit.Type, before.Count));
            if (unit.Count > 0)
                (before.Side == BattleSide.Attacker ? report.RebelSurvivors : report.DefenderRebelSurvivors).Add(new CreatureStack(unit.Type, unit.Count));
        }
        _attacker.RemoveEmpty();
        _defender.RemoveEmpty();
        _rebels?.RemoveEmpty();
        _defenderRebels?.RemoveEmpty();
        report.DefenderRebelSurvivors.AddRange(_defenderRebels?.Stacks.Where(s => s.Count > 0).Select(s => new CreatureStack(s.Type, s.Count))
                                               ?? Enumerable.Empty<CreatureStack>());
        report.RebelSurvivors.AddRange(_rebels?.Stacks.Where(s => s.Count > 0).Select(s => new CreatureStack(s.Type, s.Count))
                                       ?? Enumerable.Empty<CreatureStack>());
        return report;
    }
}
