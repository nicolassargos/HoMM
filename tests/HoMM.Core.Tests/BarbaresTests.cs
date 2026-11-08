using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les Barbares (étape 9) : leurs capacités au combat (rage, frappe et repart, foudre, griffes).</summary>
public class BarbaresTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static Army ArmyOf(string id, int count)
    {
        var army = new Army();
        army.Add(Creature(id), count);
        return army;
    }

    /// <summary>Un combat sans obstacle ; les deux troupes côte à côte, c'est au tour de l'attaquant.</summary>
    private static (TacticalBattle Battle, BattleUnit Attacker, BattleUnit Defender) Duel(string attacker, int a, string defender, int d, int seed = 5)
    {
        var battle = new TacticalBattle(ArmyOf(attacker, a), ArmyOf(defender, d), new GameRandom(seed));
        battle.Field.Obstacles.Clear();
        var first = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        var second = battle.Units.Single(u => u.Side == BattleSide.Defender);
        second.Position = new Hex(5, 4);
        battle.ForceTurn(first, new Hex(4, 4));
        return (battle, first, second);
    }

    [Fact]
    public void Les_sept_barbares_existent_un_par_niveau_et_enragent_tous()
    {
        var barbarians = TestData.Creatures.All.Where(c => c.Faction == "barbares").OrderBy(c => c.Tier).ToList();
        Assert.Equal(new[] { "gobelin", "loup", "orc", "ogre", "nomades", "oiseau-tonnerre", "behemoth" }, barbarians.Select(c => c.Id));
        Assert.All(barbarians, c => Assert.Contains(Ability.Rage, c.Abilities));
        Assert.Contains("barbares", MapGenerator.MonsterFactions);
    }

    [Fact]
    public void Une_troupe_barbare_frappee_gagne_de_l_attaque_jusqu_a_un_maximum()
    {
        var (battle, peasants, ogres) = Duel("paysan", 5, "ogre", 10);
        int before = ogres.Attack;

        battle.Attack(ogres, new Hex(4, 4));

        Assert.Equal(before + TacticalBattle.RagePerHit, ogres.Attack);
        Assert.Contains(battle.TakeLog(), a => a is UnitEnraged { Rage: TacticalBattle.RagePerHit });
        for (int i = 0; i < 10; i++)
            ogres.Enrage(TacticalBattle.RagePerHit, TacticalBattle.RageMax);
        Assert.Equal(before + TacticalBattle.RageMax, ogres.Attack);
    }

    [Fact]
    public void Les_nomades_frappent_puis_repartent_avec_le_mouvement_qui_leur_reste()
    {
        var (battle, nomads, zombies) = Duel("nomades", 5, "zombie", 10);
        nomads.Position = new Hex(2, 4);
        battle.ForceTurn(nomads);

        Assert.True(battle.Attack(zombies, new Hex(4, 4))); // deux cases parcourues
        Assert.Equal(nomads, battle.Active);
        Assert.Equal(nomads.Type.Speed - 2, battle.RetreatSteps);
        // il ne peut plus frapper, tirer ni attendre : seulement repartir ou rester
        Assert.False(battle.Attack(zombies, new Hex(4, 4)));
        Assert.False(battle.Wait());
        Assert.All(battle.Reachable(nomads).Values, path => Assert.True(path.Count <= nomads.Type.Speed - 2));

        Assert.True(battle.Move(new Hex(1, 4))); // leur cheval sur (0, 4)
        Assert.NotEqual(nomads, battle.Active);
        Assert.False(battle.IsRetreating);
    }

    [Fact]
    public void Rester_sur_place_apres_avoir_frappe_ne_donne_pas_le_bonus_de_defense()
    {
        var (battle, nomads, zombies) = Duel("nomades", 5, "zombie", 10);

        battle.Attack(zombies, new Hex(4, 4));
        Assert.True(battle.Defend());

        Assert.False(nomads.IsDefending);
        Assert.False(battle.IsRetreating);
    }

    [Fact]
    public void L_IA_fait_repartir_les_nomades_hors_de_portee_des_zombies()
    {
        var (battle, nomads, zombies) = Duel("nomades", 5, "zombie", 10);
        battle.Attack(zombies, new Hex(4, 4));

        BattleAI.PlayTurn(battle);

        Assert.True(TacticalBattle.Distance(nomads, zombies) > battle.SpeedOf(zombies) + 1);
    }

    [Fact]
    public void L_oiseau_tonnerre_appelle_parfois_la_foudre_a_l_attaque()
    {
        int bolts = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var (battle, birds, peasants) = Duel("oiseau-tonnerre", 2, "paysan", 2000, seed);
            battle.Attack(peasants, new Hex(4, 4));
            var log = battle.TakeLog();
            foreach (var bolt in log.OfType<LightningStruck>())
            {
                Assert.Equal(TacticalBattle.LightningPerCreature * 2, bolt.Damage);
                bolts++;
            }
        }
        Assert.InRange(bolts, 2, 20); // une chance sur cinq
    }

    [Fact]
    public void Les_griffes_du_behemoth_ignorent_plus_de_defense_que_le_coup_de_l_ogre()
    {
        Assert.True(TacticalBattle.RendingShare > TacticalBattle.CrushingShare);
        Assert.Contains(Ability.Rending, Creature("behemoth").Abilities);
        Assert.Equal(new() { ["Gold"] = 4900, ["Sulfur"] = 1 },
            Creature("behemoth").Cost.ToDictionary(p => p.Key.ToString(), p => p.Value));
    }
}
