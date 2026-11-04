using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les grandes créatures (capacité « two-hex ») : le Cavalier et le Dragon occupent 2 cases au combat.</summary>
public class GrandesCreaturesTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static Army ArmyOf(CreatureType type, int count)
    {
        var army = new Army();
        army.Add(type, count);
        return army;
    }

    private static TacticalBattle Fight(CreatureType attacker, CreatureType defender)
    {
        var battle = new TacticalBattle(ArmyOf(attacker, 5), ArmyOf(defender, 5), new GameRandom(3));
        battle.Field.Obstacles.Clear();
        battle.TakeLog();
        return battle;
    }

    [Fact]
    public void Le_cavalier_et_le_dragon_sont_de_grandes_creatures()
    {
        Assert.Contains(Ability.TwoHex, Creature("cavalier").Abilities);
        Assert.Contains(Ability.TwoHex, Creature("dragon").Abilities);
        Assert.DoesNotContain(Ability.TwoHex, Creature("paysan").Abilities);
    }

    [Fact]
    public void Une_grande_creature_occupe_deux_cases_son_arriere_du_cote_de_son_camp()
    {
        var battle = Fight(Creature("cavalier"), Creature("dragon"));
        var knights = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var dragons = battle.Units.Single(u => u.Side == BattleSide.Defender);

        // l'arrière sur la colonne du camp, la tête un pas devant
        Assert.Equal(new[] { new Hex(1, 4), new Hex(0, 4) }, knights.Hexes);
        Assert.Equal(new[] { new Hex(9, 4), new Hex(10, 4) }, dragons.Hexes);
        Assert.Same(knights, battle.UnitAt(new Hex(0, 4)));
        Assert.Same(dragons, battle.UnitAt(new Hex(10, 4)));
    }

    [Fact]
    public void Une_grande_creature_ne_va_que_la_ou_ses_deux_cases_tiennent()
    {
        var battle = Fight(Creature("cavalier"), Creature("paysan"));
        var knights = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        battle.Field.Obstacles.Add(new Hex(3, 4)); // l'arrière ne tiendrait pas avec la tête en (4, 4)

        var reachable = battle.Reachable(knights);

        Assert.DoesNotContain(new Hex(4, 4), reachable.Keys);
        Assert.Contains(new Hex(4, 3), reachable.Keys);
        Assert.All(reachable.Keys, h => Assert.True(battle.Fits(knights, h)));
    }

    [Fact]
    public void On_attaque_une_grande_creature_par_son_arriere()
    {
        var battle = Fight(Creature("paysan"), Creature("dragon"));
        var peasants = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var dragons = battle.Units.Single(u => u.Side == BattleSide.Defender);
        dragons.Position = new Hex(5, 4); // arrière en (6, 4)
        battle.ForceTurn(peasants, new Hex(7, 4)); // à côté de l'arrière seulement
        battle.TakeLog();

        Assert.True(battle.Attack(dragons, new Hex(7, 4)));
        Assert.Contains(battle.TakeLog(), a => a is UnitStruck s && s.Target == dragons && !s.Retaliation);
    }

    [Fact]
    public void Une_grande_creature_frappe_aussi_depuis_son_arriere()
    {
        var battle = Fight(Creature("cavalier"), Creature("paysan"));
        var knights = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var peasants = battle.Units.Single(u => u.Side == BattleSide.Defender);
        peasants.Position = new Hex(2, 4);
        battle.ForceTurn(knights, new Hex(4, 4)); // tête en (4, 4), arrière en (3, 4), à côté des paysans

        Assert.Contains(new Hex(4, 4), battle.AttackPositions(knights, peasants));
        Assert.True(battle.IsEngaged(knights));
        Assert.True(battle.Attack(peasants, new Hex(4, 4)));
    }

    [Fact]
    public void La_distance_d_un_tir_compte_la_case_la_plus_proche()
    {
        var battle = Fight(Creature("archer"), Creature("dragon"));
        var archers = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var dragons = battle.Units.Single(u => u.Side == BattleSide.Defender);
        archers.Position = new Hex(0, 4);
        dragons.Position = new Hex(6, 4); // tête en (6, 4) à 6 cases, arrière en (7, 4)

        Assert.Equal(6, TacticalBattle.Distance(archers, dragons));
    }

    [Fact]
    public void Le_souffle_ne_touche_pas_deux_fois_une_grande_cible()
    {
        var battle = Fight(Creature("dragon"), Creature("cavalier"));
        var dragons = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var knights = battle.Units.Single(u => u.Side == BattleSide.Defender);
        knights.Position = new Hex(5, 4); // arrière en (6, 4), juste derrière la tête dans la direction du coup
        battle.ForceTurn(dragons, new Hex(4, 4));
        battle.TakeLog();

        battle.Attack(knights, new Hex(4, 4));

        Assert.Single(battle.TakeLog().OfType<UnitStruck>(), s => s.Attacker == dragons && s.Target == knights);
    }

    [Fact]
    public void Le_combat_rapide_se_joue_avec_de_grandes_creatures()
    {
        var attacker = new Army();
        attacker.Add(Creature("cavalier"), 6);
        attacker.Add(Creature("paysan"), 40);
        var defender = new Army();
        defender.Add(Creature("dragon"), 1);
        defender.Add(Creature("loup"), 10);
        var battle = new TacticalBattle(attacker, defender, new GameRandom(5));

        BattleAI.PlayOut(battle);

        Assert.True(battle.IsOver);
        // jamais deux troupes sur la même case
        var taken = battle.Units.Where(u => u.IsAlive).SelectMany(u => u.Hexes).ToList();
        Assert.Equal(taken.Count, taken.Distinct().Count());
    }
}
