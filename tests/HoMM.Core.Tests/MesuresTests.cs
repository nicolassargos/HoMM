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
    /// La simulation seule avec un héros qui monte de niveau : en force (attaque, défense) ou en magie (puissance,
    /// savoir, guilde des mages, sorts), sur une carte normale : la magie doit être un choix aussi bon que la force.
    /// </summary>
    [Mesure]
    public void Simulation_force_ou_magie()
    {
        foreach (var style in new[] { BalanceSimulation.Style.Might, BalanceSimulation.Style.Magic })
        {
            var results = Seeds.Select(seed => BalanceSimulation.Play(seed, MapSize.Normal, style)).ToList();
            var won = results.Where(r => r.VictoryDay > 0).Select(r => r.VictoryDay).ToList();
            output.WriteLine($"{style} : victoire {won.Count} fois sur {results.Count}, jour {Range(won)} "
                             + $"({string.Join(" ", results.Select(r => r.VictoryDay))}), niveau {Range(results.Select(r => r.Level).ToList())}, "
                             + $"points de sort dépensés {Range(results.Select(r => r.SpellsCast).ToList())}");
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

    /// <summary>
    /// Un adversaire de chaque faction (Normal), six semaines, sur les mêmes cartes : les factions doivent se valoir.
    /// </summary>
    [Mesure]
    public void Adversaires_par_faction()
    {
        foreach (var faction in TestData.Data.Classes.PlayableFactions)
        {
            var values = new List<int>();
            int towns = 0, battles = 0, defeated = 0;
            foreach (int seed in Seeds)
            {
                var state = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1, opponentFactions: new[] { faction });
                state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
                state.Players[0].Heroes[0].Army.Add(TestData.Creatures.Get("ange"), 50);
                PlayDays(state, 42);
                var rival = state.Players[1];
                values.Add(rival.Heroes.Sum(h => Battle.ExperienceFor(h.Army.Stacks)) + rival.Towns.Sum(t => Battle.ExperienceFor(t.Garrison.Stacks)));
                towns += rival.Towns.Count;
                battles += rival.Statistics.BattlesWon;
                defeated += rival.IsDefeated ? 1 : 0;
            }
            int n = Seeds.Length;
            output.WriteLine($"{faction} : armée {Range(values)} ; par partie : {towns / (double)n:0.0} villes, "
                             + $"{battles / (double)n:0.0} combats gagnés ; éliminé {defeated} fois");
        }
    }

    /// <summary>
    /// La courbe de chaque faction : combien de paysans (sans héros) pour vaincre une unité de chaque créature. Deux
    /// créatures du même niveau devraient en demander à peu près autant.
    /// </summary>
    [Mesure]
    public void Courbe_du_chateau()
    {
        foreach (var faction in TestData.Creatures.All.Select(c => c.Faction).Distinct())
            output.WriteLine($"{faction} : " + string.Join(", ", Curve(faction)));
    }

    /// <summary>
    /// Les Nomades (frappe et repart) contre des armées du même prix en or, avec et sans leur capacité : la part de
    /// l'armée perdue par le vainqueur, sur 20 combats (les deux camps joués par l'IA). Pour voir si la capacité est trop
    /// forte contre les armées lentes.
    /// </summary>
    [Mesure]
    public void Nomades_frappe_et_repart()
    {
        var nomads = TestData.Creatures.Get("nomades");
        var without = nomads with { Abilities = nomads.Abilities.Where(a => a != Ability.HitAndRun).ToList() };
        const int count = 20;
        int gold = count * nomads.Cost[ResourceType.Gold];
        foreach (var id in new[] { "zombie", "ogre", "epeiste", "moine", "vampire", "cavalier", "troll" })
        {
            var enemy = TestData.Creatures.Get(id);
            string Line(CreatureType type)
            {
                int wins = 0;
                double lost = 0;
                for (int seed = 0; seed < 20; seed++)
                {
                    var ours = new Army();
                    ours.Add(type, count);
                    var theirs = new Army();
                    theirs.Add(enemy, Math.Max(1, (int)Math.Round((double)gold / enemy.Cost[ResourceType.Gold])));
                    var battle = new TacticalBattle(ours, theirs, new GameRandom(seed));
                    BattleAI.PlayOut(battle);
                    bool won = battle.Outcome == BattleOutcome.AttackerWon;
                    if (won)
                        wins++;
                    lost += won ? 1 - ours.TotalCount / (double)count : 0;
                }
                return $"{wins}/20 gagnés, pertes {(wins == 0 ? 0 : 100 * lost / wins):0} %";
            }
            output.WriteLine($"{enemy.Plural} : avec {Line(nomads)} ; sans {Line(without)}");
        }
    }

    /// <summary>
    /// Les créatures d'un même niveau, de factions différentes, l'une contre l'autre pour le même prix en or (environ
    /// 3000 or chacune, au moins 3 créatures, sans héros), sur 20 combats joués par l'IA, chacune à son tour attaquante : les victoires de la
    /// première, et ce qui reste au vainqueur. Plus fin que la courbe des paysans pour comparer un niveau.
    /// </summary>
    [Mesure]
    public void Duels_a_prix_egal()
    {
        const int gold = 3000;
        var factions = new[] { "chateau", "morts-vivants", "barbares", "foret" };
        for (int tier = 1; tier <= 7; tier++)
        {
            var creatures = factions.Select(f => TestData.Creatures.All.FirstOrDefault(c => c.Faction == f && c.Tier == tier))
                .OfType<CreatureType>().ToList();
            for (int i = 0; i < creatures.Count; i++)
            for (int j = i + 1; j < creatures.Count; j++)
            {
                var (a, b) = (creatures[i], creatures[j]);
                int Count(CreatureType c) => Math.Max(1, (int)Math.Round((double)Math.Max(gold, 3 * c.Cost[ResourceType.Gold]) / c.Cost[ResourceType.Gold]));
                int winsA = 0;
                double leftA = 0, leftB = 0;
                for (int seed = 0; seed < 20; seed++)
                {
                    bool aAttacks = seed % 2 == 0;
                    var armyA = new Army();
                    armyA.Add(a, Count(a));
                    var armyB = new Army();
                    armyB.Add(b, Count(b));
                    var battle = aAttacks ? new TacticalBattle(armyA, armyB, new GameRandom(seed)) : new TacticalBattle(armyB, armyA, new GameRandom(seed));
                    BattleAI.PlayOut(battle);
                    if (armyA.TotalCount > 0 && armyB.TotalCount == 0)
                    {
                        winsA++;
                        leftA += armyA.TotalCount / (double)Count(a);
                    }
                    else if (armyB.TotalCount > 0)
                        leftB += armyB.TotalCount / (double)Count(b);
                }
                int winsB = 20 - winsA;
                output.WriteLine($"{tier}. {Count(a)} {a.Plural} contre {Count(b)} {b.Plural} : {winsA}/20 (reste {(winsA == 0 ? 0 : 100 * leftA / winsA):0} %)"
                                 + $" contre {winsB}/20 (reste {(winsB == 0 ? 0 : 100 * leftB / winsB):0} %)");
            }
        }
    }

    private static IEnumerable<string> Curve(string faction)
    {
        var line = new List<string>();
        foreach (var creature in TestData.Creatures.All.Where(c => c.Faction == faction && c.Id != "paysan").OrderBy(c => c.Tier))
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
            line.Add($"{creature.Tier}. {creature.Name} {peasants}");
        }
        return line;
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
