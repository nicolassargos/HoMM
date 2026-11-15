using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>La Forêt (étape 9) : ses capacités au combat, sa ville, la classe Rôdeur.</summary>
public class ForetTests
{
    private const string Forest = "foret";

    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static Army ArmyOf(params (string Id, int Count)[] stacks)
    {
        var army = new Army();
        foreach (var (id, count) in stacks)
            army.Add(Creature(id), count);
        return army;
    }

    /// <summary>Un combat sans obstacle ; la première troupe de chaque camp côte à côte, c'est au tour de l'attaquant.</summary>
    private static (TacticalBattle Battle, BattleUnit Attacker, BattleUnit Defender) Duel(Army attackers, Army defenders, int seed = 5)
    {
        var battle = new TacticalBattle(attackers, defenders, new GameRandom(seed));
        battle.Field.Obstacles.Clear();
        var first = battle.Units.First(u => u.Side == BattleSide.Attacker);
        var second = battle.Units.First(u => u.Side == BattleSide.Defender);
        second.Position = new Hex(5, 4);
        battle.ForceTurn(first, new Hex(4, 4));
        return (battle, first, second);
    }

    [Fact]
    public void Les_sept_creatures_de_la_foret_un_par_niveau_et_le_troll_n_est_plus_neutre()
    {
        var forest = TestData.Creatures.All.Where(c => c.Faction == Forest).OrderBy(c => c.Tier).ToList();
        Assert.Equal(new[] { "farfadet", "sanglier", "archer-elfe", "cerf", "druide", "treant", "troll" }, forest.Select(c => c.Id));
        Assert.All(forest, c => Assert.Contains(Ability.NatureRegeneration, c.Abilities));
        Assert.Contains(Forest, MapGenerator.MonsterFactions);
        Assert.Equal(new[] { "dragon" }, TestData.Creatures.All.Where(c => c.Faction == "neutre").Select(c => c.Id));
    }

    [Fact]
    public void La_regeneration_de_la_nature_rend_un_quart_des_points_de_vie_au_debut_du_tour()
    {
        var (battle, peasants, boars) = Duel(ArmyOf(("paysan", 1)), ArmyOf(("sanglier", 3)));
        int hp = boars.Type.HitPoints;
        boars.TakeDamage(hp / 2); // la créature de tête à moitié
        int before = boars.TopHitPoints;

        while (battle.Active != boars && !battle.IsOver)
            battle.Defend();

        Assert.Equal(System.Math.Min(hp, before + (int)System.Math.Ceiling(hp * TacticalBattle.NatureRegenerationShare)), boars.TopHitPoints);
        Assert.Contains(battle.TakeLog(), a => a is UnitRegenerated r && r.Unit == boars);
    }

    [Fact]
    public void L_archer_elfe_tire_deux_fois()
    {
        var (battle, elves, peasants) = Duel(ArmyOf(("archer-elfe", 5)), ArmyOf(("paysan", 500)));
        elves.Position = new Hex(0, 4); // loin : aucun ennemi à côté

        Assert.True(battle.Shoot(peasants));

        Assert.Equal(2, battle.TakeLog().OfType<UnitStruck>().Count(s => s.Attacker == elves && s.Ranged));
    }

    [Fact]
    public void Le_cerf_bondit_par_dessus_les_troupes()
    {
        var (battle, stags, peasants) = Duel(ArmyOf(("cerf", 1)), ArmyOf(("paysan", 5)));
        battle.ForceTurn(stags, new Hex(4, 4));

        // les paysans sont juste devant lui : il passe par-dessus (à vol d'oiseau, au sol)
        Assert.Contains(new Hex(6, 4), battle.Reachable(stags).Keys);
    }

