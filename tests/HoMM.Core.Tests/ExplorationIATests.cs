using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>L'adversaire explore et ramasse (étape 8, point 2), sans combat et sans voir ce qu'il n'a pas exploré.</summary>
public class ExplorationIATests
{
    /// <summary>Une partie à un adversaire, à son tour (le joueur a fini le sien).</summary>
    private static (GameState State, Hero Rival) RivalTurn(int seed = 4)
    {
        var state = new GameState(seed, TestData.Data, MapSize.Tiny, opponents: 1, startingTowns: false);
        state.EndTurn();
        Assert.True(state.CurrentPlayer.IsComputer);
        return (state, state.CurrentPlayer.Heroes[0]);
    }

    /// <summary>Une case libre à 2 ou 3 pas du héros, connue de son joueur, hors de toute zone de monstre.</summary>
    private static Cell FreeCellNear(GameState state, Hero hero) =>
        Enumerable.Range(-3, 7).SelectMany(dy => Enumerable.Range(-3, 7).Select(dx => hero.Position.Offset(dx, dy)))
            .Where(c => Math.Max(Math.Abs(c.X - hero.Position.X), Math.Abs(c.Y - hero.Position.Y)) >= 2)
            .First(c => state.Map.IsPassable(c) && state.Map.ObjectAt(c) == null && !state.Map.IsGuarded(c)
                && hero.Owner.Fog.IsExplored(c) && Pathfinder.FindPath(state.Map, hero.Position, c) is { } p && !p.Any(state.Map.IsGuarded));

    [Fact]
    public void L_adversaire_ramasse_un_tas_proche()
    {
        var (state, rival) = RivalTurn();
        // seul ce tas l'intéresse (pas une ville, un monstre, un lieu ni un autre objet de la carte)
        foreach (var obj in state.Map.Objects.ToList())
            state.Map.RemoveObject(obj);
        var cell = FreeCellNear(state, rival);
        var pile = new ResourcePile(cell, ResourceType.Gold, 700);
        state.Map.AddObject(pile);
        int gold = rival.Owner.Resources[ResourceType.Gold];

        ComputerPlayer.PlayTurn(state);

        Assert.DoesNotContain(pile, state.Map.Objects);
        Assert.Equal(gold + 700, rival.Owner.Resources[ResourceType.Gold]);
    }

    [Fact]
    public void L_adversaire_laisse_un_tas_garde_par_un_monstre()
    {
        var (state, rival) = RivalTurn();
        // rien d'autre à prendre ni à combattre : il ne resterait que le tas gardé
        foreach (var obj in state.Map.Objects.Where(o => o is ResourcePile or TreasureChest or Mine or Obelisk or MonsterGuard or Town).ToList())
            state.Map.RemoveObject(obj);
        var cell = FreeCellNear(state, rival);
        var pile = new ResourcePile(cell, ResourceType.Gold, 700);
        state.Map.AddObject(pile);
        var guardCell = new[] { cell.Offset(1, 0), cell.Offset(-1, 0), cell.Offset(0, 1), cell.Offset(0, -1) }
            .First(c => state.Map.IsPassable(c) && state.Map.ObjectAt(c) == null && c != rival.Position);
        state.Map.AddObject(new MonsterGuard(guardCell, TestData.Creatures.Get("troll"), 20)); // trop fort pour lui
        int army = rival.Army.Stacks.Sum(s => s.Count);

        ComputerPlayer.PlayTurn(state);

        Assert.Contains(pile, state.Map.Objects);
        Assert.Equal(army, rival.Army.Stacks.Sum(s => s.Count)); // pas de combat
    }

    [Fact]
    public void Sans_rien_a_prendre_l_adversaire_explore()
    {
        var (state, rival) = RivalTurn();
        foreach (var obj in state.Map.Objects.Where(o => o is ResourcePile or TreasureChest or Mine or Obelisk).ToList())
            state.Map.RemoveObject(obj);
        int Explored() => Enumerable.Range(0, state.Map.Height)
            .Sum(y => Enumerable.Range(0, state.Map.Width).Count(x => rival.Owner.Fog.IsExplored(new Cell(x, y))));
        int before = Explored();
        var start = rival.Position;

        ComputerPlayer.PlayTurn(state);

        Assert.NotEqual(start, rival.Position);
        Assert.True(Explored() > before);
    }

    [Fact]
    public void Trois_semaines_avec_deux_adversaires_sans_accroc()
    {
        var state = new GameState(11, TestData.Data, 54, 40, opponents: 2);
        // en Facile, les adversaires laissent le joueur (qui ne bouge pas) tranquille les trois premières semaines
        state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents, Difficulty: Difficulty.Easy));
        var startGold = state.Opponents.Select(p => p.Resources[ResourceType.Gold]).ToList();
        for (int day = 0; day < 21 && state.Outcome == GameOutcome.Playing; day++)
        {
            state.EndTurn();
            while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
            {
                ComputerPlayer.PlayTurn(state);
                state.EndTurn();
            }
        }
        Assert.Equal(21, state.Day);
        // les adversaires peuvent s'éliminer entre eux (point 5) : on regarde ceux qui restent
        var rivals = state.Opponents.ToList();
        // l'un d'eux au moins joue encore : un héros, ou une ville où en engager un (un héros peut tomber dans un combat
        // malchanceux : la prévision de l'IA, jouée trois fois, ne voit pas toujours venir un combat qui tourne mal)
        Assert.Contains(rivals, p => p.Heroes.Count > 0 || p.Towns.Any(t => t.Buildings.Any(b => b.Tavern && t.Has(b.Id))));
        // chacun a fait quelque chose : un combat gagné, une mine ou une ville prise, ou de l'or ramassé
        Assert.All(rivals.Select((p, i) => (p, i)), x => Assert.True(x.p.Statistics.BattlesWon > 0 || x.p.Mines.Count > 0
            || x.p.Towns.Count > 0 || x.p.Resources[ResourceType.Gold] > startGold[x.i]));
    }
}
