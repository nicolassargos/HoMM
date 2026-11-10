using System;
using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Le combat tactique (étape 6) : champ de bataille, tours, déplacements, coups, tirs, fuite, bilan.</summary>
public class CombatTactiqueTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    /// <summary>
    /// Une armée. Une armée réunit les créatures d'un même type en une troupe : « paysan-bis » est une copie du Paysan,
    /// pour avoir deux troupes de Paysans séparées.
    /// </summary>
    private static Army ArmyOf(params (string Id, int Count)[] stacks)
    {
        var army = new Army();
        foreach (var (id, count) in stacks)
            army.Add(id == "paysan-bis" ? SecondPeasant : Creature(id), count);
        return army;
    }

    private static readonly CreatureType SecondPeasant = TestData.Creatures.Get("paysan") with { Id = "paysan-bis" };

    /// <summary>Un combat sans obstacles (pour placer les troupes à la main).</summary>
    private static TacticalBattle Battle(Army attacker, Army defender, int seed = 1, Hero? hero = null, Army? rebels = null)
    {
        var battle = new TacticalBattle(attacker, defender, new GameRandom(seed), hero, rebels);
        battle.Field.Obstacles.Clear();
        return battle;
    }

    private static BattleUnit UnitOf(TacticalBattle battle, BattleSide side, int slot = 0) =>
        battle.Units.Single(u => u.Side == side && u.Slot == slot && !u.IsRebel);

    // ------------------------------------------------------------------ cases

    [Fact]
    public void Chaque_case_a_six_voisines_a_un_pas()
    {
        foreach (var hex in new[] { new Hex(4, 4), new Hex(4, 3), new Hex(0, 0) })
        {
            var neighbours = hex.Neighbours().ToList();
            Assert.Equal(6, neighbours.Distinct().Count());
            Assert.All(neighbours, n => Assert.Equal(1, hex.DistanceTo(n)));
        }
    }

    [Fact]
    public void Les_distances_suivent_la_grille_hexagonale()
    {
        Assert.Equal(0, new Hex(3, 3).DistanceTo(new Hex(3, 3)));
        Assert.Equal(10, new Hex(0, 4).DistanceTo(new Hex(10, 4)));
        // en diagonale, on avance d'une demi-case par rangée
        Assert.Equal(4, new Hex(0, 0).DistanceTo(new Hex(2, 4)));
        Assert.Equal(new Hex(5, 2).DistanceTo(new Hex(1, 7)), new Hex(1, 7).DistanceTo(new Hex(5, 2)));
    }

    // ------------------------------------------------------------------ mise en place

    [Fact]
    public void Les_armees_commencent_de_chaque_cote()
    {
        var battle = new TacticalBattle(ArmyOf(("paysan", 30), ("archer", 15), ("griffon", 4)), ArmyOf(("loup", 10)), new GameRandom(7));

        Assert.All(battle.Alive(BattleSide.Attacker), u => Assert.Equal(0, u.Position.Col));
        Assert.All(battle.Alive(BattleSide.Defender), u => Assert.Equal(Battlefield.Columns - 1, u.Position.Col));
        Assert.Equal(3, battle.Alive(BattleSide.Attacker).Select(u => u.Position).Distinct().Count());
        // quelques obstacles au milieu, jamais sur une troupe
        Assert.InRange(battle.Field.Obstacles.Count, TacticalBattle.MinObstacles, TacticalBattle.MaxObstacles);
        Assert.All(battle.Field.Obstacles, o => Assert.InRange(o.Col, 2, Battlefield.Columns - 3));
        Assert.All(battle.Units, u => Assert.DoesNotContain(u.Position, battle.Field.Obstacles));
    }

    [Fact]
    public void Les_plus_rapides_jouent_d_abord()
    {
        // l'Ange (vitesse 12) du défenseur joue avant les Paysans (vitesse 4) de l'attaquant
        var battle = Battle(ArmyOf(("paysan", 10)), ArmyOf(("ange", 1)));

        Assert.Equal("ange", battle.Active!.Type.Id);
        Assert.Equal(1, battle.Round);
    }

    [Fact]
    public void A_vitesse_egale_l_attaquant_joue_d_abord()
    {
        var battle = Battle(ArmyOf(("archer", 5)), ArmyOf(("paysan", 5)));

        Assert.Equal(BattleSide.Attacker, battle.Active!.Side);
    }

    // ------------------------------------------------------------------ déplacement

    [Fact]
    public void Une_troupe_avance_au_plus_de_sa_vitesse()
    {
        var battle = Battle(ArmyOf(("paysan", 10)), ArmyOf(("paysan", 10)));
        var unit = UnitOf(battle, BattleSide.Attacker);
        int speed = unit.Type.Speed;

        Assert.False(battle.Move(new Hex(speed + 1, unit.Position.Row)));
        Assert.True(battle.Move(new Hex(speed, unit.Position.Row)));
        Assert.Equal(new Hex(speed, 4), unit.Position);
        Assert.NotEqual(unit, battle.Active); // son tour est fini
    }

    [Fact]
    public void Les_obstacles_et_les_troupes_bloquent_le_passage()
    {
        var battle = Battle(ArmyOf(("paysan", 10)), ArmyOf(("paysan", 10)));
        var unit = UnitOf(battle, BattleSide.Attacker);
        battle.Field.Obstacles.Add(new Hex(1, 4));

        Assert.False(battle.Move(new Hex(1, 4)));
        Assert.DoesNotContain(new Hex(1, 4), battle.Reachable(unit).Keys);
    }

    // ------------------------------------------------------------------ coups

    [Fact]
    public void Une_attaque_tue_et_la_cible_riposte_une_fois_par_round()
    {
        // Paysans contre Paysans : 1 de dégâts, 1 point de vie, attaque = défense : chaque Paysan en tue un
        var battle = Battle(ArmyOf(("paysan", 10), ("paysan-bis", 10)), ArmyOf(("paysan", 6)));
        var first = battle.Units.First(u => u.Side == BattleSide.Attacker);
        var second = battle.Units.Last(u => u.Side == BattleSide.Attacker);
        var target = UnitOf(battle, BattleSide.Defender);
        target.Position = new Hex(5, 4);
        battle.ForceTurn(first, new Hex(3, 4));

        Assert.True(battle.Attack(target, new Hex(4, 4)));
        Assert.Equal(0, target.Count); // 10 coups de 1 sur 6 Paysans
        Assert.Equal(10, first.Count); // pas de riposte : la cible est morte

        // une autre cible, attaquée deux fois : elle ne riposte qu'à la première
        var army = ArmyOf(("paysan", 30));
        battle = Battle(ArmyOf(("paysan", 4), ("paysan-bis", 4)), army);
        first = battle.Units.First(u => u.Side == BattleSide.Attacker);
        second = battle.Units.Last(u => u.Side == BattleSide.Attacker);
        target = UnitOf(battle, BattleSide.Defender);
        target.Position = new Hex(5, 4);
        battle.ForceTurn(first, new Hex(4, 4));
        battle.Attack(target, new Hex(4, 4));
        Assert.Equal(26, target.Count);
        Assert.Equal(0, first.Count); // 26 survivants ripostent : 26 de dégâts sur 4 Paysans
        battle.ForceTurn(second, new Hex(6, 4));
        battle.Attack(target, new Hex(6, 4));
        Assert.Equal(22, target.Count);
        Assert.Equal(4, second.Count); // plus de riposte dans ce round
    }

    [Fact]
    public void Les_degats_sont_tires_entre_le_minimum_et_le_maximum()
    {
        // 3 Ogres contre des Épéistes : entre 3 fois leurs dégâts minimum et 3 fois leur maximum, avant la règle attaque /
        // défense (leur coup écrasant ignore 40 % de la défense)
        var ogre = Creature("ogre");
        int defense = (int)Math.Round(Creature("epeiste").Defense * (1 - TacticalBattle.CrushingShare));
        double modifier = HoMM.Core.Battle.DamageModifier(Creature("ogre").Attack, defense);
        var seen = new HashSet<int>();
        for (int seed = 1; seed <= 40; seed++)
        {
            var battle = Battle(ArmyOf(("ogre", 3)), ArmyOf(("epeiste", 10)), seed);
            var ogres = UnitOf(battle, BattleSide.Attacker);
            var target = UnitOf(battle, BattleSide.Defender);
            target.Position = new Hex(5, 4);
            battle.ForceTurn(ogres, new Hex(4, 4));
            battle.Attack(target, new Hex(4, 4));
            int damage = battle.TakeLog().OfType<UnitStruck>().First(s => !s.Retaliation).Damage;
            Assert.InRange(damage, (int)Math.Floor(3 * ogre.MinDamage * modifier), (int)Math.Ceiling(3 * ogre.MaxDamage * modifier));
            seen.Add(damage);
        }
        Assert.True(seen.Count > 3, "les dégâts devraient varier d'un combat à l'autre");
    }

    // ------------------------------------------------------------------ tirs

    [Fact]
    public void Un_tir_ne_provoque_pas_de_riposte()
    {
        var battle = Battle(ArmyOf(("archer", 10)), ArmyOf(("paysan", 50)));
        var archers = UnitOf(battle, BattleSide.Attacker);
        var target = UnitOf(battle, BattleSide.Defender);
        target.Position = new Hex(4, 4);
        battle.ForceTurn(archers, new Hex(0, 4));

        Assert.True(battle.Shoot(target));

        Assert.True(target.Count < 50);
        Assert.Equal(10, archers.Count);
    }

    [Fact]
    public void Un_tireur_ne_tire_pas_avec_un_ennemi_a_cote()
    {
        var battle = Battle(ArmyOf(("archer", 10)), ArmyOf(("paysan", 5), ("paysan-bis", 5)));
        var archers = UnitOf(battle, BattleSide.Attacker);
        var near = UnitOf(battle, BattleSide.Defender, 0);
        var far = UnitOf(battle, BattleSide.Defender, 1);
        near.Position = new Hex(1, 4);
        far.Position = new Hex(8, 4);
        battle.ForceTurn(archers, new Hex(0, 4));

        Assert.False(battle.Shoot(far));
        Assert.True(battle.IsEngaged(archers));
    }

    /// <summary>Dégâts d'un tir de 10 tireurs sur une troupe placée à <paramref name="column"/> (le tireur est en colonne 0).</summary>
    private static int ShotDamage(CreatureType shooter, int column)
    {
        var attacker = new Army();
        attacker.Add(shooter, 10);
        var battle = Battle(attacker, ArmyOf(("epeiste", 50)));
        var unit = UnitOf(battle, BattleSide.Attacker);
        var target = UnitOf(battle, BattleSide.Defender);
        target.Position = new Hex(column, 4);
        battle.ForceTurn(unit, new Hex(0, 4));
        battle.Shoot(target);
        return battle.TakeLog().OfType<UnitStruck>().Single().Damage;
    }

    [Fact]
    public void Un_tir_lointain_fait_moitie_moins_de_degats()
    {
        // Archers : 2 de dégâts, sans hasard
        int near = ShotDamage(Creature("archer"), TacticalBattle.LongRange);
        int far = ShotDamage(Creature("archer"), TacticalBattle.LongRange + 1);

        Assert.InRange(far, near / 2 - 1, near / 2 + 1);
    }

    [Fact]
    public void Un_tir_lointain_se_reconnait_avant_de_tirer()
    {
        // l'écran montre une flèche brisée pour ces tirs : la règle doit dire la même chose que les dégâts
        bool LongShot(CreatureType shooter, int column)
        {
            var attacker = new Army();
            attacker.Add(shooter, 10);
            var battle = Battle(attacker, ArmyOf(("epeiste", 50)));
            var unit = UnitOf(battle, BattleSide.Attacker);
            var target = UnitOf(battle, BattleSide.Defender);
            target.Position = new Hex(column, 4);
            unit.Position = new Hex(0, 4);
            return battle.IsLongShot(unit, target);
        }
        var archer = Creature("archer");
        var druid = archer with { Id = "druide-essai", Abilities = new List<string> { Ability.Shoots, Ability.NoRangePenalty } };

        Assert.False(LongShot(archer, TacticalBattle.LongRange));
        Assert.True(LongShot(archer, TacticalBattle.LongRange + 1));
        Assert.False(LongShot(druid, Battlefield.Columns - 1));
    }

    [Fact]
    public void Un_tireur_sans_penalite_tire_aussi_fort_de_loin()
    {
        var elf = Creature("archer") with { Id = "elfe", Abilities = new List<string> { Ability.Shoots, Ability.NoRangePenalty } };

        Assert.Equal(ShotDamage(elf, TacticalBattle.LongRange), ShotDamage(elf, Battlefield.Columns - 1));
    }

    [Fact]
    public void Un_tireur_au_corps_a_corps_fait_moitie_moins_de_degats()
    {
        var swordsmen = Creature("archer") with { Id = "archer-sans-arc", Abilities = new List<string>() };

        int Melee(CreatureType type)
        {
            var attacker = new Army();
            attacker.Add(type, 10);
            var battle = Battle(attacker, ArmyOf(("epeiste", 50)));
            var unit = UnitOf(battle, BattleSide.Attacker);
            var target = UnitOf(battle, BattleSide.Defender);
            target.Position = new Hex(5, 4);
            battle.ForceTurn(unit, new Hex(4, 4));
            battle.Attack(target, new Hex(4, 4));
            return battle.TakeLog().OfType<UnitStruck>().First(s => !s.Retaliation).Damage;
        }

        int archer = Melee(Creature("archer")), plain = Melee(swordsmen);
        Assert.InRange(archer, plain / 2 - 1, plain / 2 + 1);
    }

    // ------------------------------------------------------------------ attendre, se défendre

    [Fact]
    public void Une_troupe_qui_attend_joue_a_la_fin_du_round()
    {
        var battle = Battle(ArmyOf(("griffon", 4), ("paysan", 10)), ArmyOf(("paysan", 10)));
        var griffons = battle.Active!;
        Assert.Equal("griffon", griffons.Type.Id);

        Assert.True(battle.Wait());
        Assert.NotEqual(griffons, battle.Active);
        Assert.False(griffons.HasWaited == false);

        // les autres jouent, puis les Griffons, toujours dans le même round
        while (battle.Active != griffons)
            Assert.True(battle.Defend());
        Assert.Equal(1, battle.Round);
        Assert.False(battle.Wait()); // une seule attente par round
    }

    [Fact]
    public void Se_defendre_augmente_la_defense_jusqu_au_prochain_tour()
    {
        var battle = Battle(ArmyOf(("epeiste", 5)), ArmyOf(("paysan", 5)));
        var unit = UnitOf(battle, BattleSide.Attacker);
        int defense = unit.Defense;
        battle.ForceTurn(unit);

        battle.Defend();

        Assert.Equal((int)Math.Ceiling(defense * 1.2), unit.Defense);
    }

    // ------------------------------------------------------------------ fin du combat

    [Fact]
    public void Le_combat_finit_quand_un_camp_n_a_plus_de_troupes()
    {
        var attacker = ArmyOf(("paysan", 20));
        var defender = ArmyOf(("paysan", 5));
        var battle = Battle(attacker, defender);
        var unit = UnitOf(battle, BattleSide.Attacker);
        var target = UnitOf(battle, BattleSide.Defender);
        target.Position = new Hex(5, 4);
        battle.ForceTurn(unit, new Hex(4, 4));

        battle.Attack(target, new Hex(4, 4));

        Assert.Equal(BattleOutcome.AttackerWon, battle.Outcome);
        Assert.Null(battle.Active);
        var report = battle.Report();
        Assert.True(report.AttackerWon);
        Assert.Equal(5, report.DefenderLosses.Single().Count);
        Assert.True(defender.IsEmpty);
        Assert.Equal(5 * Creature("paysan").Cost[ResourceType.Gold], report.Experience);
    }

    [Fact]
    public void Fuir_coute_une_grande_part_de_chaque_troupe()
    {
        var attacker = ArmyOf(("paysan", 100), ("archer", 20));
        var battle = Battle(attacker, ArmyOf(("dragon", 2)));
        battle.ForceTurn(UnitOf(battle, BattleSide.Attacker));

        Assert.True(battle.Flee());

        Assert.Equal(BattleOutcome.AttackerFled, battle.Outcome);
        var report = battle.Report();
        Assert.True(report.Fled);
        Assert.False(report.AttackerWon);
        Assert.Equal(0, report.Experience);
        Assert.InRange(report.AttackerLosses.Single(l => l.Type.Id == "paysan").Count, 50, 90);
        Assert.InRange(report.AttackerLosses.Single(l => l.Type.Id == "archer").Count, 10, 18);
        Assert.InRange(attacker.TotalCount, 120 - 108, 120 - 60);
    }

    [Fact]
    public void Seul_le_heros_peut_fuir_pendant_le_tour_d_une_de_ses_troupes()
    {
        var battle = Battle(ArmyOf(("paysan", 10)), ArmyOf(("ange", 1)));
        Assert.Equal(BattleSide.Defender, battle.Active!.Side);

        Assert.False(battle.Flee());
        Assert.Equal(BattleOutcome.Ongoing, battle.Outcome);
    }

    [Fact]
    public void Les_revoltes_combattent_avec_le_defenseur_sans_compter_dans_les_pertes()
    {
        var rebels = ArmyOf(("paysan", 8));
        var battle = Battle(ArmyOf(("archer", 10)), ArmyOf(("loup", 3)), rebels: rebels);

        var rebel = battle.Units.Single(u => u.IsRebel);
        Assert.Equal(BattleSide.Defender, rebel.Side);
        Assert.Equal(Battlefield.Columns - 1, rebel.Position.Col);
        Assert.Equal(0, rebel.AttackBonus);
    }

    /// <summary>Joue tout un combat : chaque troupe attaque l'ennemi le plus proche, ou s'en approche, ou tire.</summary>
    private static BattleReport PlayOut(TacticalBattle battle)
    {
        for (int turn = 0; !battle.IsOver && turn < 2000; turn++)
        {
            var unit = battle.Active!;
            var target = battle.Units.Where(u => TacticalBattle.AreEnemies(unit, u))
                .OrderBy(u => u.Position.DistanceTo(unit.Position)).ThenBy(u => u.Id).First();
            if (battle.CanShoot(unit, target))
            {
                battle.Shoot(target);
                continue;
            }
            var from = battle.AttackPositions(unit, target).OrderBy(h => h.DistanceTo(unit.Position)).ThenBy(h => h.Col).ThenBy(h => h.Row)
                .Cast<Hex?>().FirstOrDefault();
            if (from is { } hex)
            {
                battle.Attack(target, hex);
                continue;
            }
            var closer = battle.Reachable(unit).Keys.OrderBy(h => h.DistanceTo(target.Position)).ThenBy(h => h.Col).ThenBy(h => h.Row).First();
            if (closer == unit.Position || !battle.Move(closer))
                battle.Defend();
        }
        return battle.Report();
    }

    [Fact]
    public void Meme_graine_meme_combat()
    {
        BattleReport Play() => PlayOut(new TacticalBattle(ArmyOf(("paysan", 30), ("archer", 15), ("griffon", 4)),
            ArmyOf(("loup", 12), ("orc", 6)), new GameRandom(123)));

        var first = Play();
        var second = Play();

        Assert.Equal(first.AttackerWon, second.AttackerWon);
        Assert.Equal(first.Rounds, second.Rounds);
        Assert.Equal(first.AttackerLosses.Sum(l => l.Count), second.AttackerLosses.Sum(l => l.Count));
        Assert.Equal(first.DefenderLosses.Sum(l => l.Count), second.DefenderLosses.Sum(l => l.Count));
    }

    [Fact]
    public void Une_armee_bien_plus_forte_gagne()
    {
        var report = PlayOut(new TacticalBattle(ArmyOf(("paysan", 30), ("archer", 15), ("griffon", 4)),
            ArmyOf(("gobelin", 8)), new GameRandom(5)));

        Assert.True(report.AttackerWon);
        Assert.InRange(report.Rounds, 1, 10);
    }
}