    [Fact]
    public void Le_druide_soigne_les_troupes_amies_voisines_au_debut_de_son_tour()
    {
        var battle = new TacticalBattle(ArmyOf(("druide", 4), ("sanglier", 5)), ArmyOf(("paysan", 1)), new GameRandom(5));
        battle.Field.Obstacles.Clear();
        var druids = battle.Units.Single(u => u.Type.Id == "druide");
        var boars = battle.Units.Single(u => u.Type.Id == "sanglier");
        boars.Position = druids.Position.Neighbours().First(h => Battlefield.InBounds(h) && battle.UnitAt(h) == null);
        boars.TakeDamage(boars.Type.HitPoints - 1); // la créature de tête presque morte
        // le tour du druide (après les sangliers, plus rapides)
        while (battle.Active != druids && !battle.IsOver)
            battle.Defend();

        var soothed = Assert.Single(battle.TakeLog().OfType<UnitSoothed>());
        Assert.Equal(boars, soothed.Unit);
        Assert.Equal(TacticalBattle.HealingAuraPerCreature * druids.Count, soothed.HitPoints);
    }

    [Fact]
    public void La_cible_du_treant_est_enracinee_a_son_tour_suivant()
    {
        var (battle, treants, peasants) = Duel(ArmyOf(("treant", 1)), ArmyOf(("paysan", 100)));

        battle.Attack(peasants, new Hex(4, 4));

        Assert.Contains(battle.TakeLog(), a => a is UnitRooted r && r.Unit == peasants);
        while (battle.Active != peasants && !battle.IsOver)
            battle.Defend();
        // les paysans ne bougent pas, mais peuvent frapper le tréant à côté d'eux
        Assert.Equal(new[] { peasants.Position }, battle.Reachable(peasants).Keys);
        Assert.NotEmpty(battle.AttackPositions(peasants, treants));
        battle.Defend();
        Assert.False(peasants.IsRooted); // une fois seulement
    }

    // ------------------------------------------------------------------ la ville et la classe

    private static GameState ForestGame(int seed = 5) => new(seed, TestData.Data, MapSize.Small, 0, faction: Forest);

    [Fact]
    public void Un_joueur_de_la_foret_commence_avec_un_rodeur()
    {
        var state = ForestGame();
        var hero = state.Players[0].Heroes.Single();
        var castle = state.Players[0].Towns.Single();

        Assert.Equal(Forest, castle.Faction);
        Assert.True(castle.Has("bosquet-farfadets"));
        Assert.Equal("rodeur", hero.Class?.Id);
        Assert.Equal((3, 1, 1, 2, 9), (hero.Attack, hero.Defense, hero.Power, hero.Knowledge, hero.Charisma));
        Assert.Contains(hero.Spells, s => s.Id == "soins");
        Assert.Equal(new[] { ("farfadet", 15), ("sanglier", 5), ("archer-elfe", 2) }, hero.Army.Stacks.Select(s => (s.Type.Id, s.Count)));
        Assert.Equal(Forest, TestData.Data.Heroes.All.Single(h => h.Name == hero.Name).Faction);
    }

    [Fact]
    public void La_ville_de_la_foret_a_ses_sept_habitations()
    {
        var castle = ForestGame().Players[0].Towns.Single();
        Assert.Equal(new[] { "bosquet-farfadets", "bauge-sangliers", "camp-archers", "clairiere-cerfs", "cercle-druides", "bois-treants", "antre-troll" },
            Enumerable.Range(1, 7).Select(t => castle.DwellingOfTier(t)!.Id));
        var b = TestData.Data.Buildings;
        Assert.Equal(2, b.Get("cercle-druides").Cost[ResourceType.Crystal]);
        Assert.Equal(5, b.Get("antre-troll").Cost[ResourceType.Gems]);
        Assert.Contains(b.Get("grand-hotel-ville").Requires, r => r.Split('|').Contains("clairiere-cerfs"));
    }

    [Fact]
    public void Un_adversaire_de_la_foret_batit_ses_habitations()
    {
        var state = new GameState(7, TestData.Data, 54, 40, opponents: 1, opponentFactions: new[] { Forest });
        state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
        state.Players[0].Heroes[0].Army.Add(Creature("ange"), 50);
        for (int day = 0; day < 14; day++)
        {
            state.EndTurn();
            while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
            {
                ComputerPlayer.PlayTurn(state);
                state.EndTurn();
            }
        }
        var rival = state.Players[1];
        Assert.Equal("rodeur", rival.Heroes[0].Class?.Id);
        var castle = rival.Towns.First(t => t.Faction == Forest);
        Assert.True(castle.Has("bauge-sangliers"));
    }
}
