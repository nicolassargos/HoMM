using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Les douves (idée de l'utilisateur, 2026-11-24) : 5 cases du haut vers le bas, 3 rounds ; une troupe qui y entre s'y
/// arrête et perd la fin de son tour (pas d'attaque) ; elle en sort à son tour suivant. Pas de dégâts.
/// </summary>
public class DouvesTests
{
    private static SpellType Moat => TestData.Spells.Get("douves");

    private static Hero Mage()
    {
        var map = new AdventureMap(5, 5);
        var hero = new Hero("Merlin", new Player("Rouge", map), new Cell(1, 1)) { Power = 1, Knowledge = 2 };
        hero.SpellPoints = hero.MaxSpellPoints;
        hero.Learn(Moat);
        return hero;
    }

    /// <summary>Notre troupe au héros ; en face, ces troupes au bord droit. Le champ sans obstacle, à notre tour, en <paramref name="at"/>.</summary>
    private static (TacticalBattle Battle, BattleUnit Ours) Fight(string ours, int count, Hex at, params (string Id, int Count)[] theirs)
    {
        var attacker = new Army();
        attacker.Add(TestData.Creatures.Get(ours), count);
        var defender = new Army();
        foreach (var (id, n) in theirs)
            defender.AddStack(TestData.Creatures.Get(id), n);
        var battle = new TacticalBattle(attacker, defender, new GameRandom(1), Mage());
        battle.Field.Obstacles.Clear();
        var enemies = battle.Units.Where(u => u.Side == BattleSide.Defender).ToList();
        for (int i = 0; i < enemies.Count; i++)
            enemies[i].Position = new Hex(10, i * 2);
        var unit = battle.Units.Single(u => u.Side == BattleSide.Attacker);
        battle.ForceTurn(unit, at);
        return (battle, unit);
    }

    /// <summary>Des douves sur toute la colonne 4 : les rangées 2 à 6, et des obstacles au-dessus et au-dessous.</summary>
    private static void AcrossTheField(TacticalBattle battle)
    {
        foreach (int row in new[] { 0, 1, 7, 8 })
            battle.Field.Obstacles.Add(new Hex(4, row));
        Assert.True(battle.Cast(Moat, new Hex(4, 4)));
    }

    [Fact]
    public void On_s_arrete_dans_les_douves_et_on_en_sort_au_tour_suivant()
    {
        var (battle, ours) = Fight("paysan", 100, new Hex(3, 4), ("paysan", 1));
        AcrossTheField(battle);
        Assert.Equal(5, battle.Moat.Count);

        var reachable = battle.Reachable(ours);
        Assert.Contains(new Hex(4, 4), reachable.Keys);
        Assert.DoesNotContain(new Hex(5, 4), reachable.Keys); // pas au-delà

        Assert.True(battle.Move(new Hex(4, 4)));
        Assert.NotEqual(ours, battle.Active);

        battle.ForceTurn(ours);
        Assert.Contains(new Hex(5, 4), battle.Reachable(ours).Keys);
        Assert.Equal(100, ours.Count); // pas de dégâts
    }

    [Fact]
    public void Pas_d_attaque_depuis_les_douves_ou_l_on_vient_d_entrer()
    {
        var (battle, ours) = Fight("paysan", 100, new Hex(3, 4), ("paysan", 10));
        var enemy = battle.Units.Single(u => u.Side == BattleSide.Defender);
        enemy.Position = new Hex(5, 4);
        AcrossTheField(battle);

        Assert.DoesNotContain(new Hex(4, 4), battle.AttackPositions(ours, enemy));
        Assert.False(battle.Attack(enemy, new Hex(4, 4)));

        // déjà dedans au début de son tour : elle frappe sans bouger
        battle.ForceTurn(ours, new Hex(4, 4));
        Assert.True(battle.Attack(enemy, new Hex(4, 4)));
    }

    [Fact]
    public void Le_chemin_contourne_les_douves_quand_il_peut()
    {
        var (battle, ours) = Fight("paysan", 100, new Hex(3, 6), ("paysan", 1));
        Assert.True(battle.Cast(Moat, new Hex(4, 4))); // rangées 2 à 6

        var path = battle.Reachable(ours)[new Hex(5, 6)];
        Assert.DoesNotContain(path, battle.Moat.Contains);
    }

    [Fact]
    public void Un_volant_passe_par_dessus()
    {
        var (battle, griffins) = Fight("griffon", 10, new Hex(2, 4), ("paysan", 1));
        Assert.True(battle.Cast(Moat, new Hex(4, 4)));

        Assert.Contains(new Hex(6, 4), battle.Reachable(griffins).Keys);
        Assert.True(battle.Move(new Hex(6, 4)));
    }

    [Fact]
    public void Les_troupes_deja_la_ne_sont_pas_touchees()
    {
        var (battle, _) = Fight("paysan", 100, new Hex(1, 4), ("paysan", 50));
        var enemy = battle.Units.Single(u => u.Side == BattleSide.Defender);
        enemy.Position = new Hex(4, 4);

        Assert.True(battle.Cast(Moat, new Hex(4, 4)));

        Assert.Equal(50, enemy.Count);
        Assert.Empty(enemy.Spells);
    }

    [Fact]
    public void Elles_durent_trois_rounds()
    {
        var (battle, _) = Fight("paysan", 100, new Hex(1, 4), ("paysan", 1));
        Assert.True(battle.Cast(Moat, new Hex(4, 4)));
        int cast = battle.Round;
        while (battle.Round < cast + 2)
            Assert.True(battle.Defend());
        Assert.Equal(5, battle.Moat.Count);
        while (battle.Round < cast + 3)
            Assert.True(battle.Defend());
        Assert.Empty(battle.Moat);
    }

    [Fact]
    public void L_ia_les_creuse_entre_les_ennemis_et_ses_tireurs()
    {
        var (battle, _) = Fight("archer", 20, new Hex(1, 4), ("paysan", 100), ("paysan", 100));
        var enemies = battle.Units.Where(u => u.Side == BattleSide.Defender).ToList();
        enemies[0].Position = new Hex(6, 3);
        enemies[1].Position = new Hex(6, 5);

        var choice = BattleAI.ChooseSpell(battle);

        Assert.NotNull(choice);
        Assert.Equal("douves", choice.Value.Spell.Id);
        Assert.InRange(choice.Value.Target.Col, 2, 5);
    }
}
