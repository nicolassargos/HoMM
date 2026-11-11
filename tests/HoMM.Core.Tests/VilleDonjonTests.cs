using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Le Donjon jouable (étape 2, 2026-11-11) : ses habitations, la classe Seigneur des abîmes et son talent Effroi, ses
/// cinq héros.
/// </summary>
public class VilleDonjonTests
{
    private const string Dungeon = "donjon";

    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static GameState DungeonGame(int seed = 5) => new(seed, TestData.Data, MapSize.Small, 0, faction: Dungeon);

    private static Army Army(string id, int count)
    {
        var army = new Army();
        army.Add(Creature(id), count);
        return army;
    }

    [Fact]
    public void Un_joueur_du_donjon_commence_avec_un_seigneur_des_abimes()
    {
        var state = DungeonGame();
        var hero = state.Players[0].Heroes.Single();
        var castle = state.Players[0].Towns.Single();

        Assert.Equal(Dungeon, castle.Faction);
        Assert.True(castle.Has("bourbier-lezards"));
        Assert.Equal("seigneur-abimes", hero.Class?.Id);
        Assert.Equal((1, 1, 3, 2, 9), (hero.Attack, hero.Defense, hero.Power, hero.Knowledge, hero.Charisma));
        Assert.Equal(new[] { "fleche-magique", "malediction" }, hero.Spells.Select(s => s.Id));
        Assert.Equal(1, hero.SkillRank("ecole-feu"));
        Assert.Equal(new[] { ("homme-lezard", 15), ("homme-grenouille", 7), ("minotaure", 2) }, hero.Army.Stacks.Select(s => (s.Type.Id, s.Count)));
        Assert.Equal(Dungeon, TestData.Data.Heroes.All.Single(h => h.Name == hero.Name).Faction);
    }

    [Fact]
    public void Le_donjon_a_cinq_heros()
    {
        Assert.Equal(new[] { "corvin-le-maudit", "ysolde-la-noire", "malachar", "dame-sombreval", "dame-morgane" },
            TestData.Data.Heroes.All.Where(h => h.Faction == Dungeon).Select(h => h.Id));
    }

    [Fact]
    public void La_ville_du_donjon_a_ses_sept_habitations()
    {
        var castle = DungeonGame().Players[0].Towns.Single();
        Assert.Equal(new[] { "bourbier-lezards", "mare-grenouilles", "labyrinthe", "fosse-aberrations", "galeries-vers", "chambre-tyrans", "gouffre-hydre" },
            Enumerable.Range(1, 7).Select(t => castle.DwellingOfTier(t)!.Id));
        var b = TestData.Data.Buildings;
        Assert.Equal(2, b.Get("galeries-vers").Cost[ResourceType.Mercury]);
        Assert.Equal(2, b.Get("chambre-tyrans").Cost[ResourceType.Mercury]);
        Assert.Equal(5, b.Get("gouffre-hydre").Cost[ResourceType.Sulfur]);
        Assert.Contains(b.Get("grand-hotel-ville").Requires, r => r.Split('|').Contains("fosse-aberrations"));
    }

    [Fact]
    public void L_effroi_baisse_le_moral_de_l_armee_ennemie_selon_le_niveau()
    {
        var hero = DungeonGame().Players[0].Heroes.Single();
        int MoraleOf(string enemy)
        {
            var battle = new TacticalBattle(hero.Army, Army(enemy, 20), new GameRandom(1), hero);
            return battle.Units.First(u => u.Side == BattleSide.Defender).Morale;
        }

        hero.Level = 1;
        Assert.Equal(-1, MoraleOf("paysan"));
        hero.Level = 8;
        Assert.Equal(-2, MoraleOf("paysan"));
        hero.Level = 24;
        Assert.Equal(-3, MoraleOf("paysan")); // jamais sous −3
        Assert.Equal(0, MoraleOf("squelette")); // rien n'effraie les morts-vivants

        var paladin = new GameState(5, TestData.Data, MapSize.Small, 0).Players[0].Heroes.Single();
        var plain = new TacticalBattle(paladin.Army, Army("paysan", 20), new GameRandom(1), paladin);
        Assert.Equal(0, plain.Units.First(u => u.Side == BattleSide.Defender).Morale);
    }

    [Fact]
    public void Un_adversaire_du_donjon_batit_ses_habitations()
    {
        var state = new GameState(7, TestData.Data, MapSize.Normal, opponents: 1, opponentFactions: new[] { Dungeon });
        state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
        state.Players[0].Heroes[0].Army.Add(Creature("ange"), 50);
        for (int day = 0; day < 14; day++)
        {
            state.EndTurn();
            while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
            {
                ComputerPlayer.PlayTurn(state);
                state.EndTurn();
            }
        }
        var rival = state.Players[1];
        Assert.Equal("seigneur-abimes", rival.Heroes[0].Class?.Id);
        var castle = rival.Towns.First(t => t.Faction == Dungeon);
        Assert.True(castle.Has("mare-grenouilles"));
    }
}
