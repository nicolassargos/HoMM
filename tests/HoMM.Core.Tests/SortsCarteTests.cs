using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Les sorts de la carte (idées de l'utilisateur, 2026-11-24) : l'invocation de bateau, le vent porteur, la porte
/// dimensionnelle, le retour en ville.
/// </summary>
public class SortsCarteTests
{
    private static SpellType Spell(string id) => TestData.Spells.Get(id);

    /// <summary>Le héros au bord de la mer (<see cref="MerTests.GameByTheSea"/>), sans bateau, qui connaît ce sort et a ses points.</summary>
    private static (GameState State, Hero Hero, SpellType Spell) Caster(string id)
    {
        var (state, hero) = MerTests.GameByTheSea();
        state.Map.RemoveObject(state.Map.ObjectAt(new Cell(7, 4))!);
        var spell = Spell(id);
        hero.Learn(spell);
        hero.SpellPoints = 100;
        return (state, hero, spell);
    }

    [Fact]
    public void Les_sorts_de_la_carte_ne_se_lancent_pas_au_combat()
    {
        Assert.All(new[] { "invocation-bateau", "vent-porteur", "porte-dimensionnelle", "retour-en-ville" },
            id => Assert.True(Spell(id).OnMap));
        Assert.False(Spell("eclair").OnMap);
        var (state, hero, eclair) = Caster("eclair");
        Assert.Equal(MapSpellResult.NotAvailable, state.CanCastOnMap(hero, eclair));
    }

    [Fact]
    public void L_invocation_pose_un_bateau_sur_l_eau_a_cote()
    {
        var (state, hero, spell) = Caster("invocation-bateau");
        Assert.Equal(MapSpellResult.NoWater, state.CanCastOnMap(hero, spell)); // en (4, 4), la mer est loin
        MerTests.Walk(state, new Cell(6, 4));
        int points = hero.SpellPoints;

        Assert.Equal(MapSpellResult.Ok, state.CastOnMap(hero, spell));

        var boat = state.TakeEvents().OfType<MapSpellCast>().Single().Boat!.Value;
        Assert.True(state.Map.IsWater(boat));
        Assert.IsType<Boat>(state.Map.ObjectAt(boat));
        Assert.Equal(1, System.Math.Max(System.Math.Abs(boat.X - 6), System.Math.Abs(boat.Y - 4)));
        Assert.Equal(points - 12, hero.SpellPoints);
    }

    [Fact]
    public void Le_vent_porteur_double_le_mouvement_une_fois_par_jour()
    {
        var (state, hero, spell) = Caster("vent-porteur");
        hero.SpendMovement(500);
        int left = hero.Movement;

        Assert.Equal(MapSpellResult.Ok, state.CastOnMap(hero, spell));
        Assert.Equal(2 * left, hero.Movement);
        Assert.Equal(MapSpellResult.AlreadyToday, state.CanCastOnMap(hero, spell));

        state.EndTurn();
        while (state.CurrentPlayer != hero.Owner)
            state.EndTurn();
        Assert.Equal(MapSpellResult.Ok, state.CanCastOnMap(hero, spell));
    }

    [Fact]
    public void La_porte_dimensionnelle_traverse_la_mer_jusqu_a_dix_cases()
    {
        var (state, hero, spell) = Caster("porte-dimensionnelle");
        hero.Owner.Fog.Reveal(new Cell(14, 4), 6);
        MerTests.Walk(state, new Cell(5, 4));
        int movement = hero.Movement;

        Assert.Equal(MapSpellResult.BadTarget, state.CastOnMap(hero, spell, new Cell(9, 4)));  // sur l'eau
        Assert.Equal(MapSpellResult.BadTarget, state.CastOnMap(hero, spell, new Cell(16, 4))); // à 11 cases
        Assert.Equal(MapSpellResult.Ok, state.CastOnMap(hero, spell, new Cell(15, 4)));        // à 10 cases, l'autre rive

        Assert.Equal(new Cell(15, 4), hero.Position);
        Assert.Equal(movement - SpellType.DimensionDoorMovement, hero.Movement);
    }

    [Fact]
    public void La_porte_ne_va_pas_dans_le_brouillard_et_sert_deux_fois_par_jour()
    {
        var (state, hero, spell) = Caster("porte-dimensionnelle");
        var far = new Cell(4, 13);
        Assert.False(hero.Owner.Fog.IsExplored(far));
        Assert.Equal(MapSpellResult.BadTarget, state.CastOnMap(hero, spell, far));

        Assert.Equal(MapSpellResult.Ok, state.CastOnMap(hero, spell, new Cell(4, 6)));
        Assert.Equal(MapSpellResult.Ok, state.CastOnMap(hero, spell, new Cell(4, 8)));
        Assert.Equal(MapSpellResult.AlreadyToday, state.CanCastOnMap(hero, spell, new Cell(4, 10)));
    }

    [Fact]
    public void La_porte_pres_d_un_gardien_declenche_le_combat()
    {
        var (state, hero, spell) = Caster("porte-dimensionnelle");
        state.Map.AddObject(new MonsterGuard(new Cell(4, 8), TestData.Creatures.Get("gobelin"), 2));
        hero.Owner.Fog.Reveal(new Cell(4, 8), 3);

        Assert.Equal(MapSpellResult.Ok, state.CastOnMap(hero, spell, new Cell(4, 7)));

        Assert.Contains(state.TakeEvents(), e => e is BattleStarted or BattleFought or GuardEncounter);
    }

    [Fact]
    public void Le_retour_en_ville_mene_a_la_ville_la_plus_proche()
    {
        var (state, hero, spell) = Caster("retour-en-ville");
        Assert.Equal(MapSpellResult.NoTown, state.CanCastOnMap(hero, spell));
        var near = new Town(new Cell(4, 16), "Prèsville", "chateau", TestData.Data);
        var far = new Town(new Cell(4, 30), "Loinville", "chateau", TestData.Data);
        foreach (var town in new[] { near, far })
        {
            state.Map.AddObject(town);
            town.SetOwner(hero.Owner);
        }
        int points = hero.SpellPoints;

        Assert.Equal(MapSpellResult.Ok, state.CastOnMap(hero, spell));

        Assert.Equal(near.Position, hero.Position);
        Assert.Equal(0, hero.Movement);
        Assert.Equal(points - 20, hero.SpellPoints);
        Assert.Contains(state.TakeEvents(), e => e is TownEntered entered && entered.Town == near);
    }

    [Fact]
    public void Le_retour_en_ville_demande_du_mouvement()
    {
        var (state, hero, spell) = Caster("retour-en-ville");
        var town = new Town(new Cell(4, 16), "Prèsville", "chateau", TestData.Data);
        state.Map.AddObject(town);
        town.SetOwner(hero.Owner);
        hero.SpendMovement(hero.Movement - SpellType.TownPortalMovement + 1);

        Assert.Equal(MapSpellResult.NotEnoughMovement, state.CastOnMap(hero, spell));
        Assert.Equal(new Cell(4, 4), hero.Position);
    }

    [Fact]
    public void Pas_assez_de_points_de_sort()
    {
        var (state, hero, spell) = Caster("vent-porteur");
        hero.SpellPoints = 14;
        Assert.Equal(MapSpellResult.NotEnoughSpellPoints, state.CastOnMap(hero, spell));
        Assert.Equal(14, hero.SpellPoints);
    }

    [Fact]
    public void Les_sauts_du_jour_sont_sauvegardes()
    {
        var (state, hero, spell) = Caster("porte-dimensionnelle");
        state.CastOnMap(hero, spell, new Cell(4, 6));
        state.CastOnMap(state.CurrentHero!, Spell("porte-dimensionnelle"), new Cell(4, 8));

        var loaded = GameState.Load(state.Save(), TestData.Data);

        Assert.Equal(2, loaded.CurrentHero!.DimensionDoorsToday);
    }
}
