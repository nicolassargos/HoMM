using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les bâtiments communs à toutes les villes, comme Heroes 3 : le fort et ses murs, le silo, le capitole.</summary>
public class VilleBatimentsCommunsTests
{
    private static BuildingType B(string id) => TestData.Data.Buildings.Get(id);

    /// <summary>Une ville du Château à ce joueur, avec ces bâtiments.</summary>
    private static Town TownOf(Player player, params string[] buildings)
    {
        var town = new Town(new Cell(0, 0), "Testville", "chateau", TestData.Data);
        foreach (var id in buildings)
            town.AddBuilding(B(id));
        town.SetOwner(player);
        return town;
    }

    private static Player NewPlayer(string name = "Rouge") => new(name, new AdventureMap(5, 5));

    [Fact]
    public void La_citadelle_puis_le_chateau_font_grandir_les_habitations()
    {
        var player = NewPlayer();
        var paysan = TestData.Creatures.Get("paysan");
        int Week(params string[] buildings)
        {
            var town = TownOf(player, new[] { "ferme" }.Concat(buildings).ToArray());
            town.Available.Clear();
            town.NewWeek();
            return town.Available[paysan];
        }

        Assert.Equal(paysan.Growth, Week("fort"));
        Assert.Equal(paysan.Growth * 3 / 2, Week("fort", "citadelle"));
        Assert.Equal(paysan.Growth * 2, Week("fort", "citadelle", "chateau-fort"));
    }

    [Fact]
    public void Chaque_etape_du_fort_eleve_les_murs()
    {
        var player = NewPlayer();
        Assert.Equal(0, TownOf(player).Walls);
        Assert.Equal(1, TownOf(player, "fort").Walls);
        Assert.Equal(2, TownOf(player, "fort", "citadelle").Walls);
        Assert.Equal(3, TownOf(player, "fort", "citadelle", "chateau-fort").Walls);
    }

    [Fact]
    public void Le_silo_rapporte_la_ressource_principale_de_la_faction()
    {
        var player = NewPlayer();
        var town = TownOf(player, "marche");
        Assert.Equal(0, player.DailyIncome().GetValueOrDefault(ResourceType.Crystal));

        town.AddBuilding(B("silo"));

        Assert.Equal(ResourceType.Crystal, town.SiloResource); // le cristal du Château
        Assert.Equal(1, player.DailyIncome()[ResourceType.Crystal]);
    }

    [Fact]
    public void Un_seul_capitole_par_joueur()
    {
        var player = NewPlayer();
        player.Resources[ResourceType.Gold] = 100_000;
        var first = TownOf(player, "grand-hotel-ville", "chateau-fort", "capitole");
        var second = TownOf(player, "grand-hotel-ville", "chateau-fort");

        Assert.Equal(2400, first.DailyGold);
        Assert.Equal(BuildResult.CapitolElsewhere, second.CanBuild(B("capitole"), player));
    }

    [Fact]
    public void Le_capitole_pris_par_un_joueur_qui_en_a_deja_un_redevient_grand_hotel_de_ville()
    {
        var red = NewPlayer();
        var blue = NewPlayer("Bleu");
        TownOf(red, "grand-hotel-ville", "chateau-fort", "capitole");
        var taken = TownOf(blue, "grand-hotel-ville", "chateau-fort", "capitole");

        taken.SetOwner(red);

        Assert.False(taken.HasCapitol);
        Assert.True(taken.Has("grand-hotel-ville"));
        Assert.Equal(1200, taken.DailyGold);
    }
}
