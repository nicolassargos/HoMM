using System;
using System.IO;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>La scène d'une ville vue de face (art/towns/&lt;faction&gt;/scene.json) : ses bâtiments, ses images.</summary>
public class SceneVilleTests
{
    private static string Folder(string faction) => Path.Combine(AppContext.BaseDirectory, "towns", faction);

    private static TownScene Scene(string faction) =>
        TownScene.FromJson(File.ReadAllText(Path.Combine(Folder(faction), "scene.json")));

    /// <summary>
    /// Les bâtiments du Château pas encore dessinés : le chantier naval, pas encore montré. À retirer de la liste une fois
    /// dessinés.
    /// </summary>
    private static readonly string[] CastleNotDrawn = { "chantier-naval" };

    [Fact]
    public void La_scene_du_Chateau_couvre_ses_batiments()
    {
        var scene = Scene("chateau");
        var buildings = TestData.Data.Buildings.OfFaction("chateau").ToList();
        var missing = buildings.Where(b => scene.Find(b.Id) == null).Select(b => b.Id).OrderBy(id => id);
        Assert.Equal(CastleNotDrawn.OrderBy(id => id), missing);
        // chacun est montré une fois bâti, par son image ou celle de ce qu'il remplace, sauf le chantier naval
        foreach (var building in buildings.Where(b => b.Id != "chantier-naval"))
        {
            var built = Chain(buildings, building);
            Assert.Contains(scene.Shown(built), s => s.Building == building);
        }
    }

    [Fact]
    public void Chaque_place_de_la_scene_a_ses_images()
    {
        var scene = Scene("chateau");
        Assert.True(File.Exists(Path.Combine(Folder("chateau"), "fond.png")));
        foreach (var place in scene.Places)
        {
            Assert.True(File.Exists(Path.Combine(Folder("chateau"), place.Id + ".png")), place.Id);
            if (place.ShadowX != null)
                Assert.True(File.Exists(Path.Combine(Folder("chateau"), place.Id + "-ombre.png")), place.Id);
            if (place.FrontX != null)
                Assert.True(File.Exists(Path.Combine(Folder("chateau"), place.Id + "-devant.png")), place.Id);
            foreach (var rune in place.Runes ?? new())
                Assert.True(File.Exists(Path.Combine(Folder("chateau"), $"rune-{rune[2]}.png")), place.Id);
        }
    }

    [Fact]
    public void Une_seule_etape_du_chateau_est_montree_la_plus_haute()
    {
        var scene = Scene("chateau");
        var buildings = TestData.Data.Buildings.OfFaction("chateau").ToList();
        var shown = scene.Shown(Chain(buildings, buildings.Single(b => b.Id == "chateau-fort"))).ToList();
        Assert.Equal("chateau-fort", Assert.Single(shown).Place.Id);
    }

    [Fact]
    public void Un_batiment_pas_encore_dessine_prend_l_image_de_celui_qu_il_remplace()
    {
        // la scène du Château sans les guildes 2 et 3 : la guilde 3 prend l'image de la guilde 1
        var full = Scene("chateau");
        var scene = new TownScene(full.Width, full.Height, full.Places.Where(p => p.Id is not ("guilde-mages-2" or "guilde-mages-3")));
        var buildings = TestData.Data.Buildings.OfFaction("chateau").ToList();
        var shown = scene.Shown(Chain(buildings, buildings.Single(b => b.Id == "guilde-mages-3"))).ToList();
        var (building, place) = Assert.Single(shown);
        Assert.Equal("guilde-mages-3", building.Id);
        Assert.Equal("guilde-mages-1", place.Id);
    }

    [Fact]
    public void Les_batiments_sont_montres_du_plus_loin_au_plus_proche()
    {
        var scene = Scene("chateau");
        var buildings = TestData.Data.Buildings.OfFaction("chateau").Where(b => scene.Find(b.Id) != null && b.Replaces == null);
        var depths = scene.Shown(buildings).Select(s => s.Place.Depth).ToList();
        Assert.Equal(depths.OrderByDescending(d => d), depths);
    }

    /// <summary>Le bâtiment et tous ceux qu'il remplace, de proche en proche (bâtis avant lui).</summary>
    private static BuildingType[] Chain(System.Collections.Generic.List<BuildingType> buildings, BuildingType building)
    {
        var chain = new System.Collections.Generic.List<BuildingType>();
        for (var b = building; b != null; b = b.Replaces is { } id ? buildings.Single(x => x.Id == id) : null)
            chain.Add(b);
        return chain.ToArray();
    }
}
