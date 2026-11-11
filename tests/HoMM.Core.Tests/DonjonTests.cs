using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Le Donjon (décidé avec l'utilisateur, 2026-11-11) : ses sept créatures et leurs capacités, et sa marque, la Terreur
/// (chaque coup fait perdre du moral à la cible).
/// </summary>
public class DonjonTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static Army Army(params (string Id, int Count)[] stacks)
    {
        var army = new Army();
        foreach (var (id, count) in stacks)
            army.Add(Creature(id), count);
        return army;
    }

    /// <summary>Un combat sans obstacles, l'attaquant à <paramref name="at"/> et sa cible juste à côté, à son tour.</summary>
    private static (TacticalBattle Battle, BattleUnit Attacker, BattleUnit Target) Duel(string attacker, int count, string target, int targetCount, int seed = 1)
    {
        var battle = new TacticalBattle(Army((attacker, count)), Army((target, targetCount)), new GameRandom(seed));
        battle.Field.Obstacles.Clear();
        var a = battle.Units.First(u => u.Side == BattleSide.Attacker);
        var d = battle.Units.First(u => u.Side == BattleSide.Defender);
        d.Position = new Hex(5, 4);
        battle.ForceTurn(a, new Hex(4, 4));
        return (battle, a, d);
    }

    [Fact]
    public void Les_sept_creatures_du_donjon_une_par_niveau_toutes_terrifiantes()
    {
        var dungeon = TestData.Creatures.All.Where(c => c.Faction == "donjon").OrderBy(c => c.Tier).ToList();
        Assert.Equal(new[] { "homme-lezard", "homme-grenouille", "minotaure", "aberration", "ver-des-profondeurs", "tyran-oeil", "hydre" },
            dungeon.Select(c => c.Id));
        Assert.Equal(Enumerable.Range(1, 7), dungeon.Select(c => c.Tier));
        Assert.All(dungeon, c => Assert.Contains(Ability.Terror, c.Abilities));
        Assert.Equal(new[] { "malediction", "lenteur", "faiblesse", "aveuglement" }, Creature("tyran-oeil").RaySpells.Select(s => s.Id));
        Assert.Equal(1, Creature("hydre").Cost[ResourceType.Sulfur]);
    }

    [Fact]
    public void La_terreur_fait_perdre_un_point_de_moral_a_chaque_coup()
    {
        var (battle, lizards, peasants) = Duel("homme-lezard", 20, "paysan", 500);
        Assert.Equal(0, peasants.Morale);

        Assert.True(battle.Attack(peasants, lizards.Position));

        Assert.Equal(-1, peasants.Morale);
        Assert.Equal(0, lizards.Morale); // la riposte des paysans ne terrifie pas
        Assert.Contains(battle.TakeLog(), a => a is UnitTerrified { Morale: -1 });
    }

    [Fact]
    public void L_horreur_de_l_aberration_fait_perdre_deux_points_de_moral()
    {
        var (battle, aberrations, peasants) = Duel("aberration", 5, "paysan", 500);
        Assert.True(battle.Attack(peasants, aberrations.Position));
        Assert.Equal(-2, peasants.Morale);
    }

    [Theory]
    [InlineData("squelette")]     // mort-vivant
    [InlineData("golem-pierre")]  // façonné
    [InlineData("ange")]          // niveau 7
    [InlineData("minotaure")]     // brave
    public void Certains_ne_connaissent_pas_la_terreur(string target)
    {
        var (battle, lizards, unit) = Duel("homme-lezard", 50, target, 20);
        int before = unit.Morale;
        Assert.True(battle.Attack(unit, lizards.Position));
        Assert.Equal(before, unit.Morale);
        Assert.DoesNotContain(battle.TakeLog(), a => a is UnitTerrified t && t.Unit == unit);
    }

    [Fact]
    public void Le_minotaure_brave_a_toujours_au_moins_un_de_moral()
    {
        var battle = new TacticalBattle(Army(("minotaure", 10)), Army(("paysan", 10)), new GameRandom(1));
        Assert.Equal(1, battle.Units.First(u => u.Type.Id == "minotaure").Morale);
    }

    [Fact]
    public void L_aberration_riposte_a_chaque_attaque_du_round()
    {
        var battle = new TacticalBattle(Army(("paysan", 30), ("griffon", 5)), Army(("aberration", 10)), new GameRandom(1));
        battle.Field.Obstacles.Clear();
        var aberrations = battle.Units.First(u => u.Side == BattleSide.Defender);
        aberrations.Position = new Hex(5, 4);
        var peasants = battle.Units.First(u => u.Type.Id == "paysan");
        var griffons = battle.Units.First(u => u.Type.Id == "griffon");
        var second = aberrations.Position.Neighbours().First(h => h != new Hex(4, 4) && Battlefield.InBounds(h));
        griffons.Position = second;
        battle.ForceTurn(peasants, new Hex(4, 4));
        Assert.True(battle.Attack(aberrations, peasants.Position));
        battle.ForceTurn(griffons, second);
        Assert.True(battle.Attack(aberrations, griffons.Position));

        Assert.Equal(2, battle.TakeLog().OfType<UnitStruck>().Count(s => s.Retaliation && s.Attacker == aberrations));
    }

    [Fact]
    public void Le_ver_passe_sous_les_obstacles_qui_l_entourent()
    {
        var battle = new TacticalBattle(Army(("ver-des-profondeurs", 5), ("homme-lezard", 5)), Army(("paysan", 10)), new GameRandom(1));
        battle.Field.Obstacles.Clear();
        var worms = battle.Units.First(u => u.Type.Id == "ver-des-profondeurs");
        var lizards = battle.Units.First(u => u.Type.Id == "homme-lezard");
        lizards.Position = new Hex(1, 8);
        var center = new Hex(5, 4);
        foreach (var hex in center.Neighbours())
            battle.Field.Obstacles.Add(hex);
        battle.ForceTurn(worms, center);

        Assert.Contains(battle.Reachable(worms).Keys, h => h.DistanceTo(center) >= 2);
        // un homme-lézard, lui, reste coincé
        battle.ForceTurn(lizards, center);
        Assert.DoesNotContain(battle.Reachable(lizards).Keys, h => h.DistanceTo(center) >= 2);
    }

    [Fact]
    public void Le_ver_avale_parfois_une_creature_entiere_mais_jamais_d_un_niveau_plus_haut()
    {
        int swallowed = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var (battle, worms, lizards) = Duel("ver-des-profondeurs", 3, "homme-lezard", 500, seed);
            int before = lizards.Count;
            Assert.True(battle.Attack(lizards, worms.Position));
            var log = battle.TakeLog();
            int eaten = log.OfType<UnitSwallowed>().Count();
            int killed = log.OfType<UnitStruck>().Where(s => s.Target == lizards).Sum(s => s.Killed);
            Assert.Equal(before - killed - eaten, lizards.Count);
            swallowed += eaten;
        }
        Assert.InRange(swallowed, 1, 39);

        for (int seed = 0; seed < 20; seed++)
        {
            var (battle, worms, angels) = Duel("ver-des-profondeurs", 3, "ange", 5, seed);
            Assert.True(battle.Attack(angels, worms.Position));
            Assert.DoesNotContain(battle.TakeLog(), a => a is UnitSwallowed);
        }
    }

    [Fact]
    public void Les_rayons_du_tyran_oeil_lancent_un_sort_nefaste_mais_pas_sur_une_creature_faconnee()
    {
        var (battle, tyrants, peasants) = Duel("tyran-oeil", 3, "paysan", 200);
        peasants.Position = new Hex(9, 4); // à distance : un tir
        Assert.True(battle.Shoot(peasants));
        var applied = battle.TakeLog().OfType<SpellApplied>().Single();
        Assert.Equal(peasants, applied.Unit);
        Assert.Contains(applied.Spell.Id, new[] { "malediction", "lenteur", "faiblesse", "aveuglement" });
        Assert.True(peasants.IsUnder(applied.Spell.Effect));

        var (battle2, tyrants2, golems) = Duel("tyran-oeil", 3, "golem-pierre", 20);
        golems.Position = new Hex(9, 4);
        Assert.True(battle2.Shoot(golems));
        Assert.DoesNotContain(battle2.TakeLog(), a => a is SpellApplied);
    }

    [Fact]
    public void L_hydre_mord_tous_ses_voisins_et_personne_ne_riposte()
    {
        var battle = new TacticalBattle(Army(("hydre", 1)), Army(("paysan", 300), ("archer", 100)), new GameRandom(1));
        battle.Field.Obstacles.Clear();
        var hydra = battle.Units.First(u => u.Side == BattleSide.Attacker);
        var peasants = battle.Units.First(u => u.Type.Id == "paysan");
        var archers = battle.Units.First(u => u.Type.Id == "archer");
        peasants.Position = new Hex(5, 4);
        archers.Position = new Hex(4, 5);
        battle.ForceTurn(hydra, new Hex(4, 4));

        Assert.True(battle.Attack(peasants, hydra.Position));
        var struck = battle.TakeLog().OfType<UnitStruck>().ToList();
        Assert.Contains(struck, s => s.Attacker == hydra && s.Target == peasants);
        Assert.Contains(struck, s => s.Attacker == hydra && s.Target == archers);
        Assert.DoesNotContain(struck, s => s.Retaliation);
    }
}
