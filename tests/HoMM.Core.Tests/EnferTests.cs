using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// L'Enfer (décidé avec l'utilisateur, 2026-11-11) : ses sept créatures et leurs capacités, sa marque (le prix du sang :
/// ses morts rendent des points de sort à son héros) et son insensibilité au feu.
/// </summary>
public class EnferTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static Army Army(params (string Id, int Count)[] stacks)
    {
        var army = new Army();
        foreach (var (id, count) in stacks)
            army.Add(Creature(id), count);
        return army;
    }

    private static Hero Warlock(int knowledge = 5, params string[] spells)
    {
        var map = new AdventureMap(5, 5);
        var hero = new Hero("Méphis", new Player("Rouge", map), new Cell(1, 1)) { Power = 2, Knowledge = knowledge };
        hero.SpellPoints = hero.MaxSpellPoints;
        foreach (var id in spells)
            hero.Learn(TestData.Spells.Get(id));
        return hero;
    }

    /// <summary>Un combat sans obstacles : l'attaquant en (4, 4), la cible juste à côté en (5, 4), au tour de l'attaquant.</summary>
    private static (TacticalBattle Battle, BattleUnit Attacker, BattleUnit Target) Duel(string attacker, int count, string target, int targetCount,
        int seed = 1, Hero? hero = null, Hero? defenderHero = null)
    {
        var battle = new TacticalBattle(Army((attacker, count)), Army((target, targetCount)), new GameRandom(seed), hero, defenderHero: defenderHero);
        battle.Field.Obstacles.Clear();
        var a = battle.Units.First(u => u.Side == BattleSide.Attacker);
        var d = battle.Units.First(u => u.Side == BattleSide.Defender);
        d.Position = new Hex(5, 4);
        battle.ForceTurn(a, new Hex(4, 4));
        return (battle, a, d);
    }

    [Fact]
    public void Les_sept_creatures_de_l_enfer_une_par_niveau_toutes_infernales()
    {
        var inferno = TestData.Creatures.All.Where(c => c.Faction == "enfer").OrderBy(c => c.Tier).ToList();
        Assert.Equal(new[] { "diablotin", "cultiste", "limon-magma", "succube", "tourmenteur", "moloch", "demon" }, inferno.Select(c => c.Id));
        Assert.Equal(Enumerable.Range(1, 7), inferno.Select(c => c.Tier));
        Assert.All(inferno, c => Assert.Contains(Ability.Infernal, c.Abilities));
        Assert.Equal("diablotin", Creature("demon").SummonType?.Id);
        Assert.Equal(1, Creature("demon").Cost[ResourceType.Mercury]);
    }

    [Fact]
    public void Le_feu_ne_touche_pas_les_creatures_de_l_enfer()
    {
        var hero = Warlock(10, "boule-de-feu");
        var battle = new TacticalBattle(Army(("paysan", 10)), Army(("diablotin", 20), ("gobelin", 20)), new GameRandom(1), hero);
        battle.Field.Obstacles.Clear();
        var imps = battle.Units.First(u => u.Type.Id == "diablotin");
        var goblins = battle.Units.First(u => u.Type.Id == "gobelin");
        imps.Position = new Hex(7, 4);
        goblins.Position = new Hex(8, 4);
        battle.ForceTurn(battle.Units.First(u => u.Side == BattleSide.Attacker));

        var hit = battle.AffectedBy(TestData.Spells.Get("boule-de-feu"), new Hex(7, 4));
        Assert.Contains(goblins, hit);
        Assert.DoesNotContain(imps, hit);
        Assert.True(battle.Cast(TestData.Spells.Get("boule-de-feu"), new Hex(7, 4)));
        Assert.Equal(20, imps.Count);
        Assert.True(goblins.Count < 20);
    }

    [Fact]
    public void Le_prix_du_sang_rend_des_points_de_sort_sans_depasser_deux_fois_le_savoir()
    {
        var hero = Warlock(knowledge: 3);
        hero.SpellPoints = 0;
        // des paysans frappent des Cultistes (niveau 2) du héros, qui se défend
        var (battle, peasants, cultists) = Duel("paysan", 200, "cultiste", 30, defenderHero: hero);
        Assert.True(battle.Attack(cultists, peasants.Position));
        var paid = battle.TakeLog().OfType<BloodPaid>().ToList();
        Assert.NotEmpty(paid);
        Assert.Equal(6, battle.SpellPointsOf(BattleSide.Defender)); // au plus 2 × savoir 3

        battle.ForceTurn(peasants);
        battle.Attack(cultists, peasants.Position);
        Assert.Equal(6, battle.SpellPointsOf(BattleSide.Defender));
    }

    [Fact]
    public void Le_diablotin_vole_des_points_de_sort_au_heros_ennemi()
    {
        var thief = Warlock(knowledge: 5);
        thief.SpellPoints = 0;
        var victim = Warlock(knowledge: 5);
        var (battle, imps, peasants) = Duel("diablotin", 20, "paysan", 50, hero: thief, defenderHero: victim);
        Assert.True(battle.Attack(peasants, imps.Position));
        Assert.Equal(4, battle.TakeLog().OfType<SpellPointsStolen>().Single().Points); // un point pour 5 diablotins
        Assert.Equal(46, battle.SpellPointsOf(BattleSide.Defender));
        Assert.Equal(4, battle.SpellPointsOf(BattleSide.Attacker));
    }

    [Fact]
    public void Les_cultistes_se_sacrifient_pour_frapper_plus_fort()
    {
        var (battle, cultists, peasants) = Duel("cultiste", 30, "paysan", 500);
        Assert.True(battle.Attack(peasants, cultists.Position));
        var log = battle.TakeLog();
        Assert.Equal(3, log.OfType<UnitSacrificed>().Single().Count); // un sur dix
        var blow = log.OfType<UnitStruck>().First(s => s.Attacker == cultists);
        // 27 cultistes de 3 à 5 dégâts, +50 % : au-dessus de ce que feraient 27 cultistes sans sacrifice au plus fort
        Assert.True(blow.Damage > 27 * 5);
    }

    [Fact]
    public void Le_limon_de_magma_se_divise_et_se_rejoint_apres_le_combat()
    {
        var army = Army(("limon-magma", 30));
        var battle = new TacticalBattle(Army(("paysan", 50)), army, new GameRandom(1));
        battle.Field.Obstacles.Clear();
        var peasants = battle.Units.First(u => u.Side == BattleSide.Attacker);
        var ooze = battle.Units.First(u => u.Side == BattleSide.Defender);
        ooze.Position = new Hex(5, 4);
        battle.ForceTurn(peasants, new Hex(4, 4));
        Assert.True(battle.Attack(ooze, peasants.Position));

        var split = battle.TakeLog().OfType<UnitSplit>().Single();
        Assert.Equal(ooze, split.From);
        Assert.Contains(split.Part, battle.Units);
        Assert.Equal(split.Count, split.Part.Count);

        BattleAI.PlayOut(battle);
        var report = battle.Report();
        int lost = report.DefenderLosses.Where(s => s.Type.Id == "limon-magma").Sum(s => s.Count);
        Assert.Equal(30 - lost, army.Stacks.Where(s => s.Type.Id == "limon-magma").Sum(s => s.Count));
    }

    [Fact]
    public void La_succube_seduit_parfois_sa_cible_qui_passe_son_tour_et_ne_riposte_pas()
    {
        int seduced = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var (battle, succubi, peasants) = Duel("succube", 5, "paysan", 300, seed);
            peasants.Position = new Hex(9, 4);
            Assert.True(battle.Shoot(peasants));
            var log = battle.TakeLog();
            if (log.OfType<UnitSeduced>().Any())
            {
                seduced++;
                // séduite : elle passe son tour (ici, aussitôt : c'est le sien) ou le passera
                Assert.True(peasants.IsSeduced || log.OfType<UnitEntranced>().Any(e => e.Unit == peasants));
            }
        }
        Assert.InRange(seduced, 1, 39);

        // séduite, elle ne riposte pas
        var (fight, hooker, target) = Duel("cultiste", 20, "paysan", 300);
        target.IsSeduced = true;
        Assert.True(fight.Attack(target, hooker.Position));
        Assert.DoesNotContain(fight.TakeLog().OfType<UnitStruck>(), s => s.Retaliation);

        // pas une créature façonnée
        for (int seed = 0; seed < 20; seed++)
        {
            var (battle, succubi, golems) = Duel("succube", 5, "golem-pierre", 50, seed);
            golems.Position = new Hex(9, 4);
            Assert.True(battle.Shoot(golems));
            Assert.DoesNotContain(battle.TakeLog(), a => a is UnitSeduced);
        }
    }

    [Fact]
    public void Le_tourmenteur_tire_un_tireur_contre_lui_et_le_frappe()
    {
        var (battle, tormentors, archers) = Duel("tourmenteur", 5, "archer", 30);
        archers.Position = new Hex(7, 4); // à 3 cases
        Assert.True(battle.CanHook(tormentors, archers));
        Assert.True(battle.Hook(archers));
        Assert.Equal(1, TacticalBattle.Distance(tormentors, archers));
        var log = battle.TakeLog();
        Assert.Single(log.OfType<UnitHooked>());
        Assert.Contains(log.OfType<UnitStruck>(), s => s.Attacker == tormentors && s.Target == archers);

        // pas trop loin, ni une grande créature, ni le niveau 7
        var (far, t2, a2) = Duel("tourmenteur", 5, "archer", 30);
        a2.Position = new Hex(8, 4);
        Assert.False(far.CanHook(t2, a2));
        var (angel, t3, angels) = Duel("tourmenteur", 5, "ange", 2);
        angels.Position = new Hex(6, 4);
        Assert.False(angel.CanHook(t3, angels));
    }

    [Fact]
    public void Le_moloch_brule_ses_voisins_et_se_nourrit_des_morts_de_l_enfer()
    {
        var battle = new TacticalBattle(Army(("moloch", 2), ("diablotin", 10)), Army(("paysan", 100)), new GameRandom(1));
        battle.Field.Obstacles.Clear();
        var moloch = battle.Units.First(u => u.Type.Id == "moloch");
        var imps = battle.Units.First(u => u.Type.Id == "diablotin");
        var peasants = battle.Units.First(u => u.Side == BattleSide.Defender);
        moloch.Position = new Hex(4, 4);
        imps.Position = new Hex(4, 6);
        peasants.Position = new Hex(5, 4);

        // un paysan frappe les diablotins : le Moloch se nourrit
        battle.ForceTurn(peasants);
        Assert.True(battle.Attack(imps, new Hex(5, 6)));
        Assert.True(moloch.Rage >= 1);
    }

    [Fact]
    public void L_aura_du_moloch_brule_au_debut_de_son_tour()
    {
        for (int seed = 0; seed < 5; seed++)
        {
            var battle = new TacticalBattle(Army(("moloch", 3)), Army(("ogre", 10)), new GameRandom(seed));
            BattleAI.PlayOut(battle);
            var burned = battle.TakeLog().OfType<UnitBurned>().ToList();
            if (burned.Count == 0)
                continue;
            Assert.All(burned, b => Assert.Equal(0, b.Damage % TacticalBattle.HeatPerCreature));
            return;
        }
        Assert.Fail("le Moloch n'a jamais brûlé ses voisins");
    }

    [Fact]
    public void Le_demon_invoque_des_diablotins_qui_disparaissent_apres_le_combat()
    {
        var (battle, demons, peasants) = Duel("demon", 2, "paysan", 30);
        Assert.True(battle.CanSummon);
        Assert.True(battle.Summon());
        var summoned = battle.TakeLog().OfType<UnitSummoned>().Single();
        Assert.Equal(20, summoned.Count);
        Assert.Equal("diablotin", summoned.Summoned.Type.Id);
        Assert.True(summoned.Summoned.IsSummoned);
        battle.ForceTurn(demons);
        Assert.False(battle.CanSummon); // une fois par combat

        BattleAI.PlayOut(battle);
        var report = battle.Report();
        Assert.DoesNotContain(report.AttackerLosses, s => s.Type.Id == "diablotin");
    }

    [Fact]
    public void Les_points_gagnes_au_combat_reviennent_au_heros_sans_depasser_son_maximum()
    {
        var hero = Warlock(knowledge: 3);
        hero.SpellPoints = 10;
        hero.SpendSpellPoints(5 - 12); // 5 dépensés, 12 gagnés
        Assert.Equal(17, hero.SpellPoints);
        hero.SpendSpellPoints(-100);
        Assert.Equal(hero.MaxSpellPoints, hero.SpellPoints);
    }
}
