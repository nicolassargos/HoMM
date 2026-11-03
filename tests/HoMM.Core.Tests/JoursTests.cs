using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Le nombre de jours pour aller jusqu'à une case (affiché sur le curseur).</summary>
public class JoursTests
{
    /// <summary>Une petite carte d'herbe vide, avec un héros à gauche.</summary>
    private static GameState EmptyGrass(int width)
    {
        var map = new AdventureMap(width, 3) { Start = new Cell(0, 1) };
        for (int x = 0; x < width; x++)
        for (int y = 0; y < 3; y++)
            map.SetTerrain(new Cell(x, y), TerrainType.Grass);
        return GameState.ForMap(map, TestData.Data);
    }

    [Fact]
    public void Une_case_proche_s_atteint_aujourd_hui()
    {
        var state = EmptyGrass(30);

        Assert.Equal(1, state.DaysTo(new Cell(5, 1)));
    }

    [Fact]
    public void Les_jours_suivent_le_mouvement_du_heros()
    {
        var state = EmptyGrass(40);
        var hero = state.CurrentHero!;
        int perDay = hero.MaxMovement / TerrainInfo.MoveCost(TerrainType.Grass); // 15 cases d'herbe par jour

        Assert.Equal(1, state.DaysTo(new Cell(perDay, 1)));
        Assert.Equal(2, state.DaysTo(new Cell(perDay + 1, 1)));
        Assert.Equal(3, state.DaysTo(new Cell(2 * perDay + 1, 1)));
    }

    [Fact]
    public void Le_mouvement_deja_depense_compte()
    {
        var state = EmptyGrass(30);
        state.CurrentHero!.SpendMovement(state.CurrentHero.MaxMovement - 100); // il reste une case

        Assert.Equal(1, state.DaysTo(new Cell(1, 1)));
        Assert.Equal(2, state.DaysTo(new Cell(2, 1)));
    }

    [Fact]
    public void Une_case_inaccessible_n_a_pas_de_nombre_de_jours()
    {
        var state = EmptyGrass(30);
        var water = new Cell(10, 1);
        state.Map.SetTerrain(water, TerrainType.Water);

        Assert.Null(state.DaysTo(water));
        Assert.Null(state.DaysTo(state.CurrentHero!.Position));
    }
}
