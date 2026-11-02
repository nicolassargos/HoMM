using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Chaque objet, héros et joueur a un identifiant stable, utilisable pour retrouver l'objet (sauvegarde).</summary>
public class IdentifiantTests
{
    [Fact]
    public void Chaque_objet_de_la_carte_a_un_identifiant_unique()
    {
        var map = new MapGenerator(42, TestData.Data).Generate();

        var ids = map.Objects.Select(o => o.Id).ToList();

        Assert.All(ids, id => Assert.NotEqual(0, id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(map.Objects, o => Assert.Same(o, map.ObjectById(o.Id)));
    }

    [Fact]
    public void La_meme_graine_donne_les_memes_identifiants()
    {
        var first = new MapGenerator(42, TestData.Data).Generate();
        var second = new MapGenerator(42, TestData.Data).Generate();

        var a = first.Objects.Select(o => (o.Position, o.Id)).OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y);
        var b = second.Objects.Select(o => (o.Position, o.Id)).OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Un_objet_retire_de_la_carte_n_est_plus_retrouvable()
    {
        var map = new MapGenerator(42, TestData.Data).Generate();
        var obj = map.Objects.First();

        map.RemoveObject(obj);

        Assert.Null(map.ObjectById(obj.Id));
    }

    [Fact]
    public void Le_joueur_et_le_heros_de_depart_ont_un_identifiant()
    {
        var state = new GameState(7, TestData.Data);
        var player = state.Players[0];
        var hero = state.CurrentHero!;

        Assert.Equal(1, player.Id);
        Assert.Equal(1, hero.Id);
        Assert.Same(player, state.PlayerById(player.Id));
        Assert.Same(hero, state.HeroById(hero.Id));
        Assert.Null(state.HeroById(999));
    }
}
