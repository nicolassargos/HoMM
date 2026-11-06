using System;
using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;
using Xunit.Abstractions;

namespace HoMM.Core.Tests;

/// <summary>
/// Une mesure : un test qui ne vérifie rien, mais écrit des chiffres (l'équilibre, la force des adversaires…), pour
/// comparer avant et après un changement. Ignorée par un « dotnet test » ordinaire (elle est longue) ; pour la lancer :
/// <c>HOMM_MESURES=1 dotnet test --filter Category=Mesures --logger "console;verbosity=detailed"</c>.
/// </summary>
public sealed class MesureAttribute : FactAttribute
{
    public MesureAttribute()
    {
        if (Environment.GetEnvironmentVariable("HOMM_MESURES") != "1")
            Skip = "Mesure : lancer avec HOMM_MESURES=1 (voir MesuresTests)";
    }
}

/// <summary>
/// Les mesures de l'équilibre (voir <see cref="MesureAttribute"/>). Les mêmes graines à chaque fois : deux mesures se
/// comparent ligne à ligne. Noter les chiffres retenus dans PLAN.md (Repères) ou HISTORIQUE.md.
/// </summary>
[Trait("Category", "Mesures")]
public class MesuresTests(ITestOutputHelper output)
{
    /// <summary>Les cartes mesurées (toujours les mêmes, pour comparer).</summary>
    private static readonly int[] Seeds = { 3, 7, 11, 19, 42, 77 };

    private static string Range(IReadOnlyCollection<int> values) =>
        values.Count == 0 ? "-" : $"moyenne {values.Average():0}, de {values.Min()} à {values.Max()}";

    /// <summary>La simulation seule (<see cref="BalanceSimulation"/>) : le jour de la victoire, sur chaque taille de carte.</summary>
    [Mesure]
    public void Simulation_seule()
    {
        foreach (var size in new[] { MapSize.Small, MapSize.Normal, MapSize.Large })
        {
            var days = Seeds.Select(seed => BalanceSimulation.Play(seed, size).VictoryDay).ToList();
            var won = days.Where(d => d > 0).ToList();
            output.WriteLine($"{size} : victoire {won.Count} fois sur {days.Count}, jour {Range(won)} ({string.Join(" ", days)})");
        }
    }

    /// <summary>
    /// Un adversaire seul face au joueur (qui ne joue pas et ne se fait pas prendre), six semaines, à chaque niveau :
    /// ses villes, ses héros, ses combats gagnés, la valeur de son armée (or), ses sorts.
    /// </summary>
    [Mesure]
    public void Adversaires_en_six_semaines()
    {
        foreach (var difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard })
        {
            var values = new List<int>();
            int towns = 0, heroes = 0, battles = 0, defeated = 0, mages = 0, guilds = 0;
            foreach (int seed in Seeds)
            {
                var state = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1);
                state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents, Difficulty: difficulty));
                state.Players[0].Heroes[0].Army.Add(TestData.Creatures.Get("ange"), 50);
                PlayDays(state, 42);
                var rival = state.Players[1];
                values.Add(rival.Heroes.Sum(h => Battle.ExperienceFor(h.Army.Stacks))
                           + rival.Towns.Sum(t => Battle.ExperienceFor(t.Garrison.Stacks)));
                towns += rival.Towns.Count;
                heroes += rival.Heroes.Count;
                battles += rival.Statistics.BattlesWon;
                defeated += rival.IsDefeated ? 1 : 0;
                mages += rival.Heroes.Count(h => h.Spells.Count > 0);
                guilds += rival.Towns.Count(t => t.MageGuildLevel > 0);
            }
            int n = Seeds.Length;
            output.WriteLine($"{difficulty} : armée {Range(values)} ; par partie : {towns / (double)n:0.0} villes, "
                             + $"{heroes / (double)n:0.0} héros ({mages / (double)n:0.0} avec des sorts), "
                             + $"{guilds / (double)n:0.0} guildes, {battles / (double)n:0.0} combats gagnés ; éliminé {defeated} fois");
        }
    }

    /// <summary>La courbe du Château : combien de paysans (héros de départ) pour vaincre une unité de chaque créature.</summary>
    [Mesure]
    public void Courbe_du_chateau()
    {
        var line = new List<string>();
        foreach (var creature in TestData.Creatures.All.Where(c => c.Faction == "chateau" && c.Tier > 1).OrderBy(c => c.Tier))
        {
            int peasants = 1;
            while (peasants < 100_000)
            {
                var army = new Army();
                army.Add(TestData.Creatures.Get("paysan"), peasants);
                var enemy = new Army();
                enemy.Add(creature, 1);
                if (Battle.Resolve(army, enemy).AttackerWon)
                    break;
                peasants = Math.Max(peasants + 1, (int)(peasants * 1.05));
            }
            line.Add($"{creature.Name} {peasants}");
        }
        output.WriteLine("Paysans pour vaincre une unité : " + string.Join(", ", line));
    }

    /// <summary>Joue des jours : le joueur passe son tour, les adversaires jouent.</summary>
    private static void PlayDays(GameState state, int days)
    {
        for (int day = 0; day < days && state.Outcome == GameOutcome.Playing; day++)
        {
            state.EndTurn();
            while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
            {
                ComputerPlayer.PlayTurn(state);
                state.EndTurn();
            }
        }
    }
}
