using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// La Tactique (décidée avec l'utilisateur, 2026-11-09) : le héros qui a le plus haut rang place ses troupes avant le
/// premier round, dans 2, 3 ou 4 colonnes selon son avance.
/// </summary>
public class TactiqueTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static Army Army(params (string Id, int Count)[] stacks)
    {
        var army = new Army();
        foreach (var (id, count) in stacks)
            army.Add(Creature(id), count);
        return army;
    }

    /// <summary>Un héros (Paladin) avec ce rang de Tactique.</summary>
    private static Hero Hero(GameState state, int rank)
    {
        var hero = state.CurrentHero!;
        if (rank > 0)
            hero.AddSkillRank(TestData.Skills.Get(Skills.Tactics), rank);
        return hero;
    }

    private static GameState Game() => new(5, TestData.Data, MapSize.Small, 0);

    [Fact]
    public void Sans_tactique_le_combat_commence_aussitot()
    {
        var battle = new TacticalBattle(Army(("paysan", 10)), Army(("gobelin", 10)), new GameRandom(1), Hero(Game(), 0));
        Assert.False(battle.IsPlacing);
        Assert.Equal(1, battle.Round);
        Assert.NotNull(battle.Active);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    public void Le_heros_place_ses_troupes_dans_ses_premieres_colonnes(int rank, int columns)
    {
        var battle = new TacticalBattle(Army(("paysan", 10), ("archer", 5)), Army(("gobelin", 10)), new GameRandom(1), Hero(Game(), rank));
        Assert.True(battle.IsPlacing);
        Assert.Equal(BattleSide.Attacker, battle.PlacementSide);
        Assert.Equal(columns, battle.PlacementColumns);
        Assert.Equal(0, battle.Round);
        Assert.Null(battle.Active);

        var peasants = battle.Units.First(u => u.Type.Id == "paysan");
        Assert.All(battle.PlacementHexes(peasants), h => Assert.True(h.Col < columns));
        var front = battle.PlacementHexes(peasants).First(h => h.Col == columns - 1);
        Assert.True(battle.Place(peasants, front));
        Assert.Equal(front, peasants.Position);
        Assert.False(battle.Place(peasants, new Hex(columns, front.Row))); // au-delà de la zone
        var enemy = battle.Units.First(u => u.Side == BattleSide.Defender);
        Assert.False(battle.CanPlace(enemy, new Hex(Battlefield.Columns - 2, enemy.Position.Row))); // pas les troupes de l'ennemi

        battle.EndPlacement();
        Assert.False(battle.IsPlacing);
        Assert.Equal(1, battle.Round);
        Assert.NotNull(battle.Active);
    }

    [Fact]
    public void On_ne_place_pas_une_troupe_sur_une_autre()
    {
        var battle = new TacticalBattle(Army(("paysan", 10), ("archer", 5)), Army(("gobelin", 10)), new GameRandom(1), Hero(Game(), 1));
        var peasants = battle.Units.First(u => u.Type.Id == "paysan");
        var archers = battle.Units.First(u => u.Type.Id == "archer");
        Assert.False(battle.CanPlace(peasants, archers.Position));
    }

    [Fact]
    public void Si_les_deux_heros_ont_la_tactique_seul_le_plus_avance_place_selon_son_avance()
    {
        var attacker = Hero(Game(), 1);
        var other = Game();
        var defender = Hero(other, 3);
        var battle = new TacticalBattle(Army(("paysan", 10)), Army(("gobelin", 10)), new GameRandom(1), attacker, defenderHero: defender);
        Assert.Equal(BattleSide.Defender, battle.PlacementSide);
        Assert.Equal(3, battle.PlacementColumns); // deux rangs d'avance
        var goblins = battle.Units.First(u => u.Side == BattleSide.Defender);
        Assert.All(battle.PlacementHexes(goblins), h => Assert.True(h.Col >= Battlefield.Columns - 3));

        var even = new TacticalBattle(Army(("paysan", 10)), Army(("gobelin", 10)), new GameRandom(1), Hero(Game(), 2), defenderHero: Hero(Game(), 2));
        Assert.False(even.IsPlacing);
    }

    [Fact]
    public void L_ordinateur_avance_ses_troupes_de_corps_a_corps_et_garde_ses_tireurs_derriere()
    {
        var battle = new TacticalBattle(Army(("paysan", 10), ("archer", 5)), Army(("gobelin", 10)), new GameRandom(1), Hero(Game(), 3));
        var archers = battle.Units.First(u => u.Type.Id == "archer");
        var before = archers.Position;

        BattleAI.PlaceTroops(battle);

        Assert.False(battle.IsPlacing);
        Assert.Equal(1, battle.Round);
        Assert.Equal(3, battle.Units.First(u => u.Type.Id == "paysan").Position.Col);
        Assert.Equal(before, archers.Position);
    }

    [Fact]
    public void Le_combat_rapide_place_les_troupes_puis_se_joue()
    {
        var army = Army(("paysan", 50));
        var report = Battle.Resolve(army, Army(("gobelin", 5)), Hero(Game(), 2));
        Assert.True(report.AttackerWon);
    }
}
