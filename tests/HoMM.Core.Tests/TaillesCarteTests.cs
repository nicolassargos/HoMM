using System;
using System.Linq;
using HoMM.Core;
using Xunit;
using Xunit.Abstractions;

namespace HoMM.Core.Tests;

/// <summary>Les cinq tailles de carte (nouvelle partie, étape 7) : chacune se génère, se joue et se gagne.</summary>
public class TaillesCarteTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(MapSize.Tiny)]
    [InlineData(MapSize.Small)]
    [InlineData(MapSize.Normal)]
    [InlineData(MapSize.Large)]
    [InlineData(MapSize.Huge)]
    public void Chaque_taille_donne_une_carte_complete(MapSize size)
    {
        foreach (int seed in new[] { 1, 42 })
        {
            var state = new GameState(seed, TestData.Data, size);
            var map = state.Map;
            Assert.Equal(MapSizes.Dimensions(size), (map.Width, map.Height));
            Assert.Equal(size, MapSizes.Closest(map.Width, map.Height));
            Assert.Contains(map.Objects, o => o is Town);
            Assert.Contains(map.Objects, o => o is Obelisk);
            Assert.NotNull(map.Treasure);
            // les objets se rejoignent (sauf le château de départ) : un seul parcours depuis le départ, comme le Pathfinder
            // (un chemin par objet prenait près d'une minute sur les grandes cartes)
            var reachable = Reachable(map);
            foreach (var obj in map.Objects.Where(o => o.Position != map.Start))
                Assert.True(reachable.Contains(obj.Position), $"{obj.GetType().Name} hors d'atteinte en {obj.Position}");
        }
    }

    /// <summary>Les cases qu'un héros atteint depuis le départ, comme le <see cref="Pathfinder"/> : en diagonale sans couper un coin bloqué, en s'arrêtant sur un objet.</summary>
    internal static System.Collections.Generic.HashSet<Cell> Reachable(AdventureMap map, Cell? from = null)
    {
        var start = from ?? map.Start;
        var seen = new System.Collections.Generic.HashSet<Cell> { start };
        var queue = new System.Collections.Generic.Queue<Cell>(seen);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c != start && map.ObjectAt(c) != null)
                continue;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var next = c.Offset(dx, dy);
                if (!map.IsPassable(next) || (dx != 0 && dy != 0 && !(map.IsPassable(c.Offset(dx, 0)) && map.IsPassable(c.Offset(0, dy)))))
                    continue;
                if (seen.Add(next))
                    queue.Enqueue(next);
            }
        }
        return seen;
    }

    /// <summary>
    /// La simulation d'équilibrage (optimiste, un seul héros) gagne sur les petites cartes (prendre toutes les villes) ;
    /// sur les grandes, elle prend au moins une ville, sans forcément toutes en 400 jours (depuis les tailles de Heroes 3 :
    /// la normale, 72 × 72, vers le jour 210 ; la grande, pas en 400 jours : ses villes lointaines sont trop gardées pour
    /// un seul héros qui ne voyage pas).
    /// </summary>
    [Theory]
    [InlineData(MapSize.Tiny, true)]
    [InlineData(MapSize.Small, true)]
    [InlineData(MapSize.Normal, false)]
    public void Chaque_taille_se_joue(MapSize size, bool mustWin)
    {
        var seeds = mustWin ? new[] { 1, 7, 42 } : new[] { 1 }; // les grandes cartes sont longues à simuler
        var days = seeds.Select(seed => BalanceSimulation.Play(seed, size)).ToList();
        output.WriteLine($"{size} : victoire aux jours {string.Join(", ", days.Select(d => d.VictoryDay))}");
        Assert.All(days, d => Assert.True(d.TownTaken, $"{size} : aucune ville prise"));
        if (mustWin)
            Assert.All(days, d => Assert.True(d.VictoryDay > 0, $"{size} : pas de victoire en 200 jours"));
    }

    [Fact]
    public void Le_resume_d_une_sauvegarde_donne_le_jour_le_heros_et_la_taille()
    {
        var state = new GameState(3, TestData.Data, MapSize.Tiny);
        state.EndTurn();
        var summary = GameState.ReadSummary(state.Save());
        Assert.NotNull(summary);
        Assert.Equal(state.Day, summary.Day);
        Assert.Equal(state.CurrentHero!.Name, summary.HeroName);
        Assert.Equal(1, summary.HeroLevel);
        Assert.Equal(MapSize.Tiny, summary.Size);
        Assert.Null(GameState.ReadSummary("pas une sauvegarde"));
    }
}
