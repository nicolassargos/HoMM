using System;
using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les capacités des créatures au combat tactique (étape 6), une par une.</summary>
public class CapacitesTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    /// <summary>Une copie de la créature, avec des dégâts fixes et d'autres capacités (pour comparer sans hasard).</summary>
    private static CreatureType Fixed(string id, int damage, params string[] abilities) =>
        Creature(id) with { Id = id + "-test", MinDamage = damage, MaxDamage = damage, Abilities = abilities.ToList() };

    private static Army ArmyOf(CreatureType type, int count)
    {
        var army = new Army();
        army.Add(type, count);
        return army;
    }

    private static (TacticalBattle Battle, BattleUnit Attacker, BattleUnit Defender) Duel(CreatureType attacker, int attackers,
        CreatureType defender, int defenders, Hex? attackerAt = null, Hex? defenderAt = null)
    {
        var battle = new TacticalBattle(ArmyOf(attacker, attackers), ArmyOf(defender, defenders), new GameRandom(9));
        battle.Field.Obstacles.Clear();
        var a = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var d = battle.Units.Single(u => u.Side == BattleSide.Defender);
        if (defenderAt is { } hex)
            d.Position = hex;
        battle.ForceTurn(a, attackerAt);
        battle.TakeLog();
        return (battle, a, d);
    }

    private static List<UnitStruck> Strikes(TacticalBattle battle) => battle.TakeLog().OfType<UnitStruck>().ToList();

    [Fact]
    public void Une_troupe_qui_vole_passe_par_dessus_obstacles_et_troupes()
    {
        var (battle, griffons, _) = Duel(Creature("griffon"), 5, Creature("paysan"), 10, new Hex(0, 4));
        // un mur d'obstacles devant les Griffons
        for (int row = 0; row < Battlefield.Rows; row++)
            battle.Field.Obstacles.Add(new Hex(2, row));

        var reachable = battle.Reachable(griffons);

        Assert.Contains(new Hex(4, 4), reachable.Keys);
        Assert.Equal(new List<Hex> { new(4, 4) }, reachable[new Hex(4, 4)]); // à vol d'oiseau
        Assert.DoesNotContain(reachable.Keys, h => h.DistanceTo(griffons.Position) > griffons.Type.Speed);
        Assert.DoesNotContain(new Hex(2, 4), reachable.Keys); // on ne se pose pas sur un obstacle
    }

    [Fact]
    public void Le_griffon_riposte_deux_fois_par_round()
    {
        var battle = new TacticalBattle(ArmyOf(Creature("paysan"), 5), ArmyOf(Creature("griffon"), 20), new GameRandom(1));
        battle.Field.Obstacles.Clear();
        var peasants = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var griffons = battle.Units.Single(u => u.Side == BattleSide.Defender);
        griffons.Position = new Hex(5, 4);

        battle.ForceTurn(peasants, new Hex(4, 4));
        battle.Attack(griffons, new Hex(4, 4));
        Assert.Equal(1, griffons.RetaliationsLeft); // il lui en reste une

        Assert.Equal(2, griffons.RetaliationsPerRound);
    }

    [Fact]
    public void L_ange_frappe_sans_riposte()
    {
        var (battle, angel, target) = Duel(Creature("ange"), 1, Creature("epeiste"), 100, new Hex(4, 4), new Hex(5, 4));

        battle.Attack(target, new Hex(4, 4));

        Assert.DoesNotContain(Strikes(battle), s => s.Retaliation);
        Assert.Equal(1, angel.Count);
    }

    [Fact]
    public void Le_souffle_du_dragon_touche_aussi_la_troupe_derriere()
    {
        var defender = new Army();
        defender.Add(Creature("paysan"), 50);
        defender.Add(Creature("squelette"), 50);
        var battle = new TacticalBattle(ArmyOf(Creature("dragon"), 2), defender, new GameRandom(4));
        battle.Field.Obstacles.Clear();
        var dragons = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var front = battle.Units.Single(u => u.Type.Id == "paysan");
        var back = battle.Units.Single(u => u.Type.Id == "squelette");
        front.Position = new Hex(5, 4);
        back.Position = new Hex(6, 4); // juste derrière, dans la direction du coup
        battle.ForceTurn(dragons, new Hex(4, 4));
        battle.TakeLog();

        battle.Attack(front, new Hex(4, 4));

        var strikes = Strikes(battle).Where(s => !s.Retaliation).ToList();
        Assert.Contains(strikes, s => s.Target == front);
        Assert.Contains(strikes, s => s.Target == back);
    }

    [Fact]
    public void La_case_derriere_suit_la_direction_du_coup()
    {
        Assert.Equal(new Hex(6, 4), TacticalBattle.Behind(new Hex(4, 4), new Hex(5, 4)));
        Assert.Equal(new Hex(2, 4), TacticalBattle.Behind(new Hex(4, 4), new Hex(3, 4)));
        // en diagonale : deux rangées plus bas, une colonne plus loin
        Assert.Equal(new Hex(5, 6), TacticalBattle.Behind(new Hex(4, 4), new Hex(4, 5)));
    }

    /// <summary>Dégâts d'un tir de 10 Archers (2 de dégâts) sur une cible à 2 cases.</summary>
    private static int ShotOn(CreatureType target)
    {
        var (battle, _, d) = Duel(Creature("archer"), 10, target, 50, new Hex(0, 4), new Hex(2, 4));
        battle.Shoot(d);
        return Strikes(battle).Single().Damage;
    }

    [Fact]
    public void Le_bouclier_divise_par_deux_les_degats_des_tirs()
    {
        var shielded = Creature("epeiste");
        var plain = shielded with { Id = "epeiste-sans-bouclier", Abilities = new List<string>() };

        Assert.InRange(ShotOn(shielded), ShotOn(plain) / 2 - 1, ShotOn(plain) / 2 + 1);
    }

    [Fact]
    public void La_charge_frappe_plus_fort_de_loin()
    {
        var knight = Fixed("cavalier", 20, Ability.Charge);

        int Hit(Hex from, Hex start)
        {
            var (battle, _, d) = Duel(knight, 5, Fixed("epeiste", 1), 100, start, new Hex(8, 4));
            battle.Attack(d, from);
            return Strikes(battle).First(s => !s.Retaliation).Damage;
        }

        int still = Hit(new Hex(7, 4), new Hex(7, 4));
        int charged = Hit(new Hex(7, 4), new Hex(2, 4)); // 5 cases parcourues

        Assert.InRange(charged, (int)(still * 1.25) - 1, (int)(still * 1.25) + 1);
    }

    [Fact]
    public void Le_gobelin_est_plus_rapide_au_premier_round()
    {
        var (battle, goblins, _) = Duel(Creature("gobelin"), 10, Creature("paysan"), 10);

        Assert.Equal(1, battle.Round);
        Assert.Equal(Creature("gobelin").Speed + TacticalBattle.QuickStartBonus, battle.SpeedOf(goblins));
    }

    [Fact]
    public void Le_loup_frappe_deux_fois()
    {
        var (battle, _, target) = Duel(Creature("loup"), 20, Creature("epeiste"), 5, new Hex(4, 4), new Hex(5, 4));

        battle.Attack(target, new Hex(4, 4));

        var strikes = Strikes(battle);
        Assert.Equal(2, strikes.Count(s => !s.Retaliation));
        Assert.Equal(1, strikes.Count(s => s.Retaliation)); // une seule riposte
    }

    [Fact]
    public void Les_haches_de_l_orc_portent_moins_loin()
    {
        var orc = Fixed("orc", 4, Ability.Shoots, Ability.ShortRange);

        int Shot(int column)
        {
            var (battle, _, d) = Duel(orc, 10, Fixed("paysan", 1), 500, new Hex(0, 4), new Hex(column, 4));
            battle.Shoot(d);
            return Strikes(battle).Single().Damage;
        }

        Assert.InRange(Shot(TacticalBattle.ShortRange + 1), Shot(TacticalBattle.ShortRange) / 2 - 1, Shot(TacticalBattle.ShortRange) / 2 + 1);
    }

    [Fact]
    public void Le_coup_ecrasant_de_l_ogre_ignore_une_partie_de_la_defense()
    {
        int Hit(CreatureType ogre)
        {
            var (battle, _, d) = Duel(ogre, 5, Fixed("epeiste", 1), 100, new Hex(4, 4), new Hex(5, 4));
            battle.Attack(d, new Hex(4, 4));
            return Strikes(battle).First(s => !s.Retaliation).Damage;
        }

        Assert.True(Hit(Fixed("ogre", 10, Ability.Crushing)) > Hit(Fixed("ogre", 10)));
    }

    [Fact]
    public void Le_troll_se_regenere_au_debut_de_son_tour()
    {
        var battle = new TacticalBattle(ArmyOf(Fixed("archer", 5, Ability.Shoots), 8), ArmyOf(Creature("troll"), 5), new GameRandom(2));
        battle.Field.Obstacles.Clear();
        var archers = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var trolls = battle.Units.Single(u => u.Side == BattleSide.Defender);
        battle.ForceTurn(archers);
        battle.Shoot(trolls); // blesse la créature de tête sans la tuer
        Assert.True(trolls.TopHitPoints < trolls.Type.HitPoints);

        // jusqu'au tour des Trolls
        while (battle.Active != trolls)
            battle.Defend();

        Assert.Equal(trolls.Type.HitPoints, trolls.TopHitPoints);
        Assert.Contains(battle.TakeLog(), a => a is UnitRegenerated r && r.Unit == trolls);
    }

    [Fact]
    public void L_ia_d_un_cavalier_charge_de_loin()
    {
        var (battle, knights, target) = Duel(Creature("cavalier"), 3, Creature("paysan"), 20, new Hex(3, 4), new Hex(6, 4));

        BattleAI.PlayTurn(battle);

        // il s'arrête à côté de la cible, sur la case la plus loin de son départ
        var best = target.Position.Neighbours().Where(h => Battlefield.InBounds(h)).Max(h => h.DistanceTo(new Hex(3, 4)));
        Assert.True(knights.Position.IsNextTo(target.Position));
        Assert.Equal(best, knights.Position.DistanceTo(new Hex(3, 4)));
    }
}
