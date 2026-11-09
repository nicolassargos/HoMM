using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les compétences des héros (étape 9) : les points, les arbres, et ce que fait chaque compétence.</summary>
public class CompetencesTests
{
    private static SkillType Skill(string id) => TestData.Skills.Get(id);

    /// <summary>Une partie sans adversaire : le héros du joueur (un Paladin, Commandement au départ).</summary>
    private static (GameState State, Hero Hero) Game(int points = 0)
    {
        var state = new GameState(5, TestData.Data, MapSize.Small, 0);
        var hero = state.CurrentHero!;
        hero.SkillPoints = points;
        return (state, hero);
    }

    /// <summary>Donne ce rang au héros dans cette compétence (sans points).</summary>
    private static void Give(Hero hero, string id, int rank) => hero.AddSkillRank(Skill(id), rank - hero.SkillRank(id));

    [Fact]
    public void Chaque_niveau_donne_un_point_de_competence()
    {
        var (state, hero) = Game();
        state.GrantExperience(hero, Hero.ExperienceForLevel(2));

        state.ChooseStat(state.PendingChoices.Single(), HeroStat.Attack);

        Assert.Equal(1, hero.SkillPoints);
    }

    [Fact]
    public void La_classe_donne_sa_competence_de_depart_au_rang_de_base()
    {
        var (_, hero) = Game();
        Assert.Equal("paladin", hero.Class?.Id);
        Assert.Equal(1, hero.SkillRank("commandement"));
        Assert.Equal(0, hero.SkillPoints);
        foreach (var c in TestData.Data.Classes.All)
            Assert.NotNull(TestData.Skills.Find(c.Skill!));
    }

    [Fact]
    public void Un_point_apprend_une_competence_puis_la_fait_monter_jusqu_a_expert()
    {
        var (state, hero) = Game(points: 4);
        var logistics = Skill("logistique");

        for (int rank = 1; rank <= 3; rank++)
        {
            Assert.Equal(LearnSkillResult.Ok, state.LearnSkill(hero, logistics));
            Assert.Equal(rank, hero.SkillRank("logistique"));
        }
        Assert.Equal(LearnSkillResult.MaxRank, state.LearnSkill(hero, logistics));
        Assert.Equal(1, hero.SkillPoints);
        Assert.Equal(3, state.TakeEvents().OfType<SkillLearned>().Count());
    }

    [Fact]
    public void Sans_point_on_n_apprend_rien()
    {
        var (state, hero) = Game(points: 0);
        Assert.Equal(LearnSkillResult.NoPoint, state.LearnSkill(hero, Skill("logistique")));
    }

    [Fact]
    public void La_deuxieme_rangee_s_ouvre_apres_trois_points_dans_l_arbre()
    {
        var (state, hero) = Game(points: 10);
        var estates = Skill("intendance"); // rangée 2 de l'Aventure

        Assert.Equal(LearnSkillResult.RowLocked, state.LearnSkill(hero, estates));
        state.LearnSkill(hero, Skill("logistique"));
        state.LearnSkill(hero, Skill("logistique"));
        Assert.Equal(LearnSkillResult.RowLocked, state.LearnSkill(hero, estates));
        state.LearnSkill(hero, Skill("eclaireur"));

        Assert.Equal(LearnSkillResult.Ok, state.LearnSkill(hero, estates));
        // les points d'un autre arbre ne comptent pas : le Commandement de départ n'ouvre pas la Magie
        Assert.Equal(LearnSkillResult.RowLocked, state.LearnSkill(hero, Skill("ecole-feu")));
    }

    [Fact]
    public void Six_competences_au_plus()
    {
        var (state, hero) = Game(points: 20);
        foreach (var id in new[] { "logistique", "eclaireur", "discipline", "mysticisme", "ecole-eau" })
            Assert.Equal(LearnSkillResult.Ok, state.LearnSkill(hero, Skill(id)));
        Assert.Equal(6, hero.Skills.Count); // avec le Commandement de départ

        state.LearnSkill(hero, Skill("logistique"));
        state.LearnSkill(hero, Skill("logistique"));
        Assert.Equal(LearnSkillResult.TooManySkills, state.LearnSkill(hero, Skill("orientation")));
        Assert.Equal(LearnSkillResult.Ok, state.LearnSkill(hero, Skill("eclaireur"))); // un rang de plus, si
    }

    // ------------------------------------------------------------------ les effets

    [Fact]
    public void Logistique_et_eclaireur()
    {
        var (_, hero) = Game();
        int movement = hero.MaxMovement, vision = hero.VisionRadius;

        Give(hero, "logistique", 2);
        Give(hero, "eclaireur", 3);

        Assert.Equal(movement * 120 / 100, hero.MaxMovement);
        Assert.Equal(vision + 6, hero.VisionRadius);
    }

