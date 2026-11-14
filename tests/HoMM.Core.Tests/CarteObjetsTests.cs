using System;
using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les objets et lieux de la carte : forêts, coffres au choix, et les lieux à visiter.</summary>
public class CarteObjetsTests
{
    /// <summary>
    /// Une partie où le héros est juste à côté d'un objet (posé à sa droite ; un lieu haut couvre aussi la case
    /// au-dessus), sans monstre autour.
    /// </summary>
    internal static (GameState State, T Object) GameNextTo<T>(Func<Cell, T> make) where T : MapObject
    {
        var state = new GameState(42, TestData.Data, 54, 40, startingTowns: false);
        state.TakeEvents();
        var hero = state.CurrentHero!;
        var cell = hero.Position.Offset(1, 0);
        var placed = make(cell);
        var footprint = placed.Footprint.ToList();
        foreach (var obj in state.Map.Objects.Where(o => o.Footprint.Any(footprint.Contains) || o is MonsterGuard g
                     && Math.Max(Math.Abs(g.Position.X - cell.X), Math.Abs(g.Position.Y - cell.Y)) <= 2).ToList())
            state.Map.RemoveObject(obj);
        foreach (var c in footprint)
            state.Map.SetTerrain(c, TerrainType.Grass);
        state.Map.SetTerrain(hero.Position.Offset(-1, 0), TerrainType.Grass);
        state.Map.AddObject(placed);
        return (state, placed);
    }

    /// <summary>Le héros entre sur la case de l'objet.</summary>
    internal static void Visit(GameState state, MapObject obj)
    {
        state.SetDestination(obj.Position);
        state.StepAlongPath();
    }

