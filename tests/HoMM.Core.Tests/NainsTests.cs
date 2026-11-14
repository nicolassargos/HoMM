using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Les Nains (décidés avec l'utilisateur, 2026-11-11) : leurs sept créatures et leurs capacités, et leur marque,
/// l'obstination (une troupe qui n'a pas bougé dans le round prend moins de dégâts).
/// </summary>
public class NainsTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    /// <summary>La même créature, sans une de ses capacités (pour comparer les dégâts).</summary>
    private static CreatureType Without(string id, string ability) =>
        Creature(id) with { Abilities = Creature(id).Abilities.Where(a => a != ability).ToList() };

    private static Army Army(params (CreatureType Type, int Count)[] stacks)
    {
        var army = new Army();
        foreach (var (type, count) in stacks)
            army.Add(type, count);
        return army;
    }

    private static Army Army(params (string Id, int Count)[] stacks) =>
        Army(stacks.Select(s => (Creature(s.Id), s.Count)).ToArray());

    /// <summary>Un combat sans obstacles, au tour de la première troupe de l'attaquant.</summary>
    private static TacticalBattle Battle(Army attacker, Army defender, int seed = 1)
    {
        var battle = new TacticalBattle(attacker, defender, new GameRandom(seed));
        battle.Field.Obstacles.Clear();
        return battle;
    }

    private static BattleUnit Unit(TacticalBattle battle, string id) => battle.Units.First(u => u.Type.Id == id);

    /// <summary>Les dégâts du premier coup porté (pas une riposte) dans le journal.</summary>
    private static int FirstHit(TacticalBattle battle) =>
        battle.TakeLog().OfType<UnitStruck>().First(s => !s.Retaliation).Damage;

    [Fact]
    public void Les_sept_creatures_des_nains_une_par_niveau_toutes_obstinees()
    {
        var dwarves = TestData.Creatures.All.Where(c => c.Faction == "nains").OrderBy(c => c.Tier).ToList();
        Assert.Equal(new[] { "porte-bouclier", "arbaletrier", "chevaucheur-chevre", "porte-etendard", "lance-flammes", "gyrocoptere", "machine-guerre" },
            dwarves.Select(c => c.Id));
        Assert.Equal(Enumerable.Range(1, 7), dwarves.Select(c => c.Tier));
        Assert.All(dwarves, c => Assert.Contains(Ability.Stubborn, c.Abilities));
        Assert.Contains(Ability.Made, Creature("gyrocoptere").Abilities);
        Assert.Contains(Ability.Made, Creature("machine-guerre").Abilities);
        Assert.Equal(1, Creature("machine-guerre").Cost[ResourceType.Crystal]);
        Assert.Contains("nains", MapGenerator.MonsterFactions);
    }

    [Fact]
    public void L_obstination_retire_un_cinquieme_des_degats_tant_que_la_troupe_n_a_pas_bouge()
    {
        int Hit(bool moved)
        {
            var battle = Battle(Army(("ogre", 20)), Army(("porte-bouclier", 100)));
            var shields = Unit(battle, "porte-bouclier");
            shields.Position = new Hex(5, 4);
            shields.HasMoved = moved;
            battle.ForceTurn(Unit(battle, "ogre"), new Hex(4, 4));
            Assert.True(battle.Attack(shields, new Hex(4, 4)));
            return FirstHit(battle);
        }
        int still = Hit(moved: false), moved = Hit(moved: true);
        Assert.InRange(still, (int)(moved * 0.8) - 1, (int)(moved * 0.8) + 1);
    }

    [Fact]
    public void Une_troupe_qui_se_deplace_perd_son_obstination_jusqu_au_round_suivant()
    {
        var battle = Battle(Army(("porte-bouclier", 10)), Army(("gobelin", 10)));
        var shields = Unit(battle, "porte-bouclier");
        battle.ForceTurn(shields, new Hex(1, 4));
        Assert.False(shields.HasMoved);
        Assert.True(battle.Move(new Hex(3, 4)));
        Assert.True(shields.HasMoved);
    }

    [Fact]
    public void Le_mur_de_boucliers_protege_les_tireurs_voisins_des_tirs()
    {
        int Hit(bool shielded)
        {
            var defender = shielded ? Army(("archer", 50), ("porte-bouclier", 10)) : Army(("archer", 50), ("gobelin", 10));
            var battle = Battle(Army(("orc", 20)), defender);
            var archers = Unit(battle, "archer");
            archers.Position = new Hex(7, 4);
            battle.Units.First(u => u.Side == BattleSide.Defender && u != archers).Position = new Hex(8, 4);
            battle.ForceTurn(Unit(battle, "orc"), new Hex(5, 4));
            Assert.True(battle.Shoot(archers));
            return FirstHit(battle);
        }
        int open = Hit(shielded: false), shielded = Hit(shielded: true);
        Assert.InRange(shielded, open / 2 - 1, open / 2 + 1);
    }

    [Fact]
    public void Les_carreaux_de_l_arbaletrier_percent_la_defense()
    {
        int Hit(CreatureType crossbow)
        {
            var battle = Battle(Army((crossbow, 30)), Army(("golem-pierre", 50)));
            battle.ForceTurn(Unit(battle, "arbaletrier"), new Hex(3, 4));
            Unit(battle, "golem-pierre").Position = new Hex(7, 4);
            Assert.True(battle.Shoot(Unit(battle, "golem-pierre")));
            return FirstHit(battle);
        }
        Assert.True(Hit(Creature("arbaletrier")) > Hit(Without("arbaletrier", Ability.Piercing)));
    }

    [Fact]
    public void Le_chevaucheur_de_chevre_riposte_avant_le_coup_et_le_crochet_ne_le_tire_pas()
    {
        var battle = Battle(Army(("loup", 10)), Army(("chevaucheur-chevre", 10)));
        var goats = Unit(battle, "chevaucheur-chevre");
        goats.Position = new Hex(5, 4);
        battle.ForceTurn(Unit(battle, "loup"), new Hex(4, 4));
        Assert.True(battle.Attack(goats, new Hex(4, 4)));
        var hits = battle.TakeLog().OfType<UnitStruck>().ToList();
        Assert.True(hits[0].Retaliation);
        Assert.Equal(goats, hits[0].Attacker);
        Assert.Contains(hits.Skip(1), h => h.Target == goats && !h.Retaliation);

        var hooking = Battle(Army(("tourmenteur", 5)), Army(("chevaucheur-chevre", 5)));
        var tormentors = Unit(hooking, "tourmenteur");
        Unit(hooking, "chevaucheur-chevre").Position = new Hex(6, 4);
        hooking.ForceTurn(tormentors, new Hex(3, 4));
        Assert.False(hooking.CanHook(tormentors, Unit(hooking, "chevaucheur-chevre")));
    }

    [Fact]
    public void La_banniere_donne_de_la_defense_aux_troupes_amies_voisines()
    {
        var battle = Battle(Army(("porte-etendard", 5), ("arbaletrier", 10), ("porte-bouclier", 10)), Army(("gobelin", 10)));
        var banner = Unit(battle, "porte-etendard");
        var crossbows = Unit(battle, "arbaletrier");
        var shields = Unit(battle, "porte-bouclier");
        var goblins = Unit(battle, "gobelin");
        banner.Position = new Hex(3, 4);
        crossbows.Position = banner.Position.Neighbours().First(Battlefield.InBounds);
        shields.Position = new Hex(1, 0);
        goblins.Position = banner.Position.Neighbours().Last(h => Battlefield.InBounds(h) && h != crossbows.Position);
        Assert.Equal(TacticalBattle.BannerDefense, battle.BannerBonus(crossbows));
        Assert.Equal(0, battle.BannerBonus(banner));
        Assert.Equal(0, battle.BannerBonus(shields));
        Assert.Equal(0, battle.BannerBonus(goblins));
    }

    [Fact]
    public void La_bombe_du_gyrocoptere_touche_les_ennemis_autour_de_la_cible_pas_les_amis()
    {
        var battle = Battle(Army(("gyrocoptere", 5), ("porte-bouclier", 10)), Army(("gobelin", 30), ("loup", 20)));
        var gyro = Unit(battle, "gyrocoptere");
        var shields = Unit(battle, "porte-bouclier");
        var goblins = Unit(battle, "gobelin");
        var wolves = Unit(battle, "loup");
        goblins.Position = new Hex(6, 4);
        var around = goblins.Position.Neighbours().Where(Battlefield.InBounds).ToList();
        wolves.Position = around[0];
        shields.Position = around[1];
        var from = around.First(h => h != wolves.Position && h != shields.Position);
        battle.ForceTurn(gyro, from);
        Assert.True(battle.Attack(goblins, from));
        var hits = battle.TakeLog().OfType<UnitStruck>().Where(s => s.Attacker == gyro).ToList();
        Assert.Contains(hits, h => h.Target == goblins);
        Assert.Contains(hits, h => h.Target == wolves);
        Assert.DoesNotContain(hits, h => h.Target == shields);
    }

    [Fact]
    public void L_obus_de_la_machine_de_guerre_touche_une_zone()
    {
        var battle = Battle(Army(("machine-guerre", 1)), Army(("gobelin", 30), ("loup", 20)));
        var machine = Unit(battle, "machine-guerre");
        var goblins = Unit(battle, "gobelin");
        var wolves = Unit(battle, "loup");
        goblins.Position = new Hex(8, 4);
        wolves.Position = goblins.Position.Neighbours().First(Battlefield.InBounds);
        battle.ForceTurn(machine, new Hex(1, 4));
        Assert.True(battle.Shoot(goblins));
        var hits = battle.TakeLog().OfType<UnitStruck>().Where(s => s.Attacker == machine).ToList();
        Assert.Contains(hits, h => h.Target == goblins && h.Ranged);
        Assert.Contains(hits, h => h.Target == wolves && h.Ranged);
    }
}
