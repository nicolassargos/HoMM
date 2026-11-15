using System.Collections.Generic;

namespace HoMM.Core;

// Les lieux de la carte qu'un héros visite et qui restent (choix de l'utilisateur, 2026-11-08, comme Heroes 2 et 3) :
// autels des caractéristiques, cabane du sage, moulin et roue à eau, tour d'observation.

/// <summary>
/// Les lieux hauts (cabane du sage, moulin, roue à eau, tour d'observation, temple ; choix de l'utilisateur,
/// 2026-11-12, comme Heroes 2 et 3) : le héros entre par leur porte (<see cref="MapObject.Position"/>) et la case
/// au-dessus bloque le passage. Leur image (64 pixels d'art de large) déborde d'une demi-case de chaque côté, sans
/// bloquer. Ceux des sauvegardes d'avant (version 6) gardent une seule case.
/// </summary>
public static class TallSite
{
    /// <summary>Les 2 cases d'un lieu haut dont la porte serait en <paramref name="entrance"/>.</summary>
    public static IEnumerable<Cell> FootprintAt(Cell entrance)
    {
        yield return entrance;
        yield return entrance.Offset(0, -1);
    }
}

/// <summary>Le héros a visité un autel. <paramref name="Gained"/> : faux s'il l'avait déjà visité.</summary>
public sealed record ShrineVisited(Hero Hero, Shrine Shrine, bool Gained) : ObjectVisited(Hero, Shrine);

/// <summary>Un autel : +1 à une caractéristique (attaque, défense, puissance ou savoir), une fois par héros.</summary>
public sealed class Shrine : MapObject
{
    public Shrine(Cell position, HeroStat stat) : base(position) => Stat = stat;

    public HeroStat Stat { get; }

    /// <summary>Les héros qui l'ont déjà visité (leur identifiant).</summary>
    public HashSet<int> Visitors { get; } = new();

    public override bool RemovedOnVisit => false;

    public override ObjectVisited Visit(Hero hero)
    {
        if (!Visitors.Add(hero.Id))
            return new ShrineVisited(hero, this, false);
        switch (Stat)
        {
            case HeroStat.Attack: hero.Attack++; break;
            case HeroStat.Defense: hero.Defense++; break;
            case HeroStat.Power: hero.Power++; break;
            case HeroStat.Knowledge: hero.Knowledge++; break;
        }
        return new ShrineVisited(hero, this, true);
    }
}

/// <summary>Le héros a visité la cabane du sage. <paramref name="Learned"/> : faux s'il connaissait déjà le sort.</summary>
public sealed record SageVisited(Hero Hero, SageHut Hut, bool Learned) : ObjectVisited(Hero, Hut);

/// <summary>La cabane du sage : il enseigne un sort (du niveau 1 ou 2, tiré à la création de la carte) à chaque héros.</summary>
public sealed class SageHut : MapObject
{
    public SageHut(Cell position, SpellType spell) : base(position) => Spell = spell;

    public SpellType Spell { get; }

    /// <summary>Sur 2 cases (la porte et la case au-dessus), sinon une seule (sauvegardes d'avant).</summary>
    public bool Large { get; init; } = true;

    public override IEnumerable<Cell> Footprint => Large ? TallSite.FootprintAt(Position) : base.Footprint;

    public override bool RemovedOnVisit => false;

    public override ObjectVisited Visit(Hero hero) => new SageVisited(hero, this, hero.Learn(Spell));
}

/// <summary>
/// Le héros a visité un moulin ou une roue à eau : il a pris <paramref name="Amount"/> (0 : déjà pris cette semaine).
/// </summary>
public sealed record ProducerVisited(Hero Hero, WeeklyProducer Producer, int Amount) : ObjectVisited(Hero, Producer);

/// <summary>
/// Un lieu qui produit chaque semaine : la roue à eau (500 or) ou le moulin (2 d'une ressource rare). Le premier héros
/// qui passe prend tout ; le lieu se remplit au début de chaque semaine.
/// </summary>
public sealed class WeeklyProducer : MapObject
{
    public WeeklyProducer(Cell position, ResourceType resource) : base(position) => Resource = resource;

    public ResourceType Resource { get; }

    /// <summary>Sur 2 cases (la porte et la case au-dessus), sinon une seule (sauvegardes d'avant).</summary>
    public bool Large { get; init; } = true;

    public override IEnumerable<Cell> Footprint => Large ? TallSite.FootprintAt(Position) : base.Footprint;

