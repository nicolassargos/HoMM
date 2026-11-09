using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Les talents des classes, qui grandissent avec le niveau du héros (décidés avec l'utilisateur, 2026-11-09) : Croisé
/// (Paladin), Chef de guerre (Barbare), Chasseur (Rôdeur) ; la Nécromancie a ses propres tests.
/// </summary>
public class TalentsTests
{
    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    /// <summary>Un héros de cette classe, à ce niveau.</summary>
    private static Hero Hero(string classId, int level)
    {
        var state = new GameState(5, TestData.Data, MapSize.Small, 0);
        var hero = state.CurrentHero!;
        hero.Class = TestData.Data.Classes.Get(classId);
        hero.Level = level;
        return hero;
    }

    private static Army Army(string id, int count)
    {
        var army = new Army();
        army.Add(Creature(id), count);
        return army;
    }

    /// <summary>Les dégâts d'une volée des archers du héros sur cette cible (même graine : seul le héros change).</summary>
    private static int Volley(Hero hero, string shooter, string target)
    {
        var battle = new TacticalBattle(Army(shooter, 10), Army(target, 500), new GameRandom(3), hero);
        var archers = battle.Units.First(u => u.Side == BattleSide.Attacker);
        battle.ForceTurn(archers);
        battle.TakeLog();
        Assert.True(battle.Shoot(battle.Units.First(u => u.Side == BattleSide.Defender)));
        return battle.TakeLog().OfType<UnitStruck>().First().Damage;
    }

    [Fact]
    public void Chaque_classe_a_son_talent()
    {
        Assert.Equal(Talents.Crusader, TestData.Data.Classes.Get("paladin").Talent);
        Assert.Equal(Talents.Necromancy, TestData.Data.Classes.Get("necromancien").Talent);
        Assert.Equal(Talents.Warlord, TestData.Data.Classes.Get("barbare").Talent);
        Assert.Equal(Talents.Hunter, TestData.Data.Classes.Get("rodeur").Talent);
    }

    [Fact]
    public void Le_croise_frappe_plus_fort_les_morts_vivants_selon_son_niveau()
    {
        Assert.Equal(0.2, Talents.DamageBonus(Hero("paladin", 10), ranged: false, targetUndead: true), 6);
        Assert.Equal(0, Talents.DamageBonus(Hero("paladin", 10), ranged: false, targetUndead: false));

        // la même volée d'archers sur des squelettes : +20 % avec un Paladin du niveau 10
        int plain = Volley(Hero("barbare", 10), "archer", "squelette");
        int crusader = Volley(Hero("paladin", 10), "archer", "squelette");
        Assert.InRange(crusader, (int)(plain * 1.2) - 1, (int)(plain * 1.2) + 1);
        // contre des vivants, rien
        Assert.Equal(Volley(Hero("barbare", 10), "archer", "gobelin"), Volley(Hero("paladin", 10), "archer", "gobelin"));
    }

    [Fact]
    public void Le_chasseur_rend_ses_tireurs_plus_forts_selon_son_niveau()
    {
        Assert.Equal(0.15, Talents.DamageBonus(Hero("rodeur", 15), ranged: true, targetUndead: false), 6);
        Assert.Equal(0, Talents.DamageBonus(Hero("rodeur", 15), ranged: false, targetUndead: false));

        int plain = Volley(Hero("barbare", 20), "archer", "gobelin");
        int hunter = Volley(Hero("rodeur", 20), "archer", "gobelin");
        Assert.InRange(hunter, (int)(plain * 1.2) - 1, (int)(plain * 1.2) + 1);
    }

    [Fact]
    public void Le_chef_de_guerre_pousse_la_rage_plus_haut_tous_les_trois_niveaux()
    {
        Assert.Equal(TacticalBattle.RageMax, Talents.RageMax(Hero("barbare", 2), TacticalBattle.RageMax));
        Assert.Equal(TacticalBattle.RageMax + 1, Talents.RageMax(Hero("barbare", 3), TacticalBattle.RageMax));
        Assert.Equal(TacticalBattle.RageMax + 3, Talents.RageMax(Hero("barbare", 9), TacticalBattle.RageMax));
        Assert.Equal(TacticalBattle.RageMax, Talents.RageMax(Hero("paladin", 9), TacticalBattle.RageMax));
        Assert.Equal(TacticalBattle.RageMax, Talents.RageMax(null, TacticalBattle.RageMax));
    }

    [Fact]
    public void Au_combat_la_rage_d_un_barbare_de_haut_niveau_monte_plus_haut()
    {
        // des Gobelins (Rage) d'un Barbare du niveau 9 qui se défend, criblés de flèches encore et encore
        var battle = new TacticalBattle(Army("archer", 5), Army("gobelin", 2000), new GameRandom(1), defenderHero: Hero("barbare", 9));
        var archers = battle.Units.First(u => u.Side == BattleSide.Attacker);
        var goblins = battle.Units.First(u => u.Side == BattleSide.Defender);
        for (int i = 0; i < 10; i++)
        {
            battle.ForceTurn(archers);
            Assert.True(battle.Shoot(goblins));
        }
        Assert.Equal(TacticalBattle.RageMax + 3, goblins.Rage);
    }}