    [Fact]
    public void Orientation_reduit_le_surcout_des_terrains_difficiles()
    {
        var map = new AdventureMap(5, 5);
        map.SetTerrain(new Cell(1, 1), TerrainType.Swamp); // 175 : 75 de plus que l'herbe

        Assert.Equal(175, Pathfinder.StepCost(map, new Cell(1, 1), new Cell(2, 1)));
        Assert.Equal(100 + 75 / 2, Pathfinder.StepCost(map, new Cell(1, 1), new Cell(2, 1), roughReduction: 50));
        Assert.Equal(100, Pathfinder.StepCost(map, new Cell(2, 2), new Cell(3, 2), roughReduction: 50)); // l'herbe ne change pas
    }

    [Fact]
    public void Intendance_rapporte_de_l_or_chaque_jour()
    {
        var (state, hero) = Game();
        var player = state.Players[0];
        int before = player.DailyIncome()[ResourceType.Gold];

        Give(hero, "intendance", 3);

        Assert.Equal(before + 200, player.DailyIncome()[ResourceType.Gold]);
    }

    [Fact]
    public void Commandement_et_discipline()
    {
        var (_, hero) = Game();
        var peasant = TestData.Creatures.Get("paysan");
        Give(hero, "commandement", 0 + 1); // le rang de base de la classe
        Assert.Equal(Command.Limit(hero.Charisma) * 110 / 100, hero.CommandLimit);

        int count = 2 * hero.CommandLimit; // deux fois la limite : 50 % sans Discipline
        Assert.Equal(0.5, hero.RevoltChance(peasant, count), 3);
        Give(hero, "discipline", 2);
        Assert.Equal(0.25, hero.RevoltChance(peasant, count), 3);
    }

    [Fact]
    public void Recruteur_fait_grandir_la_ville_ou_le_heros_commence_la_semaine()
    {
        var (state, hero) = Game();
        var town = state.Players[0].Towns.Single();
        var peasant = TestData.Creatures.Get("paysan");
        hero.Position = town.Position;
        Give(hero, "recruteur", 3);
        town.Available[peasant] = 0;

        for (int day = 0; day < 7; day++)
            state.EndTurn();

        Assert.Equal(peasant.Growth * 130 / 100, town.Available[peasant]);
    }

    [Fact]
    public void Inspiration_rend_les_troupes_plus_rapides()
    {
        var (_, hero) = Game();
        Give(hero, "inspiration", 3);
        var army = new Army();
        army.Add(TestData.Creatures.Get("paysan"), 10);
        var enemy = new Army();
        enemy.Add(TestData.Creatures.Get("paysan"), 10);
        var battle = new TacticalBattle(army, enemy, new GameRandom(1), hero);
        var ours = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var theirs = battle.Units.Single(u => u.Side == BattleSide.Defender);

        Assert.Equal(theirs.Type.Speed + 2, battle.SpeedOf(ours)); // premier round
        Assert.Equal(theirs.Type.Speed, battle.SpeedOf(theirs));
    }

    [Fact]
    public void Mysticisme_rend_plus_de_points_de_sort_chaque_jour()
    {
        var (_, hero) = Game();
        hero.Knowledge = 5;
        hero.SpellPoints = 0;
        Give(hero, "mysticisme", 2);

        hero.NewDay();

        Assert.Equal(3, hero.SpellPoints);
    }

    [Fact]
    public void Une_ecole_de_magie_rend_ses_sorts_moins_chers_et_plus_forts()
    {
        var (_, hero) = Game();
        var bolt = TestData.Spells.Get("eclair"); // air
        var fireball = TestData.Spells.Get("boule-de-feu"); // feu
        Give(hero, "ecole-air", 2);

        Assert.Equal((int)System.Math.Round(bolt.Cost * 0.7), hero.SpellCost(bolt));
        Assert.Equal(30, hero.SpellBonus(bolt));
        Assert.Equal(fireball.Cost, hero.SpellCost(fireball));
        Assert.Equal(0, hero.SpellBonus(fireball));
        Assert.All(TestData.Spells.All, s => Assert.NotNull(s.School));
    }

    [Fact]
    public void Les_competences_et_les_points_restent_apres_une_sauvegarde()
    {
        var (state, hero) = Game(points: 2);
        state.LearnSkill(hero, Skill("logistique"));
        int movement = hero.MaxMovement;

        var loaded = GameState.Load(state.Save(), TestData.Data);
        var again = loaded.Players[0].Heroes.Single();

        Assert.Equal(1, again.SkillPoints);
        Assert.Equal(1, again.SkillRank("logistique"));
        Assert.Equal(1, again.SkillRank("commandement"));
        Assert.Equal(movement, again.MaxMovement); // la Logistique n'est pas comptée deux fois
    }

    [Fact]
    public void L_IA_depense_ses_points_en_commencant_par_la_competence_de_sa_classe()
    {
        var (state, hero) = Game(points: 3);

        ComputerPlayer.SpendSkillPoints(state, hero);

        Assert.Equal(0, hero.SkillPoints);
        Assert.Equal(3, hero.SkillRank("commandement")); // de base à expert, puis…
        Assert.Equal(1, hero.SkillRank("logistique"));
    }
}
