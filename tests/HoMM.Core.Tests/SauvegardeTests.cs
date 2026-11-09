using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Sauvegarde et chargement : une partie chargée est identique, et continue exactement de la même façon.</summary>
public class SauvegardeTests
{
    /// <summary>Une partie un peu avancée : quelques jours, un chemin prévu, de l'expérience.</summary>
    private static GameState PlayedGame()
    {
        var state = new GameState(42, TestData.Data);
        state.ChooseRules(new GameRules(VictoryCondition.CaptureTowns, DayLimit: 120));
        for (int i = 0; i < 8; i++)
            state.EndTurn();
        var hero = state.CurrentHero!;
        state.SetDestination(hero.Position.Offset(3, 2));
        state.StepAlongPath();
        state.SetDestination(hero.Position.Offset(2, 3));
        state.TakeEvents();
        return state;
    }

    [Fact]
    public void Une_partie_chargee_est_identique()
    {
        var state = PlayedGame();
        string saved = state.Save();

        var loaded = GameState.Load(saved, TestData.Data);

        Assert.Equal(saved, loaded.Save());
        Assert.Equal(state.Day, loaded.Day);
        Assert.Equal(state.Rules, loaded.Rules);
        Assert.Equal(state.CurrentHero!.Id, loaded.CurrentHero!.Id);
        Assert.Equal(state.CurrentHero.Position, loaded.CurrentHero.Position);
        Assert.Equal(state.Path, loaded.Path);
        Assert.Equal(state.Map.Objects.Count, loaded.Map.Objects.Count);
        foreach (var obj in state.Map.Objects)
        {
            var copy = loaded.Map.ObjectById(obj.Id);
            Assert.NotNull(copy);
            Assert.Equal(obj.GetType(), copy!.GetType());
            Assert.Equal(obj.Position, copy.Position);
        }
        for (int y = 0; y < state.Map.Height; y++)
        for (int x = 0; x < state.Map.Width; x++)
        {
            var cell = new Cell(x, y);
            Assert.Equal(state.Map.GetTerrain(cell), loaded.Map.GetTerrain(cell));
            Assert.Equal(state.Players[0].Fog.IsExplored(cell), loaded.Players[0].Fog.IsExplored(cell));
        }
    }

    [Fact]
    public void Une_partie_chargee_continue_de_la_meme_facon()
    {
        // Une troupe trop nombreuse (risque de révolte) et un monstre à côté : le combat tire au sort
        var state = new GameState(7, TestData.Data);
        var hero = state.CurrentHero!;
        hero.Army.Add(TestData.Creatures.Get("paysan"), 150);
        state.Map.AddObject(new MonsterGuard(hero.Position.Offset(2, 0), TestData.Creatures.Get("gobelin"), 5));
        var loaded = GameState.Load(state.Save(), TestData.Data);

        foreach (var game in new[] { state, loaded })
        {
            game.SetDestination(game.CurrentHero!.Position.Offset(1, 0));
            game.StepAlongPath();
            for (int i = 0; i < 10; i++)
                game.EndTurn();
        }

        Assert.Equal(state.Save(), loaded.Save());
    }

    [Fact]
    public void Un_choix_de_niveau_en_attente_est_de_nouveau_propose()
    {
        var state = new GameState(42, TestData.Data);
        state.GrantExperience(state.CurrentHero!, Hero.ExperienceForLevel(2));
        state.TakeEvents();

        var loaded = GameState.Load(state.Save(), TestData.Data);
        var choice = Assert.Single(loaded.TakeEvents().OfType<HeroLevelChoice>());

        Assert.Equal(2, choice.Level);
        Assert.True(loaded.ChooseStat(choice, HeroStat.Power));
        Assert.Equal(2, loaded.CurrentHero!.Level);
        Assert.Equal(2, loaded.CurrentHero.Power);
    }

    [Fact]
    public void Les_villes_et_mines_gardent_leur_proprietaire()
    {
        var state = new GameState(42, TestData.Data);
        var player = state.Players[0];
        var town = state.Towns.First();
        town.SetOwner(player);
        town.AddBuilding(TestData.Data.Buildings.Get("tour-archers"));
        var mine = state.Map.Objects.OfType<Mine>().First();
        mine.Visit(state.CurrentHero!);

        var loaded = GameState.Load(state.Save(), TestData.Data);
        var loadedPlayer = loaded.Players[0];
        var loadedTown = (Town)loaded.Map.ObjectById(town.Id)!;

        Assert.Same(loadedPlayer, loadedTown.Owner);
        Assert.Contains(loadedTown, loadedPlayer.Towns);
        Assert.True(loadedTown.Has("tour-archers"));
        Assert.Equal(town.Available.Values.Sum(), loadedTown.Available.Values.Sum());
        Assert.Same(loadedPlayer, ((Mine)loaded.Map.ObjectById(mine.Id)!).Owner);
        Assert.Equal(player.DailyIncome(), loadedPlayer.DailyIncome());
    }

    [Fact]
    public void Un_nouvel_objet_ne_reprend_pas_un_identifiant_existant()
    {
        var loaded = GameState.Load(new GameState(42, TestData.Data).Save(), TestData.Data);
        var guard = new MonsterGuard(new Cell(0, 0), TestData.Creatures.Get("gobelin"), 3);

        loaded.Map.AddObject(guard);

        Assert.True(guard.Id > loaded.Map.Objects.Where(o => o != guard).Max(o => o.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pas du json")]
    [InlineData("{\"version\": 99}")]
    public void Une_sauvegarde_illisible_est_refusee(string json)
    {
        Assert.Throws<FormatException>(() => GameState.Load(json, TestData.Data));
    }

    [Fact]
    public void Une_sauvegarde_avec_une_creature_inconnue_est_refusee()
    {
        string saved = new GameState(42, TestData.Data).Save().Replace("\"paysan\"", "\"licorne\"");

        var error = Assert.Throws<FormatException>(() => GameState.Load(saved, TestData.Data));
        Assert.Contains("licorne", error.Message);
    }
}
