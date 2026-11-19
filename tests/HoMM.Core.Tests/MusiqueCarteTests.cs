using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>La musique de la carte suit le terrain autour du héros (<see cref="AdventureMap.MusicTerrain"/>).</summary>
public class MusiqueCarteTests
{
    private static AdventureMap Carte(TerrainType fond)
    {
        var map = new AdventureMap(15, 15);
        for (int y = 0; y < 15; y++)
            for (int x = 0; x < 15; x++)
                map.SetTerrain(new Cell(x, y), fond);
        return map;
    }

    [Fact]
    public void En_bateau_la_mer()
    {
        var map = Carte(TerrainType.Grass);
        map.SetTerrain(new Cell(7, 7), TerrainType.Water);
        Assert.Equal(TerrainType.Water, map.MusicTerrain(new Cell(7, 7)));
    }

    [Fact]
    public void Le_terrain_le_plus_present_autour_du_heros()
    {
        // le héros sur une case d'herbe, au bord d'un désert qui couvre presque tout le carré autour de lui
        var map = Carte(TerrainType.Sand);
        map.SetTerrain(new Cell(7, 7), TerrainType.Grass);
        Assert.Equal(TerrainType.Sand, map.MusicTerrain(new Cell(7, 7)));
    }

    [Fact]
    public void Ni_la_mer_ni_la_montagne_a_terre()
    {
        // sur la rive : l'eau et les montagnes autour ne comptent pas, l'herbe de la case l'emporte
        var map = Carte(TerrainType.Water);
        for (int x = 0; x < 15; x++)
            map.SetTerrain(new Cell(x, 5), TerrainType.Mountain);
        map.SetTerrain(new Cell(7, 7), TerrainType.Grass);
        Assert.Equal(TerrainType.Grass, map.MusicTerrain(new Cell(7, 7)));
    }

    [Fact]
    public void Les_forets_ne_comptent_pas()
    {
        // presque tout est forêt autour du héros : c'est le sol de sa case qui donne la musique
        var map = Carte(TerrainType.Forest);
        map.SetTerrain(new Cell(7, 7), TerrainType.Sand);
        Assert.Equal(TerrainType.Sand, map.MusicTerrain(new Cell(7, 7)));
    }

    [Fact]
    public void La_roche_a_sa_musique()
    {
        var map = Carte(TerrainType.Rock);
        map.SetTerrain(new Cell(7, 7), TerrainType.Grass);
        Assert.Equal(TerrainType.Rock, map.MusicTerrain(new Cell(7, 7)));
    }

    [Fact]
    public void La_neige_a_sa_musique()
    {
        var map = Carte(TerrainType.Snow);
        map.SetTerrain(new Cell(7, 7), TerrainType.Grass);
        Assert.Equal(TerrainType.Snow, map.MusicTerrain(new Cell(7, 7)));
    }

    [Fact]
    public void A_egalite_le_terrain_de_la_case()
    {
        // en damier, une case de marais devenue montagne : 24 cases de marais, 24 de sable ; le héros est sur le marais
        var map = Carte(TerrainType.Sand);
        for (int y = 0; y < 15; y++)
            for (int x = 0; x < 15; x++)
                if ((x + y) % 2 == 0)
                    map.SetTerrain(new Cell(x, y), TerrainType.Swamp);
        map.SetTerrain(new Cell(8, 8), TerrainType.Mountain);
        Assert.Equal(TerrainType.Swamp, map.MusicTerrain(new Cell(7, 7)));
    }
}
