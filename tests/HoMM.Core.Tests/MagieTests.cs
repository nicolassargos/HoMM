using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>La magie au combat (étape 9) : points de sort, un sort par round, les effets de chaque sort, l'IA.</summary>
public class MagieTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);
    private static SpellType Spell(string id) => TestData.Spells.Get(id);

    private static Army ArmyOf(string id, int count)
    {
        var army = new Army();
        army.Add(Creature(id), count);
        return army;
    }

    /// <summary>Un héros qui connaît ces sorts (puissance et savoir donnés).</summary>
    private static Hero Mage(int power = 1, int knowledge = 1, params string[] spells)
    {
        var map = new AdventureMap(5, 5);
        var hero = new Hero("Merlin", new Player("Rouge", map), new Cell(1, 1)) { Power = power, Knowledge = knowledge };
        hero.SpellPoints = hero.MaxSpellPoints;
        foreach (var id in spells)
            hero.Learn(Spell(id));
        return hero;
    }

    /// <summary>Un combat où le héros attaque ; c'est au tour de sa troupe.</summary>
    private static (TacticalBattle Battle, BattleUnit Ours, BattleUnit Theirs) Fight(Hero hero, string ours, int our, string theirs, int their)
    {
        var battle = new TacticalBattle(ArmyOf(ours, our), ArmyOf(theirs, their), new GameRandom(3), hero);
        battle.Field.Obstacles.Clear();
        var a = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var d = battle.Units.Single(u => u.Side == BattleSide.Defender);
        battle.ForceTurn(a);
        return (battle, a, d);
    }

    [Fact]
    public void Les_sorts_sont_lus_et_valides()
    {
        Assert.Equal(12, TestData.Spells.All.Count);
        Assert.Equal(4, TestData.Spells.All.Max(s => s.Level));
        var errors = new System.Collections.Generic.List<string>();
        DataValidation.CheckSpells(new[] { Spell("eclair") with { Level = 9 }, Spell("eclair") }, errors);
        Assert.Contains(errors, e => e.Contains("en double"));
        Assert.Contains(errors, e => e.Contains("niveau 9"));
    }

    [Fact]
    public void Les_points_de_sort_viennent_du_savoir_et_reviennent_un_par_jour()
    {
        var hero = Mage(knowledge: 3);
        Assert.Equal(30, hero.MaxSpellPoints);
        hero.SpellPoints = 20;
        hero.NewDay();
        Assert.Equal(21, hero.SpellPoints);
        hero.SpellPoints = 30;
        hero.NewDay();
        Assert.Equal(30, hero.SpellPoints);
    }

    [Fact]
    public void La_fleche_magique_fait_des_degats_selon_la_puissance_et_garde_le_tour_de_la_troupe()
    {
        var hero = Mage(power: 2, spells: "fleche-magique");
        var (battle, ours, theirs) = Fight(hero, "archer", 10, "paysan", 100);

        Assert.True(battle.Cast(Spell("fleche-magique"), theirs.Position));

        Assert.Equal(70, theirs.Count); // 10 + 10 × 2 = 30 dégâts, 30 paysans
        Assert.Equal(ours, battle.Active); // la troupe joue encore
        Assert.Equal(5, battle.SpellPointsOf(BattleSide.Attacker));
        Assert.Equal(10, hero.SpellPoints); // le héros ne perd ses points qu'au bilan
    }

    [Fact]
    public void Un_seul_sort_par_round_et_assez_de_points()
    {
        var hero = Mage(knowledge: 1, spells: new[] { "fleche-magique", "eclair" });
        var (battle, _, theirs) = Fight(hero, "archer", 10, "paysan", 500);

        Assert.True(battle.Cast(Spell("fleche-magique"), theirs.Position));
        Assert.False(battle.Cast(Spell("fleche-magique"), theirs.Position)); // déjà un sort ce round

        // au round suivant, l'éclair coûte 10 : il ne reste que 5 points
        while (battle.Round == 1)
            battle.Defend();
        Assert.False(battle.CanCast(Spell("eclair")));
        Assert.True(battle.CanCast(Spell("fleche-magique")));
    }

    [Fact]
    public void On_ne_lance_pas_un_sort_inconnu_ni_sans_heros_ni_sur_une_mauvaise_cible()
    {
        var hero = Mage(spells: "fleche-magique");
        var (battle, ours, theirs) = Fight(hero, "archer", 10, "paysan", 100);
        Assert.False(battle.Cast(Spell("eclair"), theirs.Position)); // inconnu
        Assert.False(battle.Cast(Spell("fleche-magique"), ours.Position)); // un sort d'attaque sur une troupe amie
        Assert.False(battle.Cast(Spell("fleche-magique"), new Hex(5, 0))); // une case vide

        var (monsters, _, _) = Fight(hero, "archer", 10, "paysan", 100);
        monsters.ForceTurn(monsters.Units.Single(u => u.Side == BattleSide.Defender));
        Assert.Null(monsters.CasterSide); // les monstres n'ont pas de héros
    }

    [Fact]
    public void La_boule_de_feu_touche_la_case_et_ses_voisines_amis_compris()
    {
        var defender = new Army();
        defender.AddStack(Creature("paysan"), 100);
        defender.AddStack(Creature("paysan"), 100);
        defender.AddStack(Creature("paysan"), 100);
        var battle = new TacticalBattle(ArmyOf("archer", 10), defender, new GameRandom(1), Mage(knowledge: 2, spells: "boule-de-feu"));
        battle.Field.Obstacles.Clear();
        var archers = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var peasants = battle.Units.Where(u => u.Side == BattleSide.Defender).ToList();
        peasants[0].Position = new Hex(5, 4);
        peasants[1].Position = new Hex(6, 4); // voisine
        peasants[2].Position = new Hex(8, 4); // trop loin
        battle.ForceTurn(archers, new Hex(4, 4)); // voisine aussi

        Assert.True(battle.Cast(Spell("boule-de-feu"), new Hex(5, 4)));

        Assert.Equal(80, peasants[0].Count); // 10 + 10 × 1 = 20 dégâts
        Assert.Equal(80, peasants[1].Count);
        Assert.Equal(100, peasants[2].Count);
        Assert.Equal(8, archers.Count); // 20 dégâts : 2 archers
    }

    [Fact]
    public void Benediction_et_malediction_fixent_les_degats()
    {
        // les paysans ont 1 PV : les tués donnent les dégâts
        int Kills(CreatureType ogre, string? spell)
        {
            var army = new Army();
            army.Add(ogre, 10);
            var battle = new TacticalBattle(army, ArmyOf("paysan", 1000), new GameRandom(3), Mage(spells: "benediction"));
            battle.Field.Obstacles.Clear();
            var ours = battle.Units.Single(u => u.Side == BattleSide.Attacker);
            var theirs = battle.Units.Single(u => u.Side == BattleSide.Defender);
            theirs.Position = new Hex(5, 4);
            battle.ForceTurn(ours, new Hex(4, 4));
            if (spell == "benediction")
                Assert.True(battle.Cast(Spell(spell), ours.Position));
            else if (spell != null)
                ours.Enchant(Spell(spell), 5);
            battle.Attack(theirs, new Hex(4, 4));
            return 1000 - theirs.Count;
        }

        var ogre = Creature("ogre");
        Assert.Equal(Kills(ogre with { MinDamage = ogre.MaxDamage }, null), Kills(ogre, "benediction"));
        Assert.Equal(Kills(ogre with { MaxDamage = ogre.MinDamage }, null), Kills(ogre, "malediction"));
        Assert.True(Kills(ogre, "benediction") > Kills(ogre, "malediction"));
    }

    [Fact]
    public void Les_sorts_qui_durent_changent_les_caracteristiques_puis_s_arretent()
    {
        var hero = Mage(power: 1, knowledge: 5, spells: new[] { "peau-de-pierre", "hate", "lenteur", "faiblesse" });
        var (battle, ours, theirs) = Fight(hero, "epeiste", 10, "ogre", 10);
        int defense = ours.Defense, speed = battle.SpeedOf(ours);

        battle.Cast(Spell("peau-de-pierre"), ours.Position);
        Assert.Equal(defense + 5, ours.Defense);

        // puissance 1 : le sort dure le round en cours et le suivant
        battle.Wait();
        battle.Defend();
        battle.Defend();
        Assert.Equal(2, battle.Round);
        Assert.True(ours.IsUnder(SpellEffect.StoneSkin));
        battle.ForceTurn(ours);
        battle.Cast(Spell("hate"), ours.Position);
        Assert.Equal(speed + 3, battle.SpeedOf(ours));
        while (battle.Round == 2)
            battle.Defend();
        Assert.False(ours.IsUnder(SpellEffect.StoneSkin));

        battle.ForceTurn(ours);
        int ogreSpeed = battle.SpeedOf(theirs), ogreAttack = theirs.Attack;
        battle.Cast(Spell("lenteur"), theirs.Position);
        Assert.Equal(ogreSpeed / 2, battle.SpeedOf(theirs));
        while (battle.Round == 3)
            battle.Defend();
        battle.ForceTurn(ours);
        battle.Cast(Spell("faiblesse"), theirs.Position);
        Assert.Equal(ogreAttack - 6, theirs.Attack);
    }

    [Fact]
    public void La_hate_remplace_la_lenteur()
    {
        var (battle, ours, _) = Fight(Mage(knowledge: 3, spells: new[] { "hate", "lenteur" }), "archer", 10, "paysan", 10);
        var unit = ours;
        unit.Enchant(Spell("lenteur"), 5);
        battle.Cast(Spell("hate"), unit.Position);
        Assert.False(unit.IsUnder(SpellEffect.Slow));
        Assert.True(unit.IsUnder(SpellEffect.Haste));
    }

    [Fact]
    public void Les_soins_soignent_la_creature_de_tete_et_retirent_les_sorts_nefastes()
    {
        var (battle, ours, _) = Fight(Mage(power: 2, spells: "soins"), "cavalier", 3, "paysan", 10);
        ours.TakeDamage(150); // 1 cavalier tué, le suivant à 90 PV
        ours.Enchant(Spell("lenteur"), 5);

        battle.Cast(Spell("soins"), ours.Position);

        Assert.Equal(2, ours.Count); // les soins ne ramènent personne
        Assert.Equal(110, ours.TopHitPoints); // 10 + 5 × 2 = 20 PV
        Assert.False(ours.IsUnder(SpellEffect.Slow));
    }

    [Fact]
    public void La_resurrection_ramene_des_creatures_tuees_sans_depasser_le_debut()
    {
        var (battle, ours, _) = Fight(Mage(power: 3, knowledge: 2, spells: "resurrection"), "archer", 20, "paysan", 10);
        Assert.False(battle.IsValidTarget(Spell("resurrection"), ours.Position)); // personne à ramener
        ours.TakeDamage(150); // 15 archers tués

        Assert.True(battle.Cast(Spell("resurrection"), ours.Position)); // 40 + 20 × 3 = 100 PV : 10 archers
        Assert.Equal(15, ours.Count);

        var report = battle.Report();
        Assert.Equal(5, report.AttackerLosses.Sum(s => s.Count)); // les ramenés ne sont pas perdus
    }

    [Fact]
    public void Le_combat_rapide_lance_des_sorts_sans_retirer_les_points_d_une_estimation()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small, startingTowns: false);
        var hero = state.Players[0].Heroes[0];
        hero.Learn(Spell("fleche-magique"));
        hero.Army.Add(Creature("ange"), 5);
        var guard = new MonsterGuard(new Cell(0, 0), Creature("paysan"), 200, 200);

        var report = Battle.Resolve(hero.Army.Clone(), guard.Army.Clone(), hero);
        Assert.True(report.AttackerSpellPointsSpent >= 5); // le combat rapide lance des sorts
        Assert.Equal(10, hero.SpellPoints); // une estimation ne coûte rien
    }

    [Fact]
    public void L_IA_lance_le_sort_qui_vaut_le_plus()
    {
        var hero = Mage(power: 1, knowledge: 3, spells: new[] { "fleche-magique", "eclair", "benediction" });
        var (battle, _, theirs) = Fight(hero, "archer", 10, "epeiste", 10);

        var choice = BattleAI.ChooseSpell(battle);

        Assert.NotNull(choice);
        Assert.Equal("eclair", choice!.Value.Spell.Id); // 35 dégâts sur des épéistes, plus que la flèche (20)
        Assert.Equal(theirs.Position, choice.Value.Target);
    }

    [Fact]
    public void L_IA_ne_lance_pas_une_boule_de_feu_qui_blesse_surtout_ses_troupes()
    {
        var defender = new Army();
        defender.Add(Creature("paysan"), 5);
        var battle = new TacticalBattle(ArmyOf("ange", 3), defender, new GameRandom(1), Mage(knowledge: 2, spells: "boule-de-feu"));
        battle.Field.Obstacles.Clear();
        var angels = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var peasants = battle.Units.Single(u => u.Side == BattleSide.Defender);
        peasants.Position = new Hex(5, 4);
        battle.ForceTurn(angels, new Hex(4, 4));

        var choice = BattleAI.ChooseSpell(battle);

        // une boule de feu sur la case des paysans toucherait aussi les anges : l'IA vise une case qui les épargne
        Assert.NotNull(choice);
        Assert.DoesNotContain(angels, battle.AffectedBy(choice!.Value.Spell, choice.Value.Target));
    }

    [Fact]
    public void Les_sorts_et_les_points_sont_sauvegardes()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small, startingTowns: false);
        var hero = state.Players[0].Heroes[0];
        hero.Learn(Spell("eclair"));
        hero.Learn(Spell("fleche-magique"));
        hero.SpellPoints = 4;

        var loaded = GameState.Load(state.Save(), TestData.Data).Players[0].Heroes[0];

        Assert.Equal(4, loaded.SpellPoints);
        Assert.Equal(new[] { "fleche-magique", "eclair" }, loaded.Spells.Select(s => s.Id)); // par niveau
    }

    [Fact]
    public void Apres_un_combat_sur_la_carte_le_heros_perd_les_points_depenses()
    {
        var state = new GameState(7, TestData.Data, MapSize.Normal, startingTowns: false);
        var hero = state.CurrentHero!;
        hero.Learn(Spell("fleche-magique"));
        var start = hero.Position;
        state.Map.AddObject(new MonsterGuard(start.Offset(2, 0), Creature("gobelin"), 30));
        state.SetDestination(start.Offset(1, 0));

        state.StepAlongPath(); // combat rapide

        var fought = Assert.Single(state.TakeEvents().OfType<BattleFought>());
        Assert.True(fought.Report.AttackerSpellPointsSpent > 0);
        Assert.Equal(10 - fought.Report.AttackerSpellPointsSpent, hero.SpellPoints);
    }
}
