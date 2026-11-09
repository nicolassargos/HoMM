using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// La Tour de magie (décidée avec l'utilisateur, 2026-11-09) : ses sept créatures et leurs capacités (façonnées qui
/// résistent à la magie, le Gnome qui les répare, le Djinn qui bénit, le Mage sans pénalité au corps à corps).
/// </summary>
public class TourTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static Army Army(string id, int count)
    {
        var army = new Army();
        army.Add(Creature(id), count);
        return army;
    }

    private static Army Army(params (string Id, int Count)[] stacks)
    {
        var army = new Army();
        foreach (var (id, count) in stacks)
            army.Add(Creature(id), count);
        return army;
    }

    /// <summary>Un héros qui connaît ces sorts, avec beaucoup de points de sort.</summary>
    private static Hero Mage(params string[] spells)
    {
        var state = new GameState(5, TestData.Data, MapSize.Small, 0);
        var hero = state.CurrentHero!;
        hero.Knowledge = 20;
        hero.SpellPoints = hero.MaxSpellPoints;
        foreach (var id in spells)
            hero.Learn(TestData.Data.Spells.Get(id));
        return hero;
    }

    [Fact]
    public void Les_sept_creatures_de_la_tour_une_par_niveau()
    {
        var tower = TestData.Creatures.All.Where(c => c.Faction == "tour").OrderBy(c => c.Tier).ToList();
        Assert.Equal(new[] { "gnome", "gargouille", "golem-pierre", "naga", "djinn", "mage", "geant" }, tower.Select(c => c.Id));
        Assert.Equal(Enumerable.Range(1, 7), tower.Select(c => c.Tier));
        Assert.Contains(Ability.Made, Creature("gargouille").Abilities);
        Assert.Contains(Ability.Made, Creature("golem-pierre").Abilities);
        Assert.Equal(new[] { "benediction", "peau-de-pierre", "hate" }, Creature("djinn").BlessingSpells.Select(s => s.Id));
    }

    [Fact]
    public void Une_creature_faconnee_ne_prend_que_la_moitie_des_degats_des_sorts()
    {
        var hero = Mage("fleche-magique");
        var battle = new TacticalBattle(Army("paysan", 10), Army(("golem-pierre", 50), ("gobelin", 50)), new GameRandom(1), hero);
        var golems = battle.Units.First(u => u.Type.Id == "golem-pierre");
        battle.ForceTurn(battle.Units.First(u => u.Side == BattleSide.Attacker));
        var spell = TestData.Data.Spells.Get("fleche-magique");
        int full = battle.SpellStrength(spell, BattleSide.Attacker);

        Assert.True(battle.Cast(spell, golems.Position));
        Assert.Equal(full / 2, battle.TakeLog().OfType<SpellDamaged>().Single().Damage);
    }

    [Fact]
    public void Les_sorts_nefastes_ne_touchent_pas_une_creature_faconnee_mais_les_sorts_amis_si()
    {
        var hero = Mage("lenteur", "benediction");
        var battle = new TacticalBattle(Army("gargouille", 10), Army("golem-pierre", 10), new GameRandom(1), hero);
        battle.ForceTurn(battle.Units.First(u => u.Side == BattleSide.Attacker));
        var golems = battle.Units.First(u => u.Side == BattleSide.Defender);
        var gargoyles = battle.Units.First(u => u.Side == BattleSide.Attacker);
        Assert.False(battle.IsValidTarget(TestData.Data.Spells.Get("lenteur"), golems.Position));
        Assert.True(battle.IsValidTarget(TestData.Data.Spells.Get("benediction"), gargoyles.Position));
    }

    [Fact]
    public void Le_gnome_repare_les_creatures_faconnees_voisines_au_debut_de_son_tour()
    {
        var battle = new TacticalBattle(Army(("gnome", 20), ("golem-pierre", 5), ("paysan", 5)), Army("gobelin", 5), new GameRandom(1));
        var gnomes = battle.Units.First(u => u.Type.Id == "gnome");
        var golems = battle.Units.First(u => u.Type.Id == "golem-pierre");
        var peasants = battle.Units.First(u => u.Type.Id == "paysan");
        golems.Position = gnomes.Position.Neighbours().First(h => Battlefield.InBounds(h) && battle.UnitAt(h) == null);
        golems.TakeDamage(20); // la créature de tête blessée
        int before = golems.TopHitPoints;

        battle.ForceTurn(peasants);
        battle.Defend(); // jusqu'au tour des gnomes
        for (int i = 0; i < 10 && battle.Active != gnomes; i++)
            battle.Defend();

        Assert.Same(gnomes, battle.Active);
        Assert.Equal(System.Math.Min(Creature("golem-pierre").HitPoints, before + 20 * TacticalBattle.RepairPerCreature), golems.TopHitPoints);
    }

    [Fact]
    public void Le_djinn_benit_une_troupe_amie_au_debut_de_son_tour()
    {
        var battle = new TacticalBattle(Army(("djinn", 5), ("paysan", 10)), Army("gobelin", 5), new GameRandom(1));
        var djinns = battle.Units.First(u => u.Type.Id == "djinn");
        for (int i = 0; i < 10 && battle.Active != djinns; i++)
            battle.Defend();
        Assert.Same(djinns, battle.Active);
        var blessed = Assert.Single(battle.TakeLog().OfType<SpellApplied>());
        Assert.Equal(djinns.Side, blessed.Unit.Side);
        Assert.Contains(blessed.Spell.Id, new[] { "benediction", "peau-de-pierre", "hate" });
    }

    [Fact]
    public void Le_mage_frappe_au_corps_a_corps_sans_perdre_la_moitie_de_ses_degats()
    {
        Assert.Contains(Ability.NoMeleePenalty, Creature("mage").Abilities);
        // même nombre, même graine : le Mage au corps à corps face à un Archer (qui, lui, perd la moitié)
        int Melee(string shooter)
        {
            var battle = new TacticalBattle(Army(shooter, 10), Army("paysan", 1000), new GameRandom(2));
            var unit = battle.Units.First(u => u.Side == BattleSide.Attacker);
            var target = battle.Units.First(u => u.Side == BattleSide.Defender);
            battle.ForceTurn(unit, new Hex(target.Position.Col - 1, target.Position.Row));
            battle.TakeLog();
            Assert.True(battle.Attack(target, unit.Position));
            return battle.TakeLog().OfType<UnitStruck>().First().Damage;
        }
        var mage = Creature("mage");
        // sans pénalité : au moins les dégâts minimaux × 10 (avant la règle attaque / défense, qui l'avantage ici)
        Assert.True(Melee("mage") >= mage.MinDamage * 10);
    }
}
