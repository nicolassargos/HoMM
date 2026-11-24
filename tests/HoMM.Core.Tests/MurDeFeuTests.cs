using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Le mur de feu (idée de l'utilisateur, 2026-11-24) : 5 cases du haut vers le bas, 3 rounds ; il brûle les troupes qui
/// s'y trouvent quand il se dresse, puis celles qui y entrent ou le traversent ; les créatures de l'Enfer n'y craignent rien.
/// </summary>
public class MurDeFeuTests
{
    private static SpellType Wall => TestData.Spells.Get("mur-de-feu");

    /// <summary>Un héros de puissance 1 qui connaît le mur de feu : 10 + 5 × 1 = 15 dégâts.</summary>
    private static Hero Mage()
    {
        var map = new AdventureMap(5, 5);
        var hero = new Hero("Merlin", new Player("Rouge", map), new Cell(1, 1)) { Power = 1, Knowledge = 2 };
        hero.SpellPoints = hero.MaxSpellPoints;
        hero.Learn(Wall);
        return hero;
    }

    /// <summary>100 paysans au héros ; en face, ces troupes. Le champ sans obstacle, au tour des paysans, en <paramref name="at"/>.</summary>
    private static (TacticalBattle Battle, BattleUnit Ours) Fight(Hex at, params (string Id, int Count)[] theirs)
    {
        var attacker = new Army();
        attacker.Add(TestData.Creatures.Get("paysan"), 100);
        var defender = new Army();
        foreach (var (id, count) in theirs)
            defender.AddStack(TestData.Creatures.Get(id), count);
        var battle = new TacticalBattle(attacker, defender, new GameRandom(1), Mage());
        battle.Field.Obstacles.Clear();
        var ours = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var enemies = battle.Units.Where(u => u.Side == BattleSide.Defender).ToList();
        for (int i = 0; i < enemies.Count; i++)
            enemies[i].Position = new Hex(10, i * 2);
        battle.ForceTurn(ours, at);
        return (battle, ours);
    }

    [Fact]
    public void Le_mur_couvre_cinq_cases_de_la_colonne_sans_les_obstacles()
    {
        var (battle, _) = Fight(new Hex(1, 4), ("paysan", 1));
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, battle.WallCells(new Hex(4, 4)).Select(h => h.Row));
        Assert.All(battle.WallCells(new Hex(4, 4)), h => Assert.Equal(4, h.Col));
        Assert.Equal(new[] { 0, 1, 2 }, battle.WallCells(new Hex(4, 0)).Select(h => h.Row)); // coupé au bord du champ
        battle.Field.Obstacles.Add(new Hex(4, 3));
        Assert.Equal(4, battle.WallCells(new Hex(4, 4)).Count);
    }

    [Fact]
    public void Il_brule_ceux_qui_s_y_trouvent_sauf_les_creatures_de_l_enfer()
    {
        var (battle, _) = Fight(new Hex(1, 4), ("paysan", 100), ("cultiste", 10), ("paysan", 100));
        var enemies = battle.Units.Where(u => u.Side == BattleSide.Defender).ToList();
        enemies[0].Position = new Hex(4, 4);
        enemies[1].Position = new Hex(4, 5);
        enemies[2].Position = new Hex(6, 4); // à côté du mur

        Assert.True(battle.Cast(Wall, new Hex(4, 4)));

        Assert.Equal(85, enemies[0].Count);
        Assert.Equal(10, enemies[1].Count);
        Assert.Equal(enemies[1].Type.HitPoints, enemies[1].TopHitPoints);
        Assert.Equal(100, enemies[2].Count);
        Assert.Equal(5, battle.Fire.Count);
    }

    [Fact]
    public void Qui_le_traverse_brule_une_fois()
    {
        var (battle, ours) = Fight(new Hex(3, 4), ("paysan", 1));
        foreach (int row in new[] { 0, 1, 7, 8 })
            battle.Field.Obstacles.Add(new Hex(4, row)); // pas de détour : il faut passer dans le feu
        Assert.True(battle.Cast(Wall, new Hex(4, 4)));

        Assert.True(battle.Move(new Hex(5, 4)));

        Assert.Equal(85, ours.Count);
    }

    [Fact]
    public void Le_chemin_contourne_le_feu_quand_il_peut()
    {
        var (battle, ours) = Fight(new Hex(3, 6), ("paysan", 1));
        Assert.True(battle.Cast(Wall, new Hex(4, 4))); // rangées 2 à 6

        var path = battle.Reachable(ours)[new Hex(5, 6)];
        Assert.DoesNotContain(path, battle.Fire.ContainsKey);
        Assert.True(battle.Move(new Hex(5, 6)));
        Assert.Equal(100, ours.Count);
    }

    [Fact]
    public void Un_volant_ne_brule_que_s_il_se_pose_dedans()
    {
        var attacker = new Army();
        attacker.Add(TestData.Creatures.Get("griffon"), 10);
        var defender = new Army();
        defender.Add(TestData.Creatures.Get("paysan"), 1);
        var battle = new TacticalBattle(attacker, defender, new GameRandom(1), Mage());
        battle.Field.Obstacles.Clear();
        var griffins = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        battle.Units.Single(u => u.Side == BattleSide.Defender).Position = new Hex(10, 0);
        battle.ForceTurn(griffins, new Hex(2, 4));
        Assert.True(battle.Cast(Wall, new Hex(4, 4)));
        int full = griffins.TotalHitPoints;

        Assert.True(battle.Move(new Hex(6, 4))); // par-dessus le mur
        Assert.Equal(full, griffins.TotalHitPoints);

        battle.ForceTurn(griffins);
        Assert.True(battle.Move(new Hex(4, 3))); // dedans
        Assert.Equal(full - 15, griffins.TotalHitPoints);
    }

    [Fact]
    public void Il_brule_trois_rounds()
    {
        var (battle, _) = Fight(new Hex(1, 4), ("paysan", 1));
        Assert.True(battle.Cast(Wall, new Hex(4, 4)));
        int cast = battle.Round;
        while (battle.Round < cast + 2)
            Assert.True(battle.Defend());
        Assert.Equal(5, battle.Fire.Count);
        while (battle.Round < cast + 3)
            Assert.True(battle.Defend());
        Assert.Empty(battle.Fire);
    }

    [Fact]
    public void L_ia_le_dresse_sur_les_ennemis()
    {
        var (battle, _) = Fight(new Hex(1, 4), ("paysan", 100), ("paysan", 100));
        var enemies = battle.Units.Where(u => u.Side == BattleSide.Defender).ToList();
        enemies[0].Position = new Hex(6, 3);
        enemies[1].Position = new Hex(6, 5);

        var choice = BattleAI.ChooseSpell(battle);

        Assert.NotNull(choice);
        Assert.Equal("mur-de-feu", choice.Value.Spell.Id);
        Assert.Equal(6, choice.Value.Target.Col);
    }
}
