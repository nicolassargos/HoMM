using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Le trésor enfoui (le Plastron des Rois) : sa place sur la carte, les obélisques et la carte au trésor, creuser,
/// la victoire « Trouver le trésor », et les conditions qu'on peut cocher ensemble.
/// </summary>
public class TresorTests
{
    private static double Distance(Cell a, Cell b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(99)]
    public void Le_tresor_est_enfoui_loin_du_depart_sur_une_case_libre(int seed)
    {
        var map = new MapGenerator(seed, TestData.Data).Generate();

        var treasure = Assert.IsType<Cell>(map.Treasure);
        double farthest = new[] { new Cell(0, 0), new Cell(map.Width - 1, 0), new Cell(0, map.Height - 1), new Cell(map.Width - 1, map.Height - 1) }
            .Max(c => Distance(c, map.Start));
        Assert.True(Distance(treasure, map.Start) >= farthest / 2, "trésor trop près du départ");
        // rien n'est posé dessus, et on peut y aller (en combattant les monstres du chemin)
        Assert.True(map.IsPassable(treasure));
        Assert.Null(map.OccupantAt(treasure));
        Assert.NotNull(Pathfinder.FindPath(map, map.Start, treasure));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void Six_obelisques_dans_des_zones_differentes(int seed)
    {
        var map = new MapGenerator(seed, TestData.Data).Generate();

        var obelisks = map.Objects.OfType<Obelisk>().ToList();
        Assert.Equal(6, obelisks.Count);
        foreach (var obelisk in obelisks)
            Assert.NotNull(Pathfinder.FindPath(map, map.Start, obelisk.Position));
        // bien répartis : pas deux obélisques côte à côte
        foreach (var a in obelisks)
            Assert.DoesNotContain(obelisks, b => b != a && Distance(a.Position, b.Position) < 4);
    }

    /// <summary>Une partie où le héros est juste à côté d'un obélisque, sans monstre autour.</summary>
    private static (GameState State, Obelisk Obelisk) GameNextToObelisk()
    {
        var state = new GameState(42, TestData.Data);
        state.TakeEvents();
        var hero = state.CurrentHero!;
        var cell = hero.Position.Offset(1, 0);
        foreach (var obj in state.Map.Objects.Where(o => o.Footprint.Contains(cell) || o is MonsterGuard g
                     && Math.Max(Math.Abs(g.Position.X - cell.X), Math.Abs(g.Position.Y - cell.Y)) <= 2).ToList())
            state.Map.RemoveObject(obj);
        state.Map.SetTerrain(cell, TerrainType.Grass);
        var obelisk = new Obelisk(cell);
        state.Map.AddObject(obelisk);
        return (state, obelisk);
    }

    [Fact]
    public void Un_obelisque_devoile_un_morceau_de_la_carte_une_seule_fois()
    {
        var (state, obelisk) = GameNextToObelisk();
        var player = state.Players[0];
        int index = state.Obelisks.ToList().IndexOf(obelisk);
        Assert.False(state.TreasureMapPieces(player)[index]);

        state.SetDestination(obelisk.Position);
        state.StepAlongPath();

        Assert.True(state.TreasureMapPieces(player)[index]);
        Assert.Equal(1, state.TreasureMapPieces(player).Count(p => p));
        Assert.Contains(state.TakeEvents(), e => e is ObeliskVisited { FirstVisit: true });
        Assert.Contains(state.Map.Objects, o => o == obelisk); // il reste sur la carte

        // repasser ne dévoile rien de plus
        state.SetDestination(obelisk.Position.Offset(-1, 0));
        state.StepAlongPath();
        state.SetDestination(obelisk.Position);
        state.StepAlongPath();
        Assert.Contains(state.TakeEvents(), e => e is ObeliskVisited { FirstVisit: false });
        Assert.Equal(1, state.TreasureMapPieces(player).Count(p => p));
    }

    /// <summary>Une partie où le héros se tient sur le trésor, avec toute sa journée de mouvement.</summary>
    private static GameState GameOnTreasure(VictoryCondition victory)
    {
        var state = new GameState(42, TestData.Data);
        state.ChooseRules(new GameRules(victory));
        state.TakeEvents();
        state.Map.Treasure = state.CurrentHero!.Position;
        return state;
    }

    [Fact]
    public void Creuser_au_bon_endroit_fait_gagner_s_il_faut_trouver_le_tresor()
    {
        var state = GameOnTreasure(VictoryCondition.FindTreasure);
        var hero = state.CurrentHero!;

        Assert.Equal(DigResult.Found, state.Dig());

        Assert.Equal(GameOutcome.Victory, state.Outcome);
        Assert.Null(state.Map.Treasure);
        Assert.Contains(hero.Artifacts, a => a.Id == ArtifactCatalog.TreasureId);
        var events = state.TakeEvents();
        Assert.Contains(events, e => e is TreasureDug);
        Assert.Contains(events, e => e is GameEnded { Reason: GameEndReason.TreasureFound });
    }

    [Fact]
    public void Dans_une_autre_partie_le_plastron_donne_ses_bonus()
    {
        var state = GameOnTreasure(VictoryCondition.ClearGuards);
        var hero = state.CurrentHero!;
        var plastron = TestData.Data.Artifacts.Get(ArtifactCatalog.TreasureId);
        var (attack, defense, charisma) = (hero.Attack, hero.Defense, hero.Charisma);

        Assert.Equal(DigResult.Found, state.Dig());

        Assert.Equal(GameOutcome.Playing, state.Outcome);
        Assert.Equal(attack + plastron.Attack, hero.Attack);
        Assert.Equal(defense + plastron.Defense, hero.Defense);
        Assert.Equal(charisma + plastron.Charisma, hero.Charisma);
    }

    [Fact]
    public void Creuser_ailleurs_ne_donne_rien_et_coute_la_journee()
    {
        var state = GameOnTreasure(VictoryCondition.FindTreasure);
        state.Map.Treasure = state.CurrentHero!.Position.Offset(5, 5);

        Assert.Equal(DigResult.Nothing, state.Dig());

        Assert.Equal(0, state.CurrentHero!.Movement);
        Assert.NotNull(state.Map.Treasure);
        Assert.Equal(GameOutcome.Playing, state.Outcome);
        Assert.Contains(state.TakeEvents(), e => e is NothingDug);
    }

    [Fact]
    public void Il_faut_une_journee_entiere_pour_creuser()
    {
        var state = GameOnTreasure(VictoryCondition.FindTreasure);
        var hero = state.CurrentHero!;
        hero.SpendMovement(1);

        Assert.Equal(DigResult.NeedsFullDay, state.Dig());
        Assert.NotNull(state.Map.Treasure); // rien n'a changé
        Assert.Equal(hero.MaxMovement - 1, hero.Movement);
    }

    [Fact]
    public void La_premiere_condition_accomplie_fait_gagner()
    {
        // monstres ou trésor : déterrer le trésor suffit, même s'il reste des monstres
        var state = GameOnTreasure(VictoryCondition.ClearGuards | VictoryCondition.FindTreasure);
        Assert.Contains(state.Map.Objects, o => o is MonsterGuard);

        state.Dig();

        Assert.Equal(GameOutcome.Victory, state.Outcome);
    }

    [Fact]
    public void Il_faut_au_moins_une_facon_de_gagner()
    {
        var state = new GameState(42, TestData.Data);

        Assert.False(state.ChooseRules(new GameRules(VictoryCondition.None)));
        Assert.Equal(GameRules.Default, state.Rules);
    }

    [Fact]
    public void Le_choix_des_conditions_indique_ou_est_enfoui_le_tresor()
    {
        var state = new GameState(42, TestData.Data);

        state.ChooseRules(new GameRules(VictoryCondition.CaptureTowns | VictoryCondition.FindTreasure));

        var chosen = Assert.Single(state.TakeEvents().OfType<RulesChosen>());
        Assert.NotNull(chosen.Treasure);
        Assert.Equal(state.TreasureDirection(), chosen.Treasure);
    }

    [Fact]
    public void Tresor_obelisques_et_artefacts_sont_sauvegardes()
    {
        var (state, obelisk) = GameNextToObelisk();
        state.ChooseRules(new GameRules(VictoryCondition.ClearGuards | VictoryCondition.FindTreasure, DayLimit: 150));
        state.SetDestination(obelisk.Position);
        state.StepAlongPath();
        var treasure = state.Map.Treasure;
        var plastron = TestData.Data.Artifacts.Get(ArtifactCatalog.TreasureId);
        var other = state.CurrentHero!;
        other.Artifacts.Add(plastron); // un artefact porté (ses bonus sont déjà dans les caractéristiques)

        var loaded = GameState.Load(state.Save(), TestData.Data);

        Assert.Equal(treasure, loaded.Map.Treasure);
        Assert.Equal(state.Rules, loaded.Rules);
        Assert.Equal(state.TreasureMapPieces(state.Players[0]), loaded.TreasureMapPieces(loaded.Players[0]));
        Assert.Contains(loaded.HeroById(other.Id)!.Artifacts, a => a.Id == plastron.Id);
        Assert.Equal(other.Attack, loaded.HeroById(other.Id)!.Attack);
    }

    [Fact]
    public void Une_ancienne_sauvegarde_garde_sa_condition()
    {
        // avant : une seule condition, écrite « ClearGuards » ; elle se lit toujours
        var json = new GameState(42, TestData.Data).Save();
        Assert.Contains("\"ClearGuards\"", json);

        var loaded = GameState.Load(json, TestData.Data);

        Assert.Equal(VictoryCondition.ClearGuards, loaded.Rules.Victory);
    }
}