    /// <summary>Une roue à eau (de l'or), sinon un moulin.</summary>
    public bool IsWaterWheel => Resource == ResourceType.Gold;

    /// <summary>Ce qu'il donne chaque semaine : 500 or, ou 2 de sa ressource.</summary>
    public int WeeklyAmount => IsWaterWheel ? 500 : 2;

    /// <summary>Plein (au début de la partie et de chaque semaine), ou déjà pris cette semaine.</summary>
    public bool Stocked { get; internal set; } = true;

    public override bool RemovedOnVisit => false;

    public override ObjectVisited Visit(Hero hero)
    {
        if (!Stocked)
            return new ProducerVisited(hero, this, 0);
        Stocked = false;
        hero.Owner.Resources[Resource] = hero.Owner.Resources.GetValueOrDefault(Resource) + WeeklyAmount;
        return new ProducerVisited(hero, this, WeeklyAmount);
    }
}

/// <summary>Ce que donne un lieu de bénédiction : du moral (temple) ou de la chance (cercle des fées).</summary>
public enum Blessing { Morale, Luck }

/// <summary>
/// Le héros a visité un temple ou un cercle des fées. <paramref name="Gained"/> : faux s'il avait déjà ce bonus.
/// </summary>
public sealed record BlessingVisited(Hero Hero, BlessingSite Site, bool Gained) : ObjectVisited(Hero, Site);

/// <summary>
/// Un temple (+1 moral) ou un cercle des fées (+1 chance) : le bonus dure jusqu'au prochain combat du héros. On peut y
/// revenir après un combat. Sur l'eau (<see cref="Sea"/>, 2026-11-14) : une bouée (moral) ou des sirènes (chance).
/// </summary>
public sealed class BlessingSite : MapObject
{
    public const int Bonus = 1;

    public BlessingSite(Cell position, Blessing blessing) : base(position) => Blessing = blessing;

    public Blessing Blessing { get; }

    /// <summary>Un temple sur 2 cases (la porte et la case au-dessus), sinon une seule (sauvegardes d'avant).</summary>
    public bool Large { get; init; } = true;

    /// <summary>Sur l'eau : une bouée (moral) ou des sirènes (chance), sur une case.</summary>
    public bool Sea { get; init; }

    /// <summary>Le temple est haut ; le cercle des fées, plat, reste sur sa case (le héros se tient dans le cercle).</summary>
    public override IEnumerable<Cell> Footprint =>
        Large && !Sea && Blessing == Blessing.Morale ? TallSite.FootprintAt(Position) : base.Footprint;

    public override bool RemovedOnVisit => false;

    /// <summary>Le héros a-t-il déjà ce bonus (une nouvelle visite ne donnerait rien) ?</summary>
    public bool AlreadyGiven(Hero hero) => (Blessing == Blessing.Morale ? hero.SiteMorale : hero.SiteLuck) >= Bonus;

    public override ObjectVisited Visit(Hero hero)
    {
        if (AlreadyGiven(hero))
            return new BlessingVisited(hero, this, false);
        if (Blessing == Blessing.Morale)
            hero.SiteMorale = Bonus;
        else
            hero.SiteLuck = Bonus;
        return new BlessingVisited(hero, this, true);
    }
}

/// <summary>Le héros est monté dans une tour d'observation : la carte se dévoile tout autour.</summary>
public sealed record TowerVisited(Hero Hero, ObservationTower Tower) : ObjectVisited(Hero, Tower);

/// <summary>Une tour d'observation : elle dévoile la carte à <see cref="Radius"/> cases autour d'elle.</summary>
public sealed class ObservationTower : MapObject
{
    public const int Radius = 20;

    /// <summary>Sur 2 cases (la porte et la case au-dessus), sinon une seule (sauvegardes d'avant).</summary>
    public bool Large { get; init; } = true;

    public override IEnumerable<Cell> Footprint => Large ? TallSite.FootprintAt(Position) : base.Footprint;

    public ObservationTower(Cell position) : base(position)
    {
    }

    /// <summary>Les joueurs qui y sont déjà montés (leur identifiant).</summary>
    public HashSet<int> Visitors { get; } = new();

    public override bool RemovedOnVisit => false;

    public override ObjectVisited Visit(Hero hero)
    {
        Visitors.Add(hero.Owner.Id);
        hero.Owner.Fog.Reveal(Position, Radius);
        return new TowerVisited(hero, this);
    }
}
