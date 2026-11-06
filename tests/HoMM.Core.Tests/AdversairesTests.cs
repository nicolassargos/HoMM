using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les adversaires (étape 8, point 1) : un départ et une ville proche chacun, les tours, la victoire.</summary>
public class AdversairesTests
{
    [Theory]
    [InlineData(MapSize.Tiny, 1)]
    [InlineData(MapSize.Small, 1)]
    [InlineData(MapSize.Normal, 2)]
    [InlineData(MapSize.Large, 3)]
    [InlineData(MapSize.Huge, 3)]
    public void Chaque_joueur_a_son_coin_et_une_ville_proche(MapSize size, int opponents)
    {
        // les grandes cartes sont longues à vérifier : une seule graine
        foreach (int seed in size >= MapSize.Large ? new[] { 1 } : new[] { 1, 42 })
        {
            var state = new GameState(seed, TestData.Data, size, opponents);
            var map = state.Map;
            Assert.Equal(opponents + 1, state.Players.Count);
            Assert.False(state.Players[0].IsComputer);
            Assert.All(state.Players.Skip(1), p => Assert.True(p.IsComputer));
            Assert.Equal(opponents + 1, map.Starts.Distinct().Count());
            for (int i = 0; i < state.Players.Count; i++)
                Assert.Equal(map.Starts[i], Assert.Single(state.Players[i].Heroes).Position);
            // tout reste atteignable depuis chaque départ
            foreach (var start in map.Starts)
                foreach (var obj in map.Objects.Where(o => o.Position != start)) // sauf son château
                    Assert.NotNull(Pathfinder.FindPath(map, start, obj.Position));
            // chaque départ a une ville plus proche de lui que de tout autre départ
            var towns = map.Objects.OfType<Town>().Where(t => !map.Starts.Contains(t.Position)).ToList(); // les villes neutres
            foreach (var start in map.Starts)
                Assert.Contains(towns, t => map.Starts.All(other => other == start
                    || Distance(t.Position, start) < Distance(t.Position, other)));
            // pas de monstre collé à un départ
            Assert.All(map.Objects.OfType<MonsterGuard>(), g => Assert.True(map.Starts.All(s =>
                Math.Max(Math.Abs(g.Position.X - s.X), Math.Abs(g.Position.Y - s.Y)) >= 4)));
        }
    }

    private static double Distance(Cell a, Cell b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    [Fact]
    public void Le_nombre_d_adversaires_depend_de_la_taille_de_la_carte()
    {
        Assert.Equal(2, new GameState(5, TestData.Data, MapSize.Tiny, opponents: 3).Players.Count);
        Assert.Single(new GameState(5, TestData.Data, MapSize.Normal).Players);
    }

    [Fact]
    public void Les_tours_passent_aux_adversaires_puis_un_nouveau_jour_commence()
    {
        var state = new GameState(3, TestData.Data, MapSize.Normal, opponents: 2);
        state.EndTurn();
        Assert.Equal(state.Players[1], state.CurrentPlayer);
        Assert.Equal(0, state.Day);
        ComputerPlayer.PlayTurn(state);
        state.EndTurn();
        Assert.Equal(state.Players[2], state.CurrentPlayer);
        state.EndTurn();
        Assert.Equal(state.Players[0], state.CurrentPlayer);
        Assert.Equal(1, state.Day);
    }

    [Fact]
    public void Vaincre_tous_les_adversaires_fait_gagner()
    {
        var state = new GameState(3, TestData.Data, MapSize.Normal, opponents: 1, startingTowns: false);
        Assert.True(state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents)));
        state.Players[1].Heroes.Clear(); // l'adversaire n'a ni héros ni ville
        state.EndTurn();
        Assert.Equal(GameOutcome.Victory, state.Outcome);
        Assert.Contains(state.TakeEvents(), e => e is GameEnded { Reason: GameEndReason.OpponentsDefeated });
    }

    [Fact]
    public void La_sauvegarde_garde_les_adversaires()
    {
        var state = new GameState(3, TestData.Data, MapSize.Large, opponents: 3);
        state.EndTurn();
        var loaded = GameState.Load(state.Save(), TestData.Data);
        Assert.Equal(state.Players.Select(p => (p.Name, p.IsComputer)), loaded.Players.Select(p => (p.Name, p.IsComputer)));
        Assert.Equal(state.CurrentPlayer.Id, loaded.CurrentPlayer.Id);
    }

    [Theory]
    [InlineData(MapSize.Normal, 0)]
    [InlineData(MapSize.Large, 3)]
    public void Chaque_joueur_commence_avec_son_chateau(MapSize size, int opponents)
    {
        var state = new GameState(9, TestData.Data, size, opponents);
        foreach (var player in state.Players)
        {
            var hero = Assert.Single(player.Heroes);
            var castle = Assert.Single(player.Towns);
            Assert.Equal(castle.Position, hero.Position); // le héros est à l'entrée de son château
            Assert.True(castle.Garrison.IsEmpty);
            Assert.True(state.Map.IsPassable(castle.Approach));
        }
        // dès le premier jour, l'adversaire construit et recrute dans son château
        if (opponents > 0)
        {
            state.EndTurn();
            var rival = state.CurrentPlayer;
            int army = rival.Heroes[0].Army.TotalCount;
            ComputerPlayer.PlayTurn(state);
            Assert.True(rival.Towns[0].BuiltBuildings.Count() > 2);
            Assert.True(rival.Heroes.Sum(h => h.Army.TotalCount) + rival.Towns.Sum(t => t.Garrison.TotalCount) > army);
        }
    }
}
