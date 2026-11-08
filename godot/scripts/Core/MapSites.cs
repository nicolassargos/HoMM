using System.Collections.Generic;

namespace HoMM.Core;

// Les lieux de la carte qu'un héros visite et qui restent (choix de l'utilisateur, 2026-11-08, comme Heroes 2 et 3) :
// autels des caractéristiques, cabane du sage, moulin et roue à eau, tour d'observation.

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

/// <summary>Le héros est monté dans une tour d'observation : la carte se dévoile tout autour.</summary>
public sealed record TowerVisited(Hero Hero, ObservationTower Tower) : ObjectVisited(Hero, Tower);

/// <summary>Une tour d'observation : elle dévoile la carte à <see cref="Radius"/> cases autour d'elle.</summary>
public sealed class ObservationTower : MapObject
{
    public const int Radius = 20;

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
