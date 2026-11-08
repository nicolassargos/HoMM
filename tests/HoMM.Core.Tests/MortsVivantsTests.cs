using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les morts-vivants (étape 9) : leurs capacités au combat, et leur place sur la carte.</summary>
public class MortsVivantsTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static Army ArmyOf(string id, int count)
    {
        var army = new Army();
        army.Add(Creature(id), count);
        return army;
    }

    /// <summary>Un combat sans obstacle ; les deux troupes côte à côte, c'est au tour de l'attaquant.</summary>
    private static (TacticalBattle Battle, BattleUnit Attacker, BattleUnit Defender) Duel(string attacker, int a, string defender, int d,
        Hero? hero = null)
    {
        var battle = new TacticalBattle(ArmyOf(attacker, a), ArmyOf(defender, d), new GameRandom(5), hero);
        battle.Field.Obstacles.Clear();
        var first = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var second = battle.Units.Single(u => u.Side == BattleSide.Defender);
        second.Position = new Hex(5, 4);
        battle.ForceTurn(first, new Hex(4, 4));
        return (battle, first, second);
    }

    [Fact]
    public void Les_sept_morts_vivants_existent_un_par_niveau()
    {
        var undead = TestData.Creatures.All.Where(c => c.Faction == "morts-vivants").OrderBy(c => c.Tier).ToList();
        Assert.Equal(new[] { "squelette", "zombie", "fantome", "araignee", "vampire", "liche", "colosse-os" }, undead.Select(c => c.Id));
        Assert.All(undead, c => Assert.Contains(Ability.Undead, c.Abilities));
    }

    [Fact]
    public void Un_mort_vivant_ne_craint_ni_les_sorts_de_l_esprit_ni_les_soins()
    {
        var map = new AdventureMap(5, 5);
        var mage = new Hero("Merlin", new Player("Rouge", map), new Cell(1, 1)) { Knowledge = 9 };
        mage.SpellPoints = mage.MaxSpellPoints;
        foreach (var id in new[] { "aveuglement", "malediction", "faiblesse", "soins", "resurrection", "fleche-magique" })
            mage.Learn(TestData.Spells.Get(id));
        // le héros commande des squelettes contre des zombies
        var (battle, ours, theirs) = Duel("squelette", 20, "zombie", 10, mage);
        ours.TakeDamage(10);

        foreach (var id in new[] { "aveuglement", "malediction", "faiblesse" })
            Assert.False(battle.IsValidTarget(TestData.Spells.Get(id), theirs.Position));
        Assert.False(battle.IsValidTarget(TestData.Spells.Get("soins"), ours.Position));
        Assert.False(battle.IsValidTarget(TestData.Spells.Get("resurrection"), ours.Position));
        Assert.True(battle.IsValidTarget(TestData.Spells.Get("fleche-magique"), theirs.Position)); // les dégâts, si
    }

    [Fact]
    public void Des_fantomes_qui_grossissent_au_dela_du_charisme_peuvent_se_revolter_pendant_le_combat()
    {
        var ghost = Creature("fantome");
        int revolts = 0;
        for (int seed = 0; seed < 10; seed++)
        {
            var map = new AdventureMap(5, 5);
            var hero = new Hero("Mortimer", new Player("Rouge", map), new Cell(1, 1)) { Charisma = 5 };
            int atLimit = Command.Limit(hero.Charisma) / ghost.CommandCost; // juste à la limite : ils obéissent au départ
            var army = new Army();
            army.Add(ghost, atLimit);
            var battle = new TacticalBattle(army, ArmyOf("paysan", 500), new GameRandom(seed), hero);
            battle.Field.Obstacles.Clear();
            var ghosts = battle.Units.Single(u => u.Side == BattleSide.Attacker);
            var peasants = battle.Units.Single(u => u.Side == BattleSide.Defender);
            peasants.Position = new Hex(5, 4);
            battle.ForceTurn(ghosts, new Hex(4, 4));

            battle.Attack(peasants, new Hex(4, 4)); // ils tuent, grossissent et dépassent la limite

            var log = battle.TakeLog();
            Assert.True(log.OfType<UnitGrew>().Any());
            if (log.OfType<UnitRevolted>().SingleOrDefault() is not { } revolted)
                continue;
            revolts++;
            Assert.Equal(BattleSide.Defender, revolted.Unit.Side); // ils combattent maintenant le héros
            Assert.True(revolted.Unit.IsRebel);
            BattleAI.PlayOut(battle);
            var report = battle.Report();
            Assert.Equal(ghost, Assert.Single(report.Rebels).Type);
            Assert.DoesNotContain(army.Stacks, s => s.Type == ghost); // ils ont quitté son armée
            Assert.Equal(BattleOutcome.DefenderWon, battle.Outcome); // le héros n'a plus de troupe à lui
        }
        Assert.True(revolts > 0);
    }

    [Fact]
    public void Le_fantome_grossit_de_chaque_creature_qu_il_tue_et_le_garde_apres_le_combat()
    {
        var army = ArmyOf("fantome", 10);
        var battle = new TacticalBattle(army, ArmyOf("paysan", 100), new GameRandom(5));
        battle.Field.Obstacles.Clear();
        var ghosts = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var peasants = battle.Units.Single(u => u.Side == BattleSide.Defender);
        peasants.Position = new Hex(5, 4);
        battle.ForceTurn(ghosts, new Hex(4, 4));

        battle.Attack(peasants, new Hex(4, 4));

        int killed = 100 - peasants.Count;
        Assert.True(killed > 0);
        int lostToRetaliation = battle.TakeLog().OfType<UnitStruck>().Where(s => s.Retaliation).Sum(s => s.Killed);
        Assert.Equal(10 + killed - lostToRetaliation, ghosts.Count);
        battle.Report();
        Assert.Equal(ghosts.Count, army.TotalCount); // ils restent dans l'armée
    }

    [Fact]
    public void La_morsure_de_l_araignee_empoisonne_au_debut_des_tours_de_la_cible()
    {
        var (battle, spiders, ogres) = Duel("araignee", 5, "ogre", 10);
        battle.Attack(ogres, new Hex(4, 4));

        // les ogres jouent juste après (plus lents que les araignées) : le poison agit au début de leur tour
        Assert.Equal(ogres, battle.Active);
        var poison = Assert.Single(battle.TakeLog().OfType<UnitPoisoned>());
        Assert.Equal(TacticalBattle.PoisonPerCreature * 5, poison.Damage);
        Assert.Equal(TacticalBattle.PoisonTurns - 1, ogres.PoisonTurnsLeft);
        // il s'arrête au bout de trois de leurs tours
        for (int i = 0; i < 6 && ogres.PoisonTurnsLeft > 0; i++)
            battle.Defend();
        Assert.Equal(0, ogres.PoisonTurnsLeft);
    }

    [Fact]
    public void Un_mort_vivant_n_est_pas_empoisonne()
    {
        var (battle, _, zombies) = Duel("araignee", 5, "zombie", 10);
        battle.Attack(zombies, new Hex(4, 4));
        Assert.Equal(0, zombies.PoisonTurnsLeft);
    }

    [Fact]
    public void Le_vampire_se_soigne_puis_ramene_des_vampires_sans_depasser_le_debut()
    {
        var (battle, vampires, peasants) = Duel("vampire", 10, "paysan", 500);
        vampires.TakeDamage(100); // 2 vampires tués, le suivant à moitié
        peasants.RetaliationsLeft = 0; // pas de riposte : on regarde le drain seul

        battle.Attack(peasants, new Hex(4, 4));

        var drained = Assert.Single(battle.TakeLog().OfType<UnitDrained>());
        Assert.Equal(100, drained.HitPoints); // tout ce qui manquait, pas plus
        Assert.Equal(2, drained.Revived);
        Assert.Equal(10, vampires.Count); // jamais plus qu'au début
        Assert.Equal(vampires.Type.HitPoints, vampires.TopHitPoints);
    }

    [Fact]
    public void Le_nuage_de_la_liche_touche_les_voisins_vivants_mais_pas_les_morts_vivants()
    {
        var defender = new Army();
        defender.AddStack(Creature("paysan"), 100);
        defender.AddStack(Creature("paysan"), 100);
        defender.AddStack(Creature("zombie"), 10);
        defender.AddStack(Creature("paysan"), 100);
        var battle = new TacticalBattle(ArmyOf("liche", 5), defender, new GameRandom(5));
        battle.Field.Obstacles.Clear();
        var liches = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var units = battle.Units.Where(u => u.Side == BattleSide.Defender).ToList();
        units[0].Position = new Hex(7, 4); // la cible
        units[1].Position = new Hex(8, 4); // voisine vivante
        units[2].Position = new Hex(6, 4); // voisine morte-vivante
        units[3].Position = new Hex(9, 0); // loin
        battle.ForceTurn(liches, new Hex(1, 4));

        Assert.True(battle.Shoot(units[0]));

        Assert.True(units[0].Count < 100);
        Assert.True(units[1].Count < 100);
        Assert.Equal(10, units[2].Count);
        Assert.Equal(100, units[3].Count);
    }

    [Fact]
    public void Le_colosse_balaye_tous_les_ennemis_voisins()
    {
        var defender = new Army();
        defender.AddStack(Creature("paysan"), 100);
        defender.AddStack(Creature("paysan"), 100);
        defender.AddStack(Creature("paysan"), 100);
        var battle = new TacticalBattle(ArmyOf("colosse-os", 1), defender, new GameRandom(5));
        battle.Field.Obstacles.Clear();
        var colossus = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var units = battle.Units.Where(u => u.Side == BattleSide.Defender).ToList();
        units[0].Position = new Hex(5, 4);
        units[1].Position = new Hex(4, 3); // voisins de (4, 4)
        units[2].Position = new Hex(8, 8); // loin
        battle.ForceTurn(colossus, new Hex(4, 4));

        battle.Attack(units[0], new Hex(4, 4));

        Assert.True(units[0].Count < 100);
        Assert.True(units[1].Count < 100);
        Assert.Equal(100, units[2].Count);
    }

    [Fact]
    public void Sur_la_carte_un_gardien_mort_vivant_a_la_force_d_un_gardien_neutre()
    {
        // un colosse vaut bien plus qu'un dragon : un gardien de colosses en compte moins
        var colossus = Creature("colosse-os");
        var dragon = Creature("dragon");
        Assert.True(CreatureStrength.PeasantsToBeat(colossus, TestData.Creatures) > 2 * CreatureStrength.PeasantsToBeat(dragon, TestData.Creatures));
        var guards = Enumerable.Range(1, 6).SelectMany(seed => new MapGenerator(seed, TestData.Data).Generate().Objects.OfType<MonsterGuard>()).ToList();
        Assert.Contains(guards, g => g.Type.Faction == "morts-vivants");
    }
}
