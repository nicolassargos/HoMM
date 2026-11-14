using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>La mer (2026-11-14) : un bateau amarré, monter à bord, naviguer, débarquer.</summary>
public class MerTests
{
    /// <summary>
    /// Une partie dont le héros, en (4, 4), est sur une rive : de la terre jusqu'à x = 6, la mer de x = 7 à 12 sur toute
    /// la hauteur de la carte, l'autre rive à partir de x = 13 ; un bateau amarré en (7, 4). Rien d'autre autour.
    /// </summary>
    private static (GameState State, Hero Hero) GameByTheSea()
    {
        var state = new GameState(7, TestData.Data, 54, 40, startingTowns: false);
        var map = state.Map;
        var hero = state.CurrentHero!;
        Assert.Equal(new Cell(4, 4), hero.Position);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x <= 22; x++)
        {
            var c = new Cell(x, y);
            map.SetTerrain(c, x is >= 7 and <= 12 ? TerrainType.Water : TerrainType.Grass);
            map.SetRoad(c, false);
        }
        foreach (var obj in map.Objects.Where(o => o.Footprint.Any(c => c.X <= 22)).ToList())
            map.RemoveObject(obj);
        map.AddObject(new Boat(new Cell(7, 4)));
        return (state, hero);
    }

    private static void Walk(GameState state, Cell target)
    {
        state.SetDestination(target);
        while (state.StepAlongPath()) { }
    }

    [Fact]
    public void Sans_bateau_on_ne_va_pas_sur_l_eau()
    {
        var (state, hero) = GameByTheSea();
        state.Map.RemoveObject(state.Map.ObjectAt(new Cell(7, 4))!);
        Assert.Null(Pathfinder.FindPath(state.Map, hero.Position, new Cell(8, 8)));
        Assert.Null(Pathfinder.FindPath(state.Map, hero.Position, new Cell(15, 4)));
    }

    [Fact]
    public void Le_chemin_passe_par_le_bateau_et_traverse_la_mer()
    {
        var (state, hero) = GameByTheSea();
        var path = Pathfinder.FindPath(state.Map, hero.Position, new Cell(15, 4));
        Assert.NotNull(path);
        Assert.Contains(new Cell(7, 4), path!);
        Assert.Contains(path, c => state.Map.IsWater(c) && c != new Cell(7, 4));
    }

    [Fact]
    public void Monter_a_bord_prend_tout_le_mouvement()
    {
        var (state, hero) = GameByTheSea();
        Walk(state, new Cell(7, 4));

        Assert.Equal(new Cell(7, 4), hero.Position);
        Assert.True(hero.AtSea(state.Map));
        Assert.Equal(0, hero.Movement);
        Assert.Null(state.Map.ObjectAt(new Cell(7, 4))); // le bateau porte le héros
        Assert.Contains(state.TakeEvents(), e => e is BoatBoarded);
    }

    [Fact]
    public void On_peut_monter_a_bord_avec_un_peu_de_mouvement()
    {
        var (state, hero) = GameByTheSea();
        Walk(state, new Cell(6, 4));
        hero.SpendMovement(hero.Movement - 1);
        state.SetDestination(new Cell(9, 4));
        var reachable = state.PathReachability();
        Assert.True(reachable[0]);  // monter à bord
        Assert.False(reachable[1]); // plus de mouvement ensuite
        Walk(state, new Cell(9, 4));
        Assert.Equal(new Cell(7, 4), hero.Position);
    }

    [Fact]
    public void Naviguer_puis_debarquer_laisse_le_bateau_sur_l_eau()
    {
        var (state, hero) = GameByTheSea();
        Walk(state, new Cell(7, 4));
        state.EndTurn(); // un nouveau jour
        Walk(state, new Cell(15, 4));

        // il a traversé et débarqué sur l'autre rive : plus de mouvement, le bateau l'attend sur l'eau
        Assert.Equal(new Cell(13, 4), hero.Position);
        Assert.False(hero.AtSea(state.Map));
        Assert.Equal(0, hero.Movement);
        Assert.IsType<Boat>(state.Map.ObjectAt(new Cell(12, 4)));
        Assert.Contains(state.TakeEvents(), e => e is BoatLeft { BoatPosition: var p } && p == new Cell(12, 4));
    }

    [Fact]
    public void Un_monstre_de_la_terre_n_attaque_pas_un_bateau()
    {
        var (state, hero) = GameByTheSea();
        // un monstre sur la rive, à côté de la mer
        state.Map.AddObject(new MonsterGuard(new Cell(6, 6), TestData.Creatures.Get("dragon"), 20));
        Walk(state, new Cell(7, 4));
        state.EndTurn();
        Walk(state, new Cell(7, 7)); // en mer, juste à côté du monstre

        Assert.Equal(new Cell(7, 7), hero.Position);
        Assert.Null(state.CurrentBattle);
        Assert.Contains(hero.Owner.Heroes, h => h == hero);
    }

    [Fact]
    public void La_sauvegarde_garde_les_bateaux_et_le_heros_en_mer()
    {
        var (state, hero) = GameByTheSea();
        state.Map.AddObject(new Boat(new Cell(10, 10)));
        Walk(state, new Cell(7, 4));

        var loaded = GameState.Load(state.Save(), TestData.Data);

        Assert.IsType<Boat>(loaded.Map.ObjectAt(new Cell(10, 10)));
        Assert.Null(loaded.Map.ObjectAt(new Cell(7, 4)));
        var again = loaded.HeroById(hero.Id)!;
        Assert.True(again.AtSea(loaded.Map));
    }

    /// <summary>Une ville du joueur au bord de la mer (ses murs touchent l'eau en x = 7), avec de quoi tout payer.</summary>
    private static (GameState State, Town Town) CoastalTown()
    {
        var (state, _) = GameByTheSea();
        var town = new Town(new Cell(4, 12), "Port-Joli", "chateau", TestData.Data);
        town.SetOwner(state.Players[0]);
        state.Map.AddObject(town);
        state.UpdateBoatSpots();
        foreach (var resource in ResourceInfo.All)
            state.Players[0].Resources[resource] = 10000;
        return (state, town);
    }

    [Fact]
    public void Seule_une_ville_au_bord_de_l_eau_bati_un_chantier_naval()
    {
        var (state, town) = CoastalTown();
        var shipyard = TestData.Data.Buildings.Get("chantier-naval");
        Assert.Equal(new Cell(7, 12), town.BoatSpot);
        Assert.Equal(BuildResult.Ok, town.CanBuild(shipyard, state.Players[0]));

        var inland = new Town(new Cell(18, 30), "Terre-Ferme", "chateau", TestData.Data);
        inland.SetOwner(state.Players[0]);
        state.Map.AddObject(inland);
        state.UpdateBoatSpots();
        Assert.Null(inland.BoatSpot);
        Assert.Equal(BuildResult.NotCoastal, inland.CanBuild(shipyard, state.Players[0]));
    }

    [Fact]
    public void Le_chantier_naval_construit_un_bateau_sur_l_eau()
    {
        var (state, town) = CoastalTown();
        Assert.Equal(BoatResult.NoShipyard, state.CanBuildBoat(town));
        town.AddBuilding(TestData.Data.Buildings.Get("chantier-naval"));
        int gold = state.Players[0].Resources[ResourceType.Gold];

        Assert.True(state.BuildBoat(town));

        Assert.IsType<Boat>(state.Map.ObjectAt(new Cell(7, 12)));
        Assert.Equal(gold - 1000, state.Players[0].Resources[ResourceType.Gold]);
        Assert.Contains(state.TakeEvents(), e => e is BoatBuilt);
        // la place est prise : pas de second bateau tant que le premier y est
        Assert.Equal(BoatResult.SpotTaken, state.CanBuildBoat(town));
    }

    [Fact]
    public void Le_chantier_de_la_cote_vend_un_bateau_et_se_sauvegarde()
    {
        var (state, hero) = GameByTheSea();
        state.Players[0].Resources[ResourceType.Gold] = 5000;
        state.Players[0].Resources[ResourceType.Wood] = 50;
        var yard = new Shipyard(new Cell(6, 8), new Cell(7, 8));
        state.Map.AddObject(yard);
        Walk(state, yard.Position);
        Assert.Contains(state.TakeEvents(), e => e is ShipyardVisited);

        Assert.True(state.BuyBoat(yard));
        Assert.IsType<Boat>(state.Map.ObjectAt(new Cell(7, 8)));

        var loaded = GameState.Load(state.Save(), TestData.Data);
        var again = Assert.IsType<Shipyard>(loaded.Map.ObjectAt(new Cell(6, 8)));
        Assert.Equal(new Cell(7, 8), again.BoatSpot);
        Assert.IsType<Boat>(loaded.Map.ObjectAt(new Cell(7, 8)));
    }

    /// <summary>L'adversaire prend le bateau pour aller chercher un tas de l'autre côté de la mer.</summary>
    [Fact]
    public void L_adversaire_traverse_la_mer_en_bateau()
    {
        var state = new GameState(4, TestData.Data, 54, 40, opponents: 1, startingTowns: false, opponentFactions: new[] { "chateau" });
        state.EndTurn(); // au tour de l'adversaire
        var rival = state.CurrentPlayer.Heroes[0];
        var map = state.Map;
        int rx = rival.Position.X;
        // la mer sur toute la hauteur, à sa gauche ; l'autre rive au-delà ; rien d'autre sur la carte
        foreach (var obj in map.Objects.ToList())
            map.RemoveObject(obj);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var c = new Cell(x, y);
            map.SetTerrain(c, x >= rx - 7 && x <= rx - 3 ? TerrainType.Water : TerrainType.Grass);
            map.SetRoad(c, false);
        }
        map.AddObject(new Boat(new Cell(rx - 3, rival.Position.Y)));
        var pile = new ResourcePile(new Cell(rx - 10, rival.Position.Y), ResourceType.Gold, 900);
        map.AddObject(pile);
        rival.Owner.Fog.Reveal(rival.Position, 20);
        int gold = rival.Owner.Resources[ResourceType.Gold];

        for (int day = 0; day < 6 && map.Objects.Contains(pile); day++)
        {
            ComputerPlayer.PlayTurn(state);
            state.EndTurn();
            while (!state.CurrentPlayer.IsComputer)
                state.EndTurn();
        }

        Assert.DoesNotContain(pile, map.Objects);
        Assert.True(rival.Owner.Resources[ResourceType.Gold] >= gold + 900);
    }

    /// <summary>Sans bateau, l'adversaire en achète un au chantier naval de la côte, puis traverse.</summary>
    [Fact]
    public void L_adversaire_achete_un_bateau_pour_traverser()
    {
        var state = new GameState(4, TestData.Data, 54, 40, opponents: 1, startingTowns: false, opponentFactions: new[] { "chateau" });
        state.EndTurn(); // au tour de l'adversaire
        var rival = state.CurrentPlayer.Heroes[0];
        var map = state.Map;
        int rx = rival.Position.X;
        foreach (var obj in map.Objects.ToList())
            map.RemoveObject(obj);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var c = new Cell(x, y);
            map.SetTerrain(c, x >= rx - 7 && x <= rx - 3 ? TerrainType.Water : TerrainType.Grass);
            map.SetRoad(c, false);
        }
        var yard = new Shipyard(new Cell(rx - 2, rival.Position.Y + 2), new Cell(rx - 3, rival.Position.Y + 2));
        map.AddObject(yard);
        var pile = new ResourcePile(new Cell(rx - 10, rival.Position.Y), ResourceType.Gold, 900);
        map.AddObject(pile);
        rival.Owner.Fog.Reveal(rival.Position, 60); // rien d'autre à découvrir à pied
        rival.Owner.Resources[ResourceType.Gold] = 5000;
        rival.Owner.Resources[ResourceType.Wood] = 20;

        for (int day = 0; day < 6 && map.Objects.Contains(pile); day++)
        {
            ComputerPlayer.PlayTurn(state);
            state.EndTurn();
            while (!state.CurrentPlayer.IsComputer)
                state.EndTurn();
        }

        Assert.DoesNotContain(pile, map.Objects);
        Assert.Single(map.Objects.OfType<Boat>()); // un seul bateau acheté, amarré là où il a débarqué
    }
}
