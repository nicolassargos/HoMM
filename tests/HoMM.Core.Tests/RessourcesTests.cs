using System;
using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Les ressources rares (étape 9) : mercure, cristal, soufre et gemmes ; chaque faction en demande deux (une principale,
/// une secondaire), la guilde des mages un peu de chacune ; une mine de bois, de minerai et de la ressource principale
/// dans la zone de départ de chaque joueur.
/// </summary>
public class RessourcesTests
{
    [Fact]
    public void Il_y_a_sept_ressources_dont_quatre_rares()
    {
        Assert.Equal(7, ResourceInfo.All.Length);
        Assert.Equal(new[] { ResourceType.Mercury, ResourceType.Crystal, ResourceType.Sulfur, ResourceType.Gems }, ResourceInfo.Rare);
        Assert.False(ResourceInfo.IsRare(ResourceType.Wood));
        // une mine : 500 or, 2 bois ou minerai, 1 d'une ressource rare
        Assert.Equal(500, new Mine(new Cell(0, 0), ResourceType.Gold).DailyProduction);
        Assert.Equal(2, new Mine(new Cell(0, 0), ResourceType.Ore).DailyProduction);
        Assert.All(ResourceInfo.Rare, r => Assert.Equal(1, new Mine(new Cell(0, 0), r).DailyProduction));
        // un tas de ressource rare : 3 à 6
        var rng = new Random(1);
        Assert.All(Enumerable.Range(0, 50), _ => Assert.InRange(new ResourcePile(new Cell(0, 0), ResourceType.Crystal, rng).Amount, 3, 6));
    }

    [Fact]
    public void Chaque_joueur_commence_avec_deux_de_chaque_ressource_rare()
    {
        var player = new GameState(5, TestData.Data, MapSize.Small).Players[0];
        Assert.All(ResourceInfo.Rare, r => Assert.Equal(2, player.Resources[r]));
    }

    [Fact]
    public void Chaque_faction_a_une_ressource_principale_et_une_secondaire()
    {
        var paladin = TestData.Data.Classes.ForFaction("chateau")!;
        var necromancer = TestData.Data.Classes.ForFaction("morts-vivants")!;
        Assert.Equal((ResourceType.Crystal, ResourceType.Gems), (paladin.MainResource, paladin.SecondaryResource));
        Assert.Equal((ResourceType.Mercury, ResourceType.Gems), (necromancer.MainResource, necromancer.SecondaryResource));

        // la principale : l'habitation et la créature du niveau 7 ; la secondaire : les habitations des niveaux 5 et 6
        foreach (var heroClass in new[] { paladin, necromancer })
        {
            var dwellings = TestData.Data.Buildings.All.Where(b => b.Faction == heroClass.Faction && b.Dwelling != null)
                .ToDictionary(b => TestData.Creatures.Get(b.Dwelling!).Tier);
            Assert.True(dwellings[7].Cost.ContainsKey(heroClass.MainResource!.Value), heroClass.Id);
            Assert.True(TestData.Creatures.Get(dwellings[7].Dwelling!).Cost.ContainsKey(heroClass.MainResource!.Value), heroClass.Id);
            Assert.True(dwellings[5].Cost.ContainsKey(heroClass.SecondaryResource!.Value), heroClass.Id);
            Assert.True(dwellings[6].Cost.ContainsKey(heroClass.SecondaryResource!.Value), heroClass.Id);
        }
    }

    [Fact]
    public void La_guilde_des_mages_demande_un_peu_de_chaque_ressource_rare()
    {
        foreach (var (level, amount) in new[] { (2, 1), (3, 2), (4, 3) })
        {
            var guild = TestData.Data.Buildings.All.Single(b => b.MageGuild == level);
            Assert.All(ResourceInfo.Rare, r => Assert.Equal(amount, guild.Cost.GetValueOrDefault(r)));
        }
        Assert.DoesNotContain(TestData.Data.Buildings.All.Single(b => b.MageGuild == 1).Cost.Keys, ResourceInfo.IsRare);
    }

    [Theory]
    [InlineData(MapSize.Tiny, 1)]
    [InlineData(MapSize.Normal, 2)]
    [InlineData(MapSize.Large, 3)]
    public void La_carte_a_toutes_les_mines_et_chaque_depart_son_bois_son_minerai_et_sa_ressource_principale_sans_gardien(MapSize size, int opponents)
    {
        foreach (int seed in new[] { 1, 42 })
        {
            var state = new GameState(seed, TestData.Data, size, opponents, faction: "morts-vivants",
                opponentFactions: Enumerable.Repeat("chateau", opponents).ToList());
            var mines = state.Map.Objects.OfType<Mine>().ToList();
            Assert.All(ResourceInfo.All, r => Assert.Contains(mines, m => m.Resource == r));
            foreach (var (start, player) in state.Map.Starts.Zip(state.Players))
            {
                var main = TestData.Data.Classes.ForFaction(player.Towns.Single().Faction)!.MainResource!.Value;
                // sans gardien : elles assurent de quoi bâtir sans combat risqué dès les premiers jours
                foreach (var resource in new[] { ResourceType.Wood, ResourceType.Ore, main })
                    // (à 4 à 11 pas de préférence ; un peu plus loin quand la zone de départ n'a pas la place d'une mine de 3 × 2)
                    Assert.Contains(mines, m => m.Resource == resource && !state.Map.IsGuarded(m.Approach)
                        && Math.Max(Math.Abs(m.Position.X - start.X), Math.Abs(m.Position.Y - start.Y)) <= 15);
            }
        }
    }

    /// <summary>Une scierie et une mine de minerai près de chaque ville neutre, gardées (choix de l'utilisateur, 2026-11-14).</summary>
    [Theory]
    [InlineData(1, MapSize.Normal, 1)]
    [InlineData(42, MapSize.Normal, 2)]
    [InlineData(7, MapSize.Large, 3)]
    public void Chaque_ville_neutre_a_sa_scierie_et_sa_mine_de_minerai_gardees(int seed, MapSize size, int opponents)
    {
        var state = new GameState(seed, TestData.Data, size, opponents);
        var map = state.Map;
        var mines = map.Objects.OfType<Mine>().ToList();
        var towns = map.Objects.OfType<Town>().Where(t => !map.Starts.Contains(t.Position)).ToList();
        Assert.NotEmpty(towns);
        foreach (var town in towns)
            foreach (var resource in new[] { ResourceType.Wood, ResourceType.Ore })
                Assert.Contains(mines, m => m.Resource == resource && map.IsGuarded(m.Position)
                    && Math.Max(Math.Abs(m.Position.X - town.Approach.X), Math.Abs(m.Position.Y - town.Approach.Y)) <= 18);
    }

    [Fact]
    public void Une_mine_occupe_trois_cases_sur_deux_et_s_entre_par_le_bas()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small);
        var map = state.Map;
        foreach (var mine in map.Objects.OfType<Mine>())
        {
            Assert.True(mine.Large);
            Assert.Equal(6, mine.Footprint.Count());
            // l'entrée se visite ; les 5 autres cases bloquent ; on entre par la case du dessous
            Assert.True(map.IsPassable(mine.Position));
            Assert.All(mine.Footprint.Where(c => c != mine.Position), c => Assert.False(map.IsPassable(c)));
            Assert.True(map.IsPassable(mine.Approach));
            Assert.Null(map.OccupantAt(mine.Approach));
            Assert.All(mine.Footprint, c => Assert.Same(mine, map.OccupantAt(c)));
        }
        // une sauvegarde d'avant (version 2) : ses mines gardent une seule case
        var json = state.Save().Replace($"\"version\":{GameState.SaveVersion}", "\"version\":2");
        Assert.Contains("\"version\":2", json);
        Assert.All(GameState.Load(json, TestData.Data).Map.Objects.OfType<Mine>(), m => Assert.False(m.Large));
        // sauvegardées de nouveau (version d'aujourd'hui), elles gardent toujours une case
        var again = GameState.Load(json, TestData.Data);
        Assert.All(GameState.Load(again.Save(), TestData.Data).Map.Objects.OfType<Mine>(), m => Assert.False(m.Large));
    }

    [Fact]
    public void Les_nouvelles_ressources_se_sauvegardent()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small);
        state.Players[0].Resources[ResourceType.Mercury] = 7;
        state.Players[0].Resources[ResourceType.Sulfur] = 11;

        var loaded = GameState.Load(state.Save(), TestData.Data);

        Assert.Equal(7, loaded.Players[0].Resources[ResourceType.Mercury]);
        Assert.Equal(11, loaded.Players[0].Resources[ResourceType.Sulfur]);
    }
}
