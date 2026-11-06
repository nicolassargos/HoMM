using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Ce que fait un sort (étape 9).</summary>
public enum SpellEffect
{
    /// <summary>Des dégâts : une base plus tant par point de puissance.</summary>
    Damage,
    /// <summary>Dégâts en plus (en pourcentage).</summary>
    Bless,
    /// <summary>Dégâts en moins (en pourcentage).</summary>
    Curse,
    /// <summary>Défense en plus.</summary>
    StoneSkin,
    /// <summary>Vitesse en plus.</summary>
    Haste,
    /// <summary>Vitesse divisée par deux.</summary>
    Slow,
    /// <summary>Attaque en moins.</summary>
    Weakness,
    /// <summary>Soigne la créature de tête et retire les sorts néfastes.</summary>
    Cure,
    /// <summary>Ramène des créatures tuées dans ce combat.</summary>
    Resurrection,
    /// <summary>Attaque en plus.</summary>
    Bloodlust,
    /// <summary>Les tirs contre la troupe font moins de dégâts (en pourcentage).</summary>
    ArrowShield,
    /// <summary>La troupe ne joue plus jusqu'à ce qu'on la frappe (et ne riposte pas à ce coup).</summary>
    Blind,
    /// <summary>La troupe va sur n'importe quelle case libre.</summary>
    Teleport,
    /// <summary>Retire tous les sorts d'une troupe.</summary>
    Dispel,
}

/// <summary>
/// Sur qui se lance un sort : une troupe ennemie, une troupe amie, n'importe quelle troupe, une case et ses voisines,
/// ou une troupe ennemie puis, de proche en proche, les troupes les plus proches (une chaîne d'éclairs).
/// </summary>
public enum SpellTarget { Enemy, Ally, Area, Any, Chain }

/// <summary>Un sort de combat. Les valeurs viennent de data/spells.json.</summary>
public sealed record SpellType
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Le niveau du sort (1 à 4) : celui de la guilde des mages qui l'enseigne.</summary>
    public int Level { get; init; } = 1;

    /// <summary>Points de sort pour le lancer.</summary>
    public int Cost { get; init; }

    public SpellEffect Effect { get; init; }
    public SpellTarget Target { get; init; }

    /// <summary>La force du sort (dégâts, soins) : <see cref="Base"/> + <see cref="PerPower"/> × puissance du héros.</summary>
    public int Base { get; init; }
    public int PerPower { get; init; }

    /// <summary>Le bonus ou malus d'un sort qui dure (défense, vitesse, attaque).</summary>
    public int Amount { get; init; }

    /// <summary>Un sort qui dure quelques rounds (bénédiction, lenteur…), plutôt qu'un effet immédiat.</summary>
    public bool Lasts => Effect is not (SpellEffect.Damage or SpellEffect.Cure or SpellEffect.Resurrection
        or SpellEffect.Teleport or SpellEffect.Dispel);

    /// <summary>Un sort néfaste qui dure (retiré par les soins).</summary>
    public bool Harmful => Effect is SpellEffect.Curse or SpellEffect.Slow or SpellEffect.Weakness or SpellEffect.Blind;

    /// <summary>Combien de troupes touche au plus une chaîne d'éclairs (la force est divisée par deux à chaque saut).</summary>
    public const int ChainTargets = 4;
}

/// <summary>Tous les sorts du jeu.</summary>
public sealed class SpellCatalog
{
    private readonly Dictionary<string, SpellType> _byId;
    private readonly List<SpellType> _all;

    public SpellCatalog(IEnumerable<SpellType> spells)
    {
        _all = spells.ToList();
        _byId = _all.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
    }

    public static readonly SpellCatalog Empty = new(new SpellType[0]);

    /// <summary>Toutes les entrées du fichier, dans l'ordre (y compris les doublons, vérifiés par <see cref="DataValidation"/>).</summary>
    public IReadOnlyList<SpellType> All => _all;

    public SpellType Get(string id) =>
        _byId.TryGetValue(id, out var spell) ? spell : throw new KeyNotFoundException($"sort inconnu : {id}");

    public IEnumerable<SpellType> OfLevel(int level) => _all.Where(s => s.Level == level);

    private sealed record CatalogFile(List<SpellType> Spells);

    public static SpellCatalog FromJson(string json) => new(GameData.Parse<CatalogFile>(json, "sorts").Spells);
}

/// <summary>Un sort qui dure, posé sur une troupe : son bonus ou malus, jusqu'à la fin du round <paramref name="LastRound"/>.</summary>
public sealed record ActiveSpell(SpellType Spell, int Amount, int LastRound);

/// <summary>Un héros lance un sort sur cette case (pendant le tour d'une de ses troupes).</summary>
public sealed record SpellCast(Hero Caster, BattleSide Side, SpellType Spell, Hex Target) : BattleAction;

/// <summary>Un sort fait <paramref name="Damage"/> dégâts à une troupe et tue <paramref name="Killed"/> créatures.</summary>
public sealed record SpellDamaged(BattleUnit Target, SpellType Spell, int Damage, int Killed) : BattleAction;

/// <summary>Un sort rend <paramref name="HitPoints"/> points de vie à une troupe et ramène <paramref name="Revived"/> créatures.</summary>
public sealed record UnitHealed(BattleUnit Unit, SpellType Spell, int HitPoints, int Revived) : BattleAction;

/// <summary>Un sort qui dure est posé sur une troupe (bénédiction, lenteur…).</summary>
public sealed record SpellApplied(BattleUnit Unit, SpellType Spell) : BattleAction;

/// <summary>Une troupe aveuglée passe son tour.</summary>
public sealed record UnitSkipped(BattleUnit Unit) : BattleAction;

/// <summary>Une troupe est téléportée d'une case à une autre (sa tête, pour une grande créature).</summary>
public sealed record UnitTeleported(BattleUnit Unit, Hex From, Hex To) : BattleAction;

/// <summary>Les sorts d'une troupe sont dissipés.</summary>
public sealed record SpellsDispelled(BattleUnit Unit, SpellType Spell) : BattleAction;
