using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>Un héros qui se déplace sur la carte d'aventure avec son armée.</summary>
public sealed class Hero
{
    public const int DefaultMovement = 1500;
    public const int StartingCharisma = 10;

    public Hero(string name, Player owner, Cell position)
    {
        Name = name;
        Owner = owner;
        Position = position;
        Movement = MaxMovement;
        owner.Heroes.Add(this);
        owner.Fog.Reveal(position, VisionRadius);
    }

    /// <summary>Identifiant stable du héros dans la partie (donné par <see cref="GameState"/>).</summary>
    public int Id { get; internal set; }
    public string Name { get; }
    public Player Owner { get; }
    public Cell Position { get; internal set; }
    public Army Army { get; } = new();
    public int MaxMovement { get; set; } = DefaultMovement;
    public int Movement { get; internal set; }
    public int VisionRadius { get; set; } = 5;

    // Caractéristiques. Attaque et défense s'ajoutent à celles des créatures en combat.
    // (internal set : remises telles quelles au chargement d'une partie)
    public int Attack { get; internal set; } = 1;
    public int Defense { get; internal set; } = 1;
    public int Power { get; internal set; } = 1;
    public int Knowledge { get; internal set; } = 1;
    public int Charisma { get; internal set; } = StartingCharisma;

    // ------------------------------------------------------------------ magie (étape 9)

    /// <summary>Points de sort par point de savoir.</summary>
    public const int SpellPointsPerKnowledge = 10;

    /// <summary>Les points de sort au plus : 10 par point de savoir.</summary>
    public int MaxSpellPoints => SpellPointsPerKnowledge * Knowledge;

    /// <summary>Les points de sort qui restent (le héros commence plein, en regagne 1 par jour).</summary>
    public int SpellPoints { get; internal set; } = SpellPointsPerKnowledge;

    private readonly List<SpellType> _spells = new();

    /// <summary>Les sorts du livre du héros, par niveau puis dans l'ordre où il les a appris.</summary>
    public IReadOnlyList<SpellType> Spells => _spells;

    public bool Knows(SpellType spell) => _spells.Contains(spell);

    /// <summary>Le héros apprend un sort (renvoie false s'il le connaissait déjà).</summary>
    internal bool Learn(SpellType spell)
    {
        if (Knows(spell))
            return false;
        int at = _spells.FindIndex(s => s.Level > spell.Level);
        _spells.Insert(at < 0 ? _spells.Count : at, spell);
        return true;
    }

    /// <summary>Retire les points de sort dépensés au combat.</summary>
    internal void SpendSpellPoints(int points) => SpellPoints = System.Math.Max(0, SpellPoints - points);

    /// <summary>La classe du héros (étape 9) : d'où viennent ses caractéristiques et ses sorts de départ (null : aucune).</summary>
    public HeroClass? Class { get; internal set; }

    /// <summary>Le héros prend sa classe à sa création : ses caractéristiques de départ, ses sorts, tous ses points de sort.</summary>
    internal void TakeClass(HeroClass heroClass, SpellCatalog spells)
    {
        Class = heroClass;
        Attack = heroClass.Attack;
        Defense = heroClass.Defense;
        Power = heroClass.Power;
        Knowledge = heroClass.Knowledge;
        Charisma = heroClass.Charisma;
        foreach (var id in heroClass.Spells)
            Learn(spells.Get(id));
        SpellPoints = MaxSpellPoints;
    }

    /// <summary>Les artefacts que porte le héros (leurs bonus sont déjà comptés dans ses caractéristiques).</summary>
    public List<ArtifactType> Artifacts { get; } = new();

    /// <summary>Le héros prend un artefact : ses bonus s'ajoutent à ses caractéristiques, pour toujours.</summary>
    internal void Equip(ArtifactType artifact)
    {
        Artifacts.Add(artifact);
        Attack += artifact.Attack;
        Defense += artifact.Defense;
        Power += artifact.Power;
        Knowledge += artifact.Knowledge;
        Charisma += artifact.Charisma;
    }

    public int Level { get; internal set; } = 1;
    public int Experience { get; internal set; }

    /// <summary>Niveau et caractéristiques actuels (copie figée, pour comparer avant et après un niveau).</summary>
    public HeroStatistics Statistics => new(Level, Attack, Defense, Power, Knowledge, Charisma);

    /// <summary>Expérience totale nécessaire pour atteindre ce niveau.</summary>
    public static int ExperienceForLevel(int level) => 1000 * (level - 1) * level / 2;

    /// <summary>Un nouveau jour : tout le mouvement, et un point de sort de plus.</summary>
    public void NewDay()
    {
        Movement = MaxMovement;
        if (SpellPoints < MaxSpellPoints)
            SpellPoints++;
    }

    public bool CanStepTo(AdventureMap map, Cell next) =>
        Pathfinder.StepCost(map, Position, next) <= Movement;

    public void SpendMovement(int amount) => Movement = System.Math.Max(0, Movement - amount);

    public void StepTo(AdventureMap map, Cell next)
    {
        SpendMovement(Pathfinder.StepCost(map, Position, next));
        Position = next;
        Owner.Fog.Reveal(Position, VisionRadius);
    }

    public void AddExperience(int amount) => Experience += amount;

    /// <summary>Passe au niveau suivant : +1 charisme (automatique) et +1 à la caractéristique choisie.</summary>
    public void LevelUp(HeroStat stat)
    {
        Level++;
        Charisma++;
        switch (stat)
        {
            case HeroStat.Attack: Attack++; break;
            case HeroStat.Defense: Defense++; break;
            case HeroStat.Power: Power++; break;
            case HeroStat.Knowledge: Knowledge++; break;
        }
    }
}

/// <summary>Les caractéristiques gagnées au niveau supérieur (le charisme augmente à chaque niveau).</summary>
public enum HeroStat { Attack, Defense, Power, Knowledge }

/// <summary>Le niveau et les caractéristiques d'un héros à un moment donné.</summary>
public readonly record struct HeroStatistics(int Level, int Attack, int Defense, int Power, int Knowledge, int Charisma);

/// <summary>Un héros a avancé d'une case.</summary>
public sealed record HeroMoved(Hero Hero, Cell From, Cell To) : GameEvent;

/// <summary>Un héros a perdu un combat et disparaît.</summary>
public sealed record HeroDefeated(Hero Hero) : GameEvent;

/// <summary>Un héros a été recruté à la taverne d'une ville.</summary>
public sealed record HeroHired(Hero Hero, Town Town) : GameEvent;

/// <summary>Un héros peut passer au niveau <paramref name="Level"/> : le joueur choisit une des <paramref name="Options"/>.</summary>
public sealed record HeroLevelChoice(Hero Hero, int Level, IReadOnlyList<HeroStat> Options) : GameEvent;

/// <summary>Un héros passe au niveau supérieur et gagne une caractéristique. <paramref name="Before"/> et <paramref name="After"/> permettent d'afficher ce qui a changé.</summary>
public sealed record HeroLeveledUp(Hero Hero, HeroStat Stat, HeroStatistics Before, HeroStatistics After) : GameEvent;

/// <summary>Pourquoi un héros peut (ou ne peut pas) être recruté.</summary>
public enum HireResult { Ok, NotOwner, NoTavern, TooManyHeroes, NoHeroLeft, TownOccupied, NotEnoughGold }
