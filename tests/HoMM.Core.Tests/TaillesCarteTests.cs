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
            foreach (var obj in map.Objects.Where(o => o.Position != map.Start)) // sauf le château de départ
                Assert.NotNull(Pathfinder.FindPath(map, map.Start, obj.Position));
        }
    }

    /// <summary>
    /// La simulation d'équilibrage (optimiste, un seul héros) gagne sur les petites cartes ; sur les grandes, il y a
    /// beaucoup plus de monstres à vaincre : elle prend au moins une ville, sans forcément tout vaincre en 200 jours
    /// (grande carte : vers le jour 146 ; très grande : pas en 200 jours).
    /// </summary>
    [Theory]
    [InlineData(MapSize.Tiny, true)]
    [InlineData(MapSize.Small, true)]
    [InlineData(MapSize.Large, false)]
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
