using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

public class HeroTests
{
    /// <summary>Une partie avec une ville du joueur à côté du héros (entrée deux cases à droite), avec une taverne.</summary>
    private static (GameState State, Town Town, Player Player) GameWithTavern()
    {
        var state = new GameState(7, TestData.Data);
        var town = new Town(state.CurrentHero!.Position.Offset(2, 0), "Testville", "chateau", TestData.Data);
        town.AddBuilding(TestData.Data.Buildings.Get("salle-village"));
        town.AddBuilding(TestData.Data.Buildings.Get("taverne"));
        town.SetOwner(state.Players[0]);
        state.Map.AddObject(town);
        return (state, town, state.Players[0]);
    }

    [Fact]
    public void Recruter_un_heros_a_la_taverne()
    {
        var (state, town, player) = GameWithTavern();
        var roland = state.CurrentHero!;
        state.TakeEvents();

        Assert.Equal(HireResult.Ok, state.HireHero(town));

        Assert.Equal(0, player.Resources[ResourceType.Gold]);
        Assert.Equal(2, player.Heroes.Count);
        var hired = state.CurrentHero!;
        Assert.NotSame(roland, hired);
        Assert.Equal("Dame Isolde", hired.Name);
        Assert.Equal(town.Position, hired.Position);
        Assert.Equal(12, hired.Army.Stacks.Single(s => s.Type.Id == "piquier").Count);
        Assert.Equal(hired.MaxMovement, hired.Movement);
        Assert.Equal(new HeroHired(hired, town), state.TakeEvents().OfType<HeroHired>().Single());
    }

    [Fact]
    public void Pas_de_heros_sans_taverne_sans_or_ou_si_la_ville_est_occupee()
    {
        var (state, town, player) = GameWithTavern();
        var noTavern = new Town(new Cell(30, 20), "Sanstaverne", "chateau", TestData.Data);
        noTavern.SetOwner(player);
        Assert.Equal(HireResult.NoTavern, state.CanHireHero(noTavern));

        Assert.Equal(HireResult.Ok, state.HireHero(town));
        // Le héros recruté est encore dans la ville : la place est prise
        player.Resources[ResourceType.Gold] = 10_000;
        Assert.Equal(HireResult.TownOccupied, state.CanHireHero(town));

        var other = new Town(new Cell(30, 25), "Autreville", "chateau", TestData.Data);
        other.AddBuilding(TestData.Data.Buildings.Get("taverne"));
        other.SetOwner(player);
        player.Resources[ResourceType.Gold] = 100;
        Assert.Equal(HireResult.NotEnoughGold, state.CanHireHero(other));
    }

    [Fact]
    public void Chaque_heros_garde_son_chemin()
    {
        var (state, town, _) = GameWithTavern();
        var roland = state.CurrentHero!;
        var start = roland.Position;
        state.SetDestination(start.Offset(0, 2));
        var rolandPath = state.Path;

        state.HireHero(town);
        Assert.Null(state.Path); // le nouveau héros n'a pas encore de chemin
        state.SetDestination(town.Approach.Offset(1, 1));
        var isoldePath = state.Path;

        state.SelectHero(roland);
        Assert.Same(rolandPath, state.Path);
        state.SelectNextHero();
        Assert.Same(isoldePath, state.Path);
    }

    [Fact]
    public void Les_heros_ne_se_traversent_pas()
    {
        var (state, town, _) = GameWithTavern();
        var roland = state.CurrentHero!;
        var start = roland.Position;
        state.SetDestination(start.Offset(0, 2)); // Roland prévoit de descendre de deux cases
        Assert.Equal(start.Offset(0, 1), state.Path![0]);

        state.HireHero(town);
        var isolde = state.CurrentHero!;
        // Isolde sort de la ville et se place juste sous Roland, sur son chemin
        state.SetDestination(start.Offset(0, 1));
        while (state.StepAlongPath()) { }
        Assert.Equal(start.Offset(0, 1), isolde.Position);

        // Roland ne peut ni aller sur sa case, ni la traverser : il s'arrête, son chemin reste prévu
        state.SelectHero(roland);
        Assert.False(state.StepAlongPath());
        Assert.Equal(start, roland.Position);
        Assert.NotNull(state.Path);
        state.SetDestination(isolde.Position);
        Assert.Null(state.Path);
    }

    [Fact]
    public void Perdre_un_heros_selectionne_le_suivant()
    {
        var (state, town, player) = GameWithTavern();
        var roland = state.CurrentHero!;
        state.HireHero(town);
        state.SelectHero(roland);
        var start = roland.Position;
        state.Map.AddObject(new MonsterGuard(start.Offset(0, 3), TestData.Creatures.Get("dragon"), 50));

        state.SetDestination(start.Offset(0, 2));
        while (state.StepAlongPath()) { }

        Assert.Single(player.Heroes);
        Assert.Equal("Dame Isolde", state.CurrentHero!.Name);
        Assert.Equal(GameOutcome.Playing, state.Outcome);
    }
}
