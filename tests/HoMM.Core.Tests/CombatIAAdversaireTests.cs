using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>L'adversaire combat (étape 8, point 3) : les monstres et les villes neutres qu'il bat sans trop de pertes.</summary>
public class CombatIAAdversaireTests
{
    /// <summary>Une partie à un adversaire, à son tour, sans rien à ramasser (pour qu'il ne fasse que ce qu'on teste).</summary>
    private static (GameState State, Hero Rival) RivalTurn()
    {
        var state = new GameState(4, TestData.Data, MapSize.Tiny, opponents: 1, startingTowns: false);
        state.EndTurn();
        foreach (var obj in state.Map.Objects.Where(o => o is ResourcePile or TreasureChest or Mine or Obelisk or MonsterGuard or Town).ToList())
            state.Map.RemoveObject(obj);
        return (state, state.CurrentPlayer.Heroes[0]);
    }

    /// <summary>Une case libre, connue, à 3 pas environ du héros, et ses abords libres (pour un monstre ou une ville).</summary>
    private static Cell FreeCellNear(GameState state, Hero hero, int distance) =>
        Enumerable.Range(-distance, 2 * distance + 1).SelectMany(dy => Enumerable.Range(-distance, 2 * distance + 1).Select(dx => hero.Position.Offset(dx, dy)))
            .Where(c => Math.Max(Math.Abs(c.X - hero.Position.X), Math.Abs(c.Y - hero.Position.Y)) == distance)
            .First(c => state.Map.IsPassable(c) && state.Map.ObjectAt(c) == null && hero.Owner.Fog.IsExplored(c)
                && Pathfinder.FindPath(state.Map, hero.Position, c) != null);

    [Fact]
    public void L_adversaire_bat_un_monstre_faible()
    {
        var (state, rival) = RivalTurn();
        var guard = new MonsterGuard(FreeCellNear(state, rival, 3), TestData.Creatures.Get("gobelin"), 3);
        state.Map.AddObject(guard);

        ComputerPlayer.PlayTurn(state);

        Assert.DoesNotContain(guard, state.Map.Objects);
        Assert.Equal(1, rival.Owner.Statistics.BattlesWon);
    }

    [Fact]
    public void L_adversaire_evite_un_monstre_trop_fort()
    {
        var (state, rival) = RivalTurn();
        var guard = new MonsterGuard(FreeCellNear(state, rival, 3), TestData.Creatures.Get("dragon"), 5);
        state.Map.AddObject(guard);
        int army = rival.Army.TotalCount;

        ComputerPlayer.PlayTurn(state);

        Assert.Contains(guard, state.Map.Objects);
        Assert.Equal(army, rival.Army.TotalCount);
    }

    [Fact]
    public void L_adversaire_prend_une_ville_neutre_mal_defendue()
    {
        var (state, rival) = RivalTurn();
        // une ville dont l'entrée est à 3 pas, avec une petite garnison
        var town = Enumerable.Range(2, 4).SelectMany(d => new[] { rival.Position.Offset(0, d), rival.Position.Offset(d, 0), rival.Position.Offset(-d, 0) })
            .Select(c => new Town(c, "Testville", "chateau", TestData.Data))
            .First(t => t.Footprint.All(f => state.Map.InBounds(f) && state.Map.IsPassable(f) && state.Map.ObjectAt(f) == null && f != rival.Position)
                && rival.Owner.Fog.IsExplored(t.Position) && Pathfinder.FindPath(state.Map, rival.Position, t.Approach) != null);
        town.AddBuilding(TestData.Data.Buildings.Get("salle-village"));
        town.Garrison.Add(TestData.Creatures.Get("paysan"), 5);
        state.Map.AddObject(town);

        ComputerPlayer.PlayTurn(state);

        Assert.Equal(rival.Owner, town.Owner);
        Assert.Contains(town, rival.Owner.Towns);
    }

    [Fact]
    public void Un_mois_avec_deux_adversaires_qui_combattent()
    {
        var state = new GameState(11, TestData.Data, MapSize.Normal, opponents: 2);
        for (int day = 0; day < 28 && state.Outcome == GameOutcome.Playing; day++)
        {
            state.EndTurn();
            while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
            {
                ComputerPlayer.PlayTurn(state);
                state.EndTurn();
            }
        }
        // le joueur de ce test ne bouge pas : il peut être éliminé avant la fin du mois (la partie s'arrête)
        Assert.Contains(state.Opponents, p => p.Statistics.BattlesWon > 0);
        // les adversaires s'attaquent aussi entre eux (point 5) : l'un peut être éliminé, pas tous
        Assert.Contains(state.Opponents, p => !p.IsDefeated);
    }
}
