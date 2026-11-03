using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>L'IA du combat tactique, et le combat rapide qu'elle joue (étape 6).</summary>
public class CombatIATests
{
    private static Army ArmyOf(params (string Id, int Count)[] stacks)
    {
        var army = new Army();
        foreach (var (id, count) in stacks)
            army.Add(TestData.Creatures.Get(id), count);
        return army;
    }

    private static TacticalBattle Battle(Army attacker, Army defender)
    {
        var battle = new TacticalBattle(attacker, defender, new GameRandom(3));
        battle.Field.Obstacles.Clear();
        return battle;
    }

    [Fact]
    public void Un_tireur_tire_sur_l_ennemi_le_plus_dangereux()
    {
        var battle = Battle(ArmyOf(("archer", 10)), ArmyOf(("paysan", 5), ("ogre", 5)));
        var archers = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var ogres = battle.Units.Single(u => u.Type.Id == "ogre");
        battle.ForceTurn(archers);

        BattleAI.PlayTurn(battle);

        var shot = Assert.Single(battle.TakeLog().OfType<UnitStruck>());
        Assert.True(shot.Ranged);
        Assert.Equal(ogres, shot.Target);
    }

    [Fact]
    public void Une_troupe_attaque_l_ennemi_qu_elle_peut_atteindre()
    {
        var battle = Battle(ArmyOf(("griffon", 5)), ArmyOf(("paysan", 10)));
        var griffons = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var peasants = battle.Units.Single(u => u.Side == BattleSide.Defender);
        peasants.Position = new Hex(5, 4);
        battle.ForceTurn(griffons, new Hex(0, 4));

        BattleAI.PlayTurn(battle);

        Assert.True(griffons.Position.IsNextTo(peasants.Position));
        Assert.Contains(battle.TakeLog(), a => a is UnitStruck { Retaliation: false } s && s.Target == peasants);
    }

    [Fact]
    public void Trop_loin_une_troupe_s_approche()
    {
        var battle = Battle(ArmyOf(("paysan", 10)), ArmyOf(("paysan", 10)));
        var attacker = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var defender = battle.Units.Single(u => u.Side == BattleSide.Defender);
        int before = attacker.Position.DistanceTo(defender.Position);
        battle.ForceTurn(attacker);

        BattleAI.PlayTurn(battle);

        Assert.Equal(before - attacker.Type.Speed, attacker.Position.DistanceTo(defender.Position));
    }

    [Fact]
    public void Le_combat_rapide_joue_tout_le_combat()
    {
        var attacker = ArmyOf(("paysan", 30), ("archer", 15), ("griffon", 4));
        var defender = ArmyOf(("gobelin", 12));

        var report = HoMM.Core.Battle.Resolve(attacker, defender);

        Assert.True(report.AttackerWon);
        Assert.True(defender.IsEmpty);
        Assert.Equal(12, report.DefenderLosses.Sum(l => l.Count));
    }

    [Fact]
    public void Le_combat_rapide_utilise_le_hasard_donne()
    {
        // même hasard, même combat ; un autre hasard peut donner d'autres pertes
        int Losses(int seed)
        {
            // des Ogres (6 à 12 de dégâts) contre des Épéistes : les pertes dépendent des tirages
            var attacker = ArmyOf(("epeiste", 20));
            HoMM.Core.Battle.Resolve(attacker, ArmyOf(("ogre", 12)), rng: new GameRandom(seed));
            return attacker.TotalCount;
        }

        Assert.Equal(Losses(10), Losses(10));
        Assert.True(Enumerable.Range(1, 10).Select(Losses).Distinct().Count() > 1, "le hasard devrait changer les pertes");
    }
}
