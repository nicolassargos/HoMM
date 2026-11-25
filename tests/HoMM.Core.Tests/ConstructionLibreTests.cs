using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Développement (« --free-build ») : bâtir sans payer, sans attendre, les prérequis bâtis avec.</summary>
public class ConstructionLibreTests
{
    [Fact]
    public void Sans_limites_on_batit_tout_le_meme_jour_sans_payer()
    {
        var state = new GameState(3, TestData.Data, MapSize.Small);
        var player = state.Players[0];
        var town = player.Towns.Single();
        player.FreeBuilding = true;
        var gold = player.Resources[ResourceType.Gold];

        Assert.Equal(BuildResult.Ok, state.Build(town, "chateau-fort"));
        Assert.Equal(BuildResult.Ok, state.Build(town, "guilde-mages-3"));

        // le fort et la citadelle avant le château fort, les guildes 1 et 2 avant la 3
        foreach (var id in new[] { "fort", "citadelle", "chateau-fort", "guilde-mages-1", "guilde-mages-2", "guilde-mages-3" })
            Assert.True(town.Has(id), id);
        Assert.Equal(gold, player.Resources[ResourceType.Gold]);
        Assert.False(town.BuiltToday);
    }

    [Fact]
    public void Sans_l_option_les_limites_restent()
    {
        var state = new GameState(3, TestData.Data, MapSize.Small);
        var town = state.Players[0].Towns.Single();
        Assert.Equal(BuildResult.MissingRequirement, state.Build(town, "chateau-fort"));
    }
}
