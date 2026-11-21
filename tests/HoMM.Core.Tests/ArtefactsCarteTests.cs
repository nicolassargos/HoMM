using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Les artefacts sur la carte (choix de l'utilisateur, 2026-11-21) : posés (les mineurs seuls, les majeurs et les reliques
/// gardés), cachés dans certains coffres, ramassés, sauvegardés.
/// </summary>
public class ArtefactsCarteTests
{
    private static ArtifactType Get(string id) => TestData.Data.Artifacts.Get(id);

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    public void La_carte_pose_des_artefacts_tous_differents_les_meilleurs_gardes(int seed)
    {
        var generator = new MapGenerator(seed, TestData.Data);
        var map = generator.Generate();

        var piles = map.Objects.OfType<ArtifactPile>().ToList();
        var inChests = map.Objects.OfType<TreasureChest>().Where(c => c.Artifact != null).Select(c => c.Artifact!).ToList();
        var all = piles.Select(p => p.Artifact).Concat(inChests).ToList();

        Assert.Equal(generator.PlannedArtifacts, piles.Count);
        Assert.Equal(all.Count, all.Select(a => a.Id).Distinct().Count()); // chacun une fois au plus
        Assert.DoesNotContain(all, a => a.Id == ArtifactCatalog.TreasureId); // le Plastron reste enfoui
        Assert.All(inChests, a => Assert.Equal(ArtifactRank.Minor, a.Rank));
        Assert.Contains(piles, p => p.Artifact.Rank == ArtifactRank.Minor);
        Assert.Contains(piles, p => p.Artifact.Rank != ArtifactRank.Minor);
        Assert.All(piles.Where(p => p.Artifact.Rank != ArtifactRank.Minor), p => Assert.True(map.IsGuarded(p.Position),
            $"{p.Artifact.Id} en {p.Position} sans gardien"));
    }

    [Fact]
    public void Le_heros_ramasse_l_artefact()
    {
        var (state, pile) = CarteObjetsTests.GameNextTo(c => new ArtifactPile(c, Get("hache-bourreau")));
        var hero = state.CurrentHero!;
        int attack = hero.Attack;

        CarteObjetsTests.Visit(state, pile);

        var found = Assert.Single(state.TakeEvents().OfType<ArtifactFound>());
        Assert.Equal("hache-bourreau", found.Artifact.Id);
        Assert.Contains(hero.Artifacts, a => a.Id == "hache-bourreau");
        Assert.Equal(attack + 4, hero.Attack);
        Assert.DoesNotContain(state.Map.Objects, o => o == pile);
    }

    [Fact]
    public void Un_coffre_a_artefact_le_donne_sans_choix()
    {
        var (state, chest) = CarteObjetsTests.GameNextTo(c => new TreasureChest(c, 1500, Get("anneau-trefle")));
        var hero = state.CurrentHero!;
        int gold = hero.Owner.Resources[ResourceType.Gold];

        CarteObjetsTests.Visit(state, chest);

        Assert.Single(state.TakeEvents().OfType<ArtifactFound>());
        Assert.Empty(state.PendingChests);
        Assert.Contains(hero.Artifacts, a => a.Id == "anneau-trefle");
        Assert.Equal(gold, hero.Owner.Resources[ResourceType.Gold]);
    }

    [Fact]
    public void Artefacts_poses_et_coffres_sont_sauvegardes()
    {
        var (state, pile) = CarteObjetsTests.GameNextTo(c => new ArtifactPile(c, Get("de-geant")));
        var chest = new TreasureChest(pile.Position.Offset(0, 1), 1000, Get("longue-vue"));
        state.Map.AddObject(chest);

        var map = GameState.Load(state.Save(), TestData.Data).Map;

        Assert.Equal("de-geant", Assert.IsType<ArtifactPile>(map.ObjectAt(pile.Position)).Artifact.Id);
        Assert.Equal("longue-vue", Assert.IsType<TreasureChest>(map.ObjectAt(chest.Position)).Artifact?.Id);
    }
}
