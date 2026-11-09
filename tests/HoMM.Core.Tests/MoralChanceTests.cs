using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Le moral et la chance (étape 9, décidés avec l'utilisateur le 2026-11-09) : d'où ils viennent (charisme, mélange des
/// factions, troupe trop nombreuse, compétences, lieux), et ce qu'ils font au combat.
/// </summary>
public class MoralChanceTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static Army Army(params (string Id, int Count)[] stacks)
    {
        var army = new Army();
        foreach (var (id, count) in stacks)
            army.Add(Creature(id), count);
        return army;
    }

    /// <summary>Une partie sans adversaire : le héros du joueur (un Paladin, charisme 14, Commandement au départ).</summary>
    private static (GameState State, Hero Hero) Game()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small, 0);
        return (state, state.CurrentHero!);
    }

    [Fact]
    public void Le_charisme_donne_du_moral_a_toute_l_armee()
    {
        Assert.Equal(0, Morale.Aura(19));
        Assert.Equal(1, Morale.Aura(20));
        Assert.Equal(2, Morale.Aura(30));
        Assert.Equal(3, Morale.Aura(40));
        Assert.Equal(3, Morale.Aura(90));
    }

    [Fact]
    public void Une_seule_faction_donne_du_moral_et_trop_de_factions_en_retirent()
    {
        Assert.Equal(1, Morale.Factions(new[] { Creature("paysan"), Creature("archer") }));
        Assert.Equal(0, Morale.Factions(new[] { Creature("paysan"), Creature("squelette") }));
        Assert.Equal(-1, Morale.Factions(new[] { Creature("paysan"), Creature("squelette"), Creature("gobelin") }));
        Assert.Equal(-2, Morale.Factions(new[] { Creature("paysan"), Creature("squelette"), Creature("gobelin"), Creature("farfadet") }));
    }

    [Fact]
    public void Une_troupe_trop_nombreuse_perd_du_moral_peu_a_peu()
    {
        var peasant = Creature("paysan"); // 1 point de commandement chacun
        Assert.Equal(0, Morale.Overload(peasant, 100, 100));
        Assert.Equal(-1, Morale.Overload(peasant, 150, 100));
        Assert.Equal(-2, Morale.Overload(peasant, 151, 100));
        Assert.Equal(-2, Morale.Overload(peasant, 200, 100));
        Assert.Equal(-3, Morale.Overload(peasant, 201, 100));
    }

    [Fact]
    public void Le_moral_reste_entre_moins_trois_et_plus_trois()
    {
        Assert.Equal(3, new MoraleParts(3, 1, 0, 3, 1).Total);
        Assert.Equal(-3, new MoraleParts(0, -2, -3, 0, 0).Total);
        Assert.Equal(-3, new LuckParts(-5, 0).Total);
    }

    [Fact]
    public void Les_troupes_du_heros_ont_son_moral_et_sa_chance_au_combat_pas_les_monstres()
    {
        var (_, hero) = Game();
        hero.AddSkillRank(TestData.Skills.Get("chance"), 2);
        hero.SiteMorale = 1;
        // 400 paysans : bien plus que ses 154 points (2,6 fois la limite)
        var army = Army(("paysan", 400), ("archer", 10));

        var battle = new TacticalBattle(army, Army(("gobelin", 20)), new GameRandom(1), hero);

        var peasants = battle.Units.Single(u => u.Type.Id == "paysan");
        var archers = battle.Units.Single(u => u.Type.Id == "archer");
        // une seule faction +1, temple +1 ; les paysans trop nombreux −3
        Assert.Equal(2, archers.Morale);
        Assert.Equal(-1, peasants.Morale);
        Assert.Equal(2, archers.Luck);
        Assert.Equal(2, peasants.Luck);
        var goblins = battle.Units.Single(u => u.Side == BattleSide.Defender);
        Assert.Equal(0, goblins.Morale);
        Assert.Equal(0, goblins.Luck);
    }

    /// <summary>Un combat de paysans : la troupe de l'attaquant a la main, avec ce moral.</summary>
    private static (TacticalBattle Battle, BattleUnit Unit) Peasants(int seed, int morale)
    {
        var battle = new TacticalBattle(Army(("paysan", 10)), Army(("paysan", 10)), new GameRandom(seed));
        var unit = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        battle.ForceTurn(unit);
        unit.Morale = morale;
        return (battle, unit);
    }

    [Fact]
    public void Un_bon_moral_fait_parfois_rejouer_la_troupe_aussitot()
    {
        int cheered = 0;
        for (int seed = 1; seed <= 300; seed++)
        {
            var (battle, unit) = Peasants(seed, morale: 3);
            battle.TakeLog();
            Assert.True(battle.Move(battle.Reachable(unit).Keys.First(h => h != unit.Position)));
            if (battle.TakeLog().OfType<UnitCheered>().Any())
            {
                cheered++;
                Assert.Same(unit, battle.Active); // elle rejoue
                Assert.False(battle.CanWait); // sans pouvoir attendre
            }
        }
        // 15 % (5 % par point) : 45 sur 300 en moyenne
        Assert.InRange(cheered, 25, 70);
    }

    [Fact]
    public void Sans_bon_moral_ou_en_se_defendant_la_troupe_ne_rejoue_pas()
    {
        for (int seed = 1; seed <= 100; seed++)
        {
            var (battle, unit) = Peasants(seed, morale: 0);
            battle.Move(battle.Reachable(unit).Keys.First(h => h != unit.Position));
            Assert.DoesNotContain(battle.TakeLog(), a => a is UnitCheered);

            (battle, _) = Peasants(seed, morale: 3);
            battle.Defend();
            Assert.DoesNotContain(battle.TakeLog(), a => a is UnitCheered);
        }
    }

    [Fact]
    public void Un_mauvais_moral_fait_parfois_passer_son_tour()
    {
        int froze = 0, played = 0;
        for (int seed = 1; seed <= 4; seed++)
        {
            var (battle, unit) = Peasants(seed, morale: -3);
            battle.Defend();
            while (!battle.IsOver)
            {
                foreach (var action in battle.TakeLog())
                {
                    if (action is UnitFroze f && f.Unit == unit)
                        froze++;
                    if (action is UnitTurn t && t.Unit == unit)
                        played++;
                }
                battle.Defend();
            }
        }
        // 30 % (10 % par point)
        double share = froze / (double)(froze + played);
        Assert.InRange(share, 0.2, 0.4);
    }

    /// <summary>Des archers (2 dégâts chacun, toujours) tirent encore et encore sur beaucoup de paysans.</summary>
    private static List<UnitStruck> Volleys(int luck)
    {
        var battle = new TacticalBattle(Army(("archer", 10)), Army(("paysan", 5000)), new GameRandom(3));
        var archers = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var target = battle.Units.Single(u => u.Side == BattleSide.Defender);
        archers.Luck = luck;
        var strikes = new List<UnitStruck>();
        for (int i = 0; i < 200; i++)
        {
            battle.TakeLog();
            battle.ForceTurn(archers);
            Assert.True(battle.Shoot(target));
            strikes.AddRange(battle.TakeLog().OfType<UnitStruck>().Where(s => s.Attacker == archers));
        }
        return strikes;
    }

    [Fact]
    public void La_chance_double_parfois_les_degats_et_la_malchance_les_divise_par_deux()
    {
        var plain = Volleys(0);
        Assert.All(plain, s => Assert.Equal(0, s.Luck));
        int normal = plain[0].Damage;
        Assert.All(plain, s => Assert.Equal(normal, s.Damage));

        var lucky = Volleys(3);
        int doubled = lucky.Count(s => s.Luck > 0);
        Assert.InRange(doubled, 12, 50); // 15 % de 200 : 30 en moyenne
        Assert.All(lucky, s => Assert.Equal(s.Luck > 0 ? normal * 2 : normal, s.Damage));

        var unlucky = Volleys(-3);
        Assert.InRange(unlucky.Count(s => s.Luck < 0), 12, 50);
        Assert.All(unlucky, s => Assert.Equal(s.Luck < 0 ? normal / 2 : normal, s.Damage));
    }

    [Fact]
    public void Le_temple_et_le_cercle_des_fees_donnent_un_bonus_jusqu_au_prochain_combat()
    {
        var (state, temple) = CarteObjetsTests.GameNextTo(c => new BlessingSite(c, Blessing.Morale));
        var hero = state.CurrentHero!;

        CarteObjetsTests.Visit(state, temple);
        Assert.Equal(1, hero.SiteMorale);
        Assert.Contains(state.TakeEvents(), e => e is BlessingVisited { Gained: true });
        Assert.Equal(1, Morale.OfArmy(hero).Site);

        // une nouvelle visite ne donne rien de plus (le héros sort, puis revient)
        state.SetDestination(temple.Position.Offset(-1, 0));
        state.StepAlongPath();
        CarteObjetsTests.Visit(state, temple);
        Assert.Equal(1, hero.SiteMorale);
        Assert.Contains(state.TakeEvents(), e => e is BlessingVisited { Gained: false });

        var (state2, ring) = CarteObjetsTests.GameNextTo(c => new BlessingSite(c, Blessing.Luck));
        CarteObjetsTests.Visit(state2, ring);
        Assert.Equal(1, state2.CurrentHero!.SiteLuck);
        Assert.Equal(1, Morale.LuckOf(state2.CurrentHero).Total);
    }

    [Fact]
    public void Apres_un_combat_les_bonus_des_lieux_sont_epuises()
    {
        var state = new GameState(7, TestData.Data, MapSize.Normal, startingTowns: false) { QuickBattles = false };
        var hero = state.CurrentHero!;
        state.Map.AddObject(new MonsterGuard(hero.Position.Offset(2, 0), Creature("gobelin"), 10)); // assez pour ne pas s'enfuir
        hero.SiteMorale = hero.SiteLuck = 1;
        state.SetDestination(hero.Position.Offset(1, 0));
        state.StepAlongPath();

        var battle = state.CurrentBattle!;
        Assert.All(battle.Units.Where(u => u.Side == BattleSide.Attacker), u => Assert.Equal(1, u.Luck));
        BattleAI.PlayOut(battle);
        Assert.True(state.FinishBattle());

        Assert.Equal(0, hero.SiteMorale);
        Assert.Equal(0, hero.SiteLuck);
    }

    [Fact]
    public void Les_lieux_et_les_bonus_sont_sauvegardes()
    {
        var (state, temple) = CarteObjetsTests.GameNextTo(c => new BlessingSite(c, Blessing.Morale));
        CarteObjetsTests.Visit(state, temple);
        state.CurrentHero!.SiteLuck = 1;
        string saved = state.Save();

        var loaded = GameState.Load(saved, TestData.Data);

        Assert.Equal(saved, loaded.Save());
        Assert.Equal(1, loaded.CurrentHero!.SiteMorale);
        Assert.Equal(1, loaded.CurrentHero.SiteLuck);
        Assert.Contains(loaded.Map.Objects, o => o is BlessingSite { Blessing: Blessing.Morale } && o.Position == temple.Position);
    }

    [Fact]
    public void La_carte_a_des_temples_et_des_cercles_des_fees()
    {
        var state = new GameState(42, TestData.Data, MapSize.Normal);
        Assert.Contains(state.Map.Objects, o => o is BlessingSite { Blessing: Blessing.Morale });
        Assert.Contains(state.Map.Objects, o => o is BlessingSite { Blessing: Blessing.Luck });
    }

    [Fact]
    public void Une_troupe_qui_se_revolte_perd_le_moral_et_la_chance_de_son_heros()
    {
        var (battle, unit) = Peasants(1, morale: 2);
        unit.Luck = 2;
        unit.Turn();
        Assert.Equal(0, unit.Morale);
        Assert.Equal(0, unit.Luck);
    }
}
