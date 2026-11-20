using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Les bâtiments spéciaux des factions (`PLAN.md`, « Les bâtiments des villes ») : le Grand Comptoir des Nains, les
/// Sentiers des rôdeurs de la Forêt, le Tourbillon de mana du Donjon.
/// </summary>
public class BatimentsSpeciauxTests
{
    private static BuildingType B(string id) => TestData.Data.Buildings.Get(id);

    /// <summary>Une partie avec une ville du joueur de cette faction juste à côté du héros (deux cases à droite).</summary>
    private static (GameState State, Town Town) GameWithOwnTown(string faction, params string[] buildings)
    {
        var state = new GameState(7, TestData.Data, 54, 40, startingTowns: false);
        var town = new Town(state.CurrentHero!.Position.Offset(2, 0), "Testville", faction, TestData.Data);
        foreach (var id in buildings)
            town.AddBuilding(B(id));
        state.Map.AddObject(town);
        town.SetOwner(state.Players[0]);
        return (state, town);
    }

    private static void WalkTo(GameState state, Cell target)
    {
        state.SetDestination(target);
        while (state.StepAlongPath()) { }
    }

    [Fact]
    public void Le_Grand_Comptoir_compte_pour_deux_marches_de_plus()
    {
        var (state, town) = GameWithOwnTown("nains", "marche");
        Assert.Equal(1, Market.Count(state.Players[0]));

        town.AddBuilding(B("grand-comptoir"));

        Assert.Equal(3, Market.Count(state.Players[0]));
    }

    [Fact]
    public void Les_Sentiers_des_rodeurs_donnent_du_mouvement_a_qui_part_de_la_ville()
    {
        var (state, town) = GameWithOwnTown("foret", "sentiers-rodeurs");
        var hero = state.CurrentHero!;
        WalkTo(state, town.Position);

        state.EndTurn();

        Assert.Equal(hero.MaxMovement + 400, hero.Movement);
    }

    [Fact]
    public void Le_Tourbillon_de_mana_double_les_points_de_sort_une_fois_par_semaine()
    {
        var (state, town) = GameWithOwnTown("donjon", "tourbillon-mana");
        var hero = state.CurrentHero!;
        Assert.True(hero.MaxSpellPoints > 0);
        state.TakeEvents();

        WalkTo(state, town.Position);

        Assert.Equal(2 * hero.MaxSpellPoints, hero.SpellPoints);
        Assert.Contains(state.TakeEvents(), e => e is ManaVortexUsed);
        Assert.False(town.CanUseManaVortex);

        town.NewWeek();
        Assert.True(town.CanUseManaVortex);
    }
}