    [Fact]
    public void La_foret_bloque_le_passage()
    {
        Assert.False(TerrainInfo.IsPassable(TerrainType.Forest));
        var map = new AdventureMap(3, 1);
        map.SetTerrain(new Cell(1, 0), TerrainType.Forest);
        Assert.Null(Pathfinder.FindPath(map, new Cell(0, 0), new Cell(2, 0)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Un_coffre_donne_l_or_ou_l_experience_au_choix(bool gold)
    {
        var (state, chest) = GameNextTo(c => new TreasureChest(c, 1500));
        var hero = state.CurrentHero!;
        int goldBefore = hero.Owner.Resources[ResourceType.Gold];

        Visit(state, chest);

        var opened = Assert.Single(state.TakeEvents().OfType<ChestOpened>());
        Assert.DoesNotContain(state.Map.Objects, o => o == chest);
        Assert.Equal(goldBefore, hero.Owner.Resources[ResourceType.Gold]); // rien tant qu'on n'a pas choisi
        Assert.Single(state.PendingChests);

        Assert.True(state.ChooseChest(opened, gold));

        Assert.Empty(state.PendingChests);
        Assert.False(state.ChooseChest(opened, gold)); // une seule fois
        Assert.Equal(goldBefore + (gold ? 1500 : 0), hero.Owner.Resources[ResourceType.Gold]);
        Assert.Equal(gold ? 0 : 1000, hero.Experience);
        Assert.Contains(state.TakeEvents(), e => e is ChestChosen c && c.Gold == gold);
    }

    [Fact]
    public void Un_coffre_qui_attend_son_choix_est_sauvegarde()
    {
        var (state, chest) = GameNextTo(c => new TreasureChest(c, 2000));
        Visit(state, chest);

        var loaded = GameState.Load(state.Save(), TestData.Data);

        var pending = Assert.Single(loaded.PendingChests);
        Assert.Equal(2000, pending.Chest.Gold);
        Assert.True(loaded.ChooseChest(pending, gold: false));
        Assert.Equal(1500, loaded.CurrentHero!.Experience);
    }

    /// <summary>Le héros repart sur sa case de départ, puis revient sur l'objet (le lendemain, avec tout son mouvement).</summary>
    private static void VisitAgain(GameState state, MapObject obj)
    {
        state.SetDestination(obj.Position.Offset(-1, 0));
        state.StepAlongPath();
        state.EndTurn();
        Visit(state, obj);
    }

    [Fact]
    public void Un_autel_donne_un_point_une_seule_fois_par_heros()
    {
        var (state, shrine) = GameNextTo(c => new Shrine(c, HeroStat.Defense));
        var hero = state.CurrentHero!;
        int before = hero.Defense;

        Visit(state, shrine);
        Assert.Equal(before + 1, hero.Defense);
        Assert.Contains(state.TakeEvents(), e => e is ShrineVisited { Gained: true });
        Assert.Contains(state.Map.Objects, o => o == shrine); // il reste

        VisitAgain(state, shrine);
        Assert.Equal(before + 1, hero.Defense);
        Assert.Contains(state.TakeEvents(), e => e is ShrineVisited { Gained: false });
    }

    [Fact]
    public void La_cabane_du_sage_enseigne_son_sort()
    {
        var spell = TestData.Spells.All.First(s => s.Level == 2);
        var (state, hut) = GameNextTo(c => new SageHut(c, spell));
        var hero = state.CurrentHero!;
        Assert.False(hero.Knows(spell));

        Visit(state, hut);

        Assert.True(hero.Knows(spell));
        Assert.Contains(state.TakeEvents(), e => e is SageVisited { Learned: true });
    }

    [Fact]
    public void Le_moulin_donne_chaque_semaine_au_premier_qui_passe()
    {
        var (state, mill) = GameNextTo(c => new WeeklyProducer(c, ResourceType.Crystal));
        var player = state.CurrentHero!.Owner;
        int before = player.Resources.GetValueOrDefault(ResourceType.Crystal);

        Visit(state, mill);
        Assert.Equal(before + 2, player.Resources[ResourceType.Crystal]);
        VisitAgain(state, mill);
        Assert.Equal(before + 2, player.Resources[ResourceType.Crystal]); // vide jusqu'à la semaine suivante
        Assert.Contains(state.TakeEvents(), e => e is ProducerVisited { Amount: 0 });

        while (state.Day % 7 != 0)
            state.EndTurn();
        state.EndTurn(); // une nouvelle semaine
        Assert.True(mill.Stocked);
    }

    [Fact]
    public void La_tour_d_observation_devoile_la_carte_autour()
    {
        var (state, tower) = GameNextTo(c => new ObservationTower(c));
        var fog = state.CurrentHero!.Owner.Fog;
        var far = tower.Position.Offset(ObservationTower.Radius - 2, 0);
        if (!state.Map.InBounds(far))
            far = tower.Position.Offset(0, ObservationTower.Radius - 2);
        Assert.False(fog.IsExplored(far));

        Visit(state, tower);

        Assert.True(fog.IsExplored(far));
        Assert.Contains(tower.Visitors, id => id == state.CurrentHero!.Owner.Id);
    }

    /// <summary>
    /// Les tours loin des bords (ce qu'elles dévoilent reste sur la carte : le quart de la carte au plus sur une petite) et
    /// loin l'une de l'autre (chacune dévoile sa part).
    /// </summary>
    internal static void AssertTowersSpread(AdventureMap map)
    {
        int margin = Math.Min(MapGenerator.TowerEdgeMargin, Math.Min(map.Width, map.Height) / 4);
        var towers = map.Objects.OfType<ObservationTower>().ToList();
        Assert.All(towers, t => Assert.True(
            Math.Min(Math.Min(t.Position.X, t.Position.Y), Math.Min(map.Width - 1 - t.Position.X, map.Height - 1 - t.Position.Y)) >= margin,
            $"tour au bord en {t.Position}"));
        foreach (var a in towers)
            foreach (var b in towers.Where(b => b != a))
                Assert.True(Math.Pow(a.Position.X - b.Position.X, 2) + Math.Pow(a.Position.Y - b.Position.Y, 2)
                    >= MapGenerator.TowerSpacing * MapGenerator.TowerSpacing, $"tours trop proches en {a.Position} et {b.Position}");
    }

    [Theory]
    [InlineData(MapSize.Normal)]
    [InlineData(MapSize.Huge)]
    public void Les_tours_sont_loin_des_bords_et_l_une_de_l_autre(MapSize size)
    {
        foreach (int seed in new[] { 1, 42 })
        {
            var map = new GameState(seed, TestData.Data, size).Map;
            Assert.Contains(map.Objects, o => o is ObservationTower);
            AssertTowersSpread(map);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void Le_generateur_pose_les_lieux_et_ils_sont_sauvegardes(int seed)
    {
        var state = new GameState(seed, TestData.Data, 54, 40);
        var map = state.Map;
        Assert.Contains(map.Objects, o => o is ObservationTower);
        AssertTowersSpread(map);
        Assert.Contains(map.Objects, o => o is SageHut);
        Assert.Contains(map.Objects, o => o is WeeklyProducer { IsWaterWheel: true });
        Assert.Contains(map.Objects, o => o is WeeklyProducer { IsWaterWheel: false });
        Assert.Equal(4, map.Objects.OfType<Shrine>().Select(s => s.Stat).Distinct().Count());
        // aucune case de forêt n'est accessible : les objets sont tous sur des cases où l'on marche
        Assert.All(map.Objects, o => Assert.NotEqual(TerrainType.Forest, map.GetTerrain(o.Position)));

        var shrine = map.Objects.OfType<Shrine>().First();
        shrine.Visitors.Add(state.CurrentHero!.Id);
        var loaded = GameState.Load(state.Save(), TestData.Data);
        var again = loaded.Map.Objects.OfType<Shrine>().Single(s => s.Id == shrine.Id);
        Assert.Equal(shrine.Stat, again.Stat);
        Assert.Contains(state.CurrentHero!.Id, again.Visitors);
        Assert.Equal(map.Objects.OfType<SageHut>().Select(h => h.Spell.Id), loaded.Map.Objects.OfType<SageHut>().Select(h => h.Spell.Id));
    }

    [Fact]
    public void Un_lieu_haut_bloque_la_case_au_dessus_de_sa_porte()
    {
        var (state, mill) = GameNextTo(c => new WeeklyProducer(c, ResourceType.Gold));
        var above = mill.Position.Offset(0, -1);

        Assert.Equal(new[] { mill.Position, above }, mill.Footprint);
        Assert.False(state.Map.IsPassable(above));
        Assert.True(state.Map.IsPassable(mill.Position));
        Assert.Same(mill, state.Map.OccupantAt(above)); // un clic sur le toit mène à la porte
        // le cercle des fées, plat, reste sur sa case ; le temple est haut
        Assert.Single(new BlessingSite(new Cell(5, 5), Blessing.Luck).Footprint);
        Assert.Equal(2, new BlessingSite(new Cell(5, 5), Blessing.Morale).Footprint.Count());
        Assert.Equal(2, new SageHut(new Cell(5, 5), TestData.Spells.All[0]).Footprint.Count());
        Assert.Equal(2, new ObservationTower(new Cell(5, 5)).Footprint.Count());
        Assert.Single(new Shrine(new Cell(5, 5), HeroStat.Attack).Footprint);
    }

    [Theory]
    [InlineData(1, MapSize.Normal)]
    [InlineData(42, MapSize.Normal)]
    [InlineData(7, MapSize.Small)]
    [InlineData(3, MapSize.Large)]
    public void Le_generateur_laisse_la_place_aux_lieux_hauts(int seed, MapSize size)
    {
        var map = new GameState(seed, TestData.Data, size).Map;
        var tall = map.Objects.Where(o => o.Footprint.Count() == 2).ToList();
        Assert.NotEmpty(tall);
        foreach (var site in tall)
        {
            var above = site.Position.Offset(0, -1);
            Assert.True(TerrainInfo.IsPassable(map.GetTerrain(above)), $"{site} : terrain sous le toit");
            Assert.False(map.HasRoad(above));
            Assert.False(map.IsPassable(above));
            // rien sous l'image, qui déborde d'une demi-case de chaque côté
            foreach (var side in new[] { site.Position, above }.SelectMany(c => new[] { c.Offset(-1, 0), c.Offset(1, 0) }))
                Assert.True(!map.InBounds(side) || map.OccupantAt(side) == null, $"{site} : {map.OccupantAt(side)} sous l'image en {side}");
        }
    }

    [Fact]
    public void Les_lieux_hauts_sont_sauvegardes_et_ceux_d_avant_gardent_une_case()
    {
        var state = new GameState(1, TestData.Data, 54, 40);
        var tower = state.Map.Objects.OfType<ObservationTower>().First();

        var loaded = GameState.Load(state.Save(), TestData.Data);
        Assert.True(loaded.Map.Objects.OfType<ObservationTower>().Single(t => t.Id == tower.Id).Large);
        Assert.False(loaded.Map.IsPassable(tower.Position.Offset(0, -1)));

        var json = state.Save().Replace($"\"version\":{GameState.SaveVersion}", "\"version\":6");
        var old = GameState.Load(json, TestData.Data);
        var oldTower = old.Map.Objects.OfType<ObservationTower>().Single(t => t.Id == tower.Id);
        Assert.False(oldTower.Large);
        Assert.Single(oldTower.Footprint);
        Assert.False(old.Map.Objects.OfType<SageHut>().First().Large);
        // sauvegardée de nouveau, elle reste sur une case
        var again = GameState.Load(old.Save(), TestData.Data);
        Assert.False(again.Map.Objects.OfType<ObservationTower>().Single(t => t.Id == tower.Id).Large);
    }
}
