using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// La Diplomatie et la fuite des gardiens (décidées avec l'utilisateur, 2026-11-09) : un gardien bien plus faible propose
/// de rejoindre le héros (selon son degré : rang, faction, charisme ; dans son commandement ; tout ou rien), un gardien
/// très faible s'enfuit.
/// </summary>
public class DiplomatieTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    /// <summary>
    /// Une partie où un gardien attend juste à côté du héros (un Paladin, charisme 14, commandement 154), avec cette
    /// armée à la place de la sienne et ce rang de Diplomatie.
    /// </summary>
    private static (GameState State, Hero Hero, MonsterGuard Guard) Game(string guardId, int guardCount, string armyId, int armyCount, int rank)
    {
        var (state, guard) = CarteObjetsTests.GameNextTo(c => new MonsterGuard(c, Creature(guardId), guardCount));
        var hero = state.CurrentHero!;
        foreach (var stack in hero.Army.Stacks)
            stack.Count = 0;
        hero.Army.RemoveEmpty();
        hero.Army.Add(Creature(armyId), armyCount);
        if (rank > 0)
            hero.AddSkillRank(TestData.Skills.Get(Skills.Diplomacy), rank);
        state.TakeEvents();
        return (state, hero, guard);
    }

    [Fact]
    public void Sans_la_diplomatie_un_gardien_plus_faible_ne_propose_rien()
    {
        // 10 Gobelins (60 or) contre 60 Paysans (180 or) : trois fois plus fort, mais pas assez pour qu'ils fuient
        var (_, hero, guard) = Game("gobelin", 10, "paysan", 60, rank: 0);
        Assert.Null(Diplomacy.Encounter(hero, guard));
    }

    [Fact]
    public void Au_rang_de_base_il_faut_trois_fois_sa_valeur_et_il_coute_son_prix()
    {
        var (_, hero, guard) = Game("gobelin", 10, "paysan", 60, rank: 1);
        var offer = Diplomacy.Encounter(hero, guard)?.Offer;
        Assert.NotNull(offer);
        Assert.Equal(60, offer!.Price[ResourceType.Gold]);
        Assert.True(offer.CanAccept);

        hero.Army.Stacks[0].Count = 59; // 177 or : un peu moins de trois fois
        Assert.Null(Diplomacy.Encounter(hero, guard));
    }

    [Fact]
    public void Plus_de_degres_moins_d_armee_et_un_prix_plus_bas()
    {
        Assert.Equal(new[] { 3, 2, 1.5, 1.25, 1, 1 }, Enumerable.Range(1, 6).Select(Diplomacy.RatioAt));
        Assert.Equal(new[] { 100, 50, 0, 0 }, Enumerable.Range(1, 4).Select(Diplomacy.PricePercentAt));

        var (_, hero, guard) = Game("gobelin", 10, "paysan", 40, rank: 2); // 120 or : deux fois
        Assert.Equal(30, Diplomacy.Encounter(hero, guard)!.Offer!.Price[ResourceType.Gold]);
        var (_, expert, guard3) = Game("gobelin", 10, "paysan", 30, rank: 3); // 90 or : une fois et demie
        Assert.True(Diplomacy.Encounter(expert, guard3)!.Offer!.IsFree);
    }

    [Fact]
    public void Un_gardien_de_sa_faction_et_le_charisme_rendent_plus_facile()
    {
        var (_, hero, _) = Game("gobelin", 10, "paysan", 60, rank: 1);
        Assert.Equal(1, Diplomacy.Degree(hero, Creature("gobelin")));
        Assert.Equal(2, Diplomacy.Degree(hero, Creature("archer"))); // le Paladin est du Château
        hero.Charisma = 20;
        Assert.Equal(3, Diplomacy.Degree(hero, Creature("archer")));

        // des Archers (Château) : au degré 2, deux fois leur valeur suffit, à moitié prix
        var (_, paladin, archers) = Game("archer", 5, "paysan", 60, rank: 1); // 90 or contre 180 or
        var offer = Diplomacy.Encounter(paladin, archers)!.Offer!;
        Assert.Equal(45, offer.Price[ResourceType.Gold]);
    }

    [Fact]
    public void Un_gardien_qui_ne_tiendrait_pas_dans_son_commandement_refuse()
    {
        // 200 Gobelins (200 points de commandement) : plus que les 154 du Paladin ; une armée d'Anges qui vaut assez
        var (_, hero, guard) = Game("gobelin", 200, "ange", 1, rank: 1);
        Assert.Equal(JoinRefusal.Command, Diplomacy.Encounter(hero, guard)!.Offer!.Refusal);
    }

    [Fact]
    public void Sans_or_ou_sans_place_il_ne_peut_pas_accepter()
    {
        var (state, hero, guard) = Game("gobelin", 10, "paysan", 60, rank: 1);
        hero.Owner.Resources[ResourceType.Gold] = 10;
        Assert.Equal(JoinRefusal.Price, Diplomacy.Encounter(hero, guard)!.Offer!.Refusal);

        hero.Owner.Resources[ResourceType.Gold] = 5000;
        foreach (var id in new[] { "archer", "griffon", "epeiste", "moine", "cavalier", "squelette" })
            hero.Army.Add(Creature(id), 1);
        Assert.Equal(JoinRefusal.Slot, Diplomacy.Encounter(hero, guard)!.Offer!.Refusal);
        Assert.False(state.ResolveEncounter(EncounterChoice.Join)); // rien n'attend
    }

    [Fact]
    public void Le_gardien_qui_rejoint_l_armee_est_paye_et_quitte_la_carte_sans_combat_ni_experience()
    {
        var (state, hero, guard) = Game("gobelin", 10, "paysan", 60, rank: 1);
        int gold = hero.Owner.Resources[ResourceType.Gold];
        CarteObjetsTests.Visit(state, guard);

        var encounter = Assert.IsType<GuardEncounter>(state.PendingEncounter);
        Assert.Contains(state.TakeEvents(), e => e is GuardEncounter);
        Assert.False(encounter.Flees);
        Assert.True(state.ResolveEncounter(EncounterChoice.Join));

        Assert.Null(state.PendingEncounter);
        Assert.Equal(gold - 60, hero.Owner.Resources[ResourceType.Gold]);
        Assert.Equal(10, hero.Army.Stacks.Single(s => s.Type.Id == "gobelin").Count);
        Assert.DoesNotContain(state.Map.Objects, o => o == guard);
        Assert.Equal(0, hero.Experience);
        var events = state.TakeEvents();
        Assert.Contains(events, e => e is GuardJoined { Count: 10 });
        Assert.DoesNotContain(events, e => e is BattleStarted);
    }

    [Fact]
    public void Refuser_l_offre_mene_au_combat()
    {
        var (state, _, guard) = Game("gobelin", 10, "paysan", 60, rank: 1);
        CarteObjetsTests.Visit(state, guard);
        Assert.True(state.ResolveEncounter(EncounterChoice.Fight));
        Assert.Contains(state.TakeEvents(), e => e is BattleFought);
        Assert.False(state.ResolveEncounter(EncounterChoice.Fight)); // plus rien n'attend
    }

    [Fact]
    public void Un_gardien_tres_faible_s_enfuit_on_le_laisse_partir_ou_on_le_poursuit()
    {
        // 3 Gobelins (18 or) contre 60 Paysans (180 or) : dix fois plus fort, et sans Diplomatie
        var (state, hero, guard) = Game("gobelin", 3, "paysan", 60, rank: 0);
        var encounter = Diplomacy.Encounter(hero, guard)!;
        Assert.True(encounter.Flees);
        Assert.Null(encounter.Offer);

        CarteObjetsTests.Visit(state, guard);
        Assert.False(state.ResolveEncounter(EncounterChoice.Join)); // il ne propose rien
        Assert.True(state.ResolveEncounter(EncounterChoice.LetFlee));
        Assert.DoesNotContain(state.Map.Objects, o => o == guard);
        Assert.Equal(0, hero.Experience);
        Assert.Contains(state.TakeEvents(), e => e is GuardFled { Count: 3 });

        var (chase, _, weak) = Game("gobelin", 3, "paysan", 60, rank: 0);
        CarteObjetsTests.Visit(chase, weak);
        Assert.True(chase.ResolveEncounter(EncounterChoice.Fight));
        Assert.Contains(chase.TakeEvents(), e => e is BattleFought);
    }

    [Fact]
    public void Tant_que_le_choix_attend_on_ne_marche_ni_ne_finit_son_tour_ni_ne_sauvegarde()
    {
        var (state, hero, guard) = Game("gobelin", 3, "paysan", 60, rank: 0);
        CarteObjetsTests.Visit(state, guard);
        Assert.NotNull(state.PendingEncounter);
        int day = state.Day;
        state.EndTurn();
        Assert.Equal(day, state.Day);
        Assert.Throws<System.InvalidOperationException>(() => state.Save());
        state.SetDestination(hero.Position.Offset(-1, 0));
        Assert.False(state.CanStartWalking);
    }

    [Fact]
    public void L_ordinateur_prend_le_gardien_s_il_garde_de_quoi_recruter()
    {
        var (_, hero, guard) = Game("gobelin", 10, "paysan", 60, rank: 1);
        hero.Owner.Resources[ResourceType.Gold] = 5000;
        Assert.Equal(EncounterChoice.Join, ComputerPlayer.ChooseEncounter(Diplomacy.Encounter(hero, guard)!));
        hero.Owner.Resources[ResourceType.Gold] = 1030; // il ne resterait que 970 or
        Assert.Equal(EncounterChoice.Fight, ComputerPlayer.ChooseEncounter(Diplomacy.Encounter(hero, guard)!));
    }
}
