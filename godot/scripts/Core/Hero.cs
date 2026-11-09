using System.Collections.Generic;
using System.Linq;

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
    /// <summary>Le mouvement de chaque jour, sans compétence (celui de la sauvegarde).</summary>
    public int BaseMovement { get; set; } = DefaultMovement;

    /// <summary>Le mouvement de chaque jour : la Logistique en ajoute.</summary>
    public int MaxMovement => BaseMovement * (100 + SkillValue(HoMM.Core.Skills.Logistics)) / 100;

    public int Movement { get; internal set; }

    /// <summary>La vue, sans compétence (celle de la sauvegarde).</summary>
    public int BaseVisionRadius { get; set; } = 5;

    /// <summary>Jusqu'où le héros voit : l'Éclaireur voit plus loin.</summary>
    public int VisionRadius => BaseVisionRadius + SkillValue(HoMM.Core.Skills.Scouting);

    // ------------------------------------------------------------------ compétences (étape 9)

    private readonly Dictionary<SkillType, int> _skills = new();

    /// <summary>Les compétences du héros et leur rang (1 à 3), dans l'ordre où il les a apprises.</summary>
    public IReadOnlyDictionary<SkillType, int> Skills => _skills;

    /// <summary>Les points de compétence pas encore dépensés (un par niveau).</summary>
    public int SkillPoints { get; internal set; }

    /// <summary>Le rang du héros dans cette compétence (0 : il ne l'a pas).</summary>
    public int SkillRank(string id) => _skills.FirstOrDefault(p => p.Key.Id == id).Value;

    /// <summary>L'effet de cette compétence à son rang (0 sans elle).</summary>
    public int SkillValue(string id) => _skills.FirstOrDefault(p => p.Key.Id == id) is { Key: { } skill, Value: var rank } ? skill.ValueAt(rank) : 0;

    /// <summary>Les points dépensés dans un arbre (la somme des rangs) : ils ouvrent ses rangées.</summary>
    public int PointsIn(SkillTree tree) => _skills.Where(p => p.Key.Tree == tree).Sum(p => p.Value);

    /// <summary>Le héros peut-il apprendre cette compétence, ou un rang de plus ?</summary>
    public LearnSkillResult CanLearn(SkillType skill)
    {
        int rank = _skills.GetValueOrDefault(skill);
        if (rank >= HoMM.Core.Skills.MaxRank)
            return LearnSkillResult.MaxRank;
        if (rank == 0 && _skills.Count >= HoMM.Core.Skills.MaxSkills)
            return LearnSkillResult.TooManySkills;
        if (PointsIn(skill.Tree) < HoMM.Core.Skills.PointsToOpenRow(skill.Row))
            return LearnSkillResult.RowLocked;
        if (SkillPoints <= 0)
            return LearnSkillResult.NoPoint;
        return LearnSkillResult.Ok;
    }

    /// <summary>Un rang de plus dans cette compétence (sans compter de point : la classe, le chargement d'une partie).</summary>
    internal int AddSkillRank(SkillType skill, int ranks = 1)
    {
        int rank = System.Math.Min(HoMM.Core.Skills.MaxRank, _skills.GetValueOrDefault(skill) + ranks);
        _skills[skill] = rank;
        return rank;
    }

    /// <summary>
    /// Les points de commandement par troupe : 10 par point de charisme (<see cref="Command.Limit"/>), plus ce
    /// qu'ajoute le Commandement.
    /// </summary>
    public int CommandLimit => Command.Limit(Charisma) * (100 + SkillValue(HoMM.Core.Skills.Leadership)) / 100;

    public bool IsOverLimit(CreatureType type, int count) => Command.Cost(type, count) > CommandLimit;

    /// <summary>La chance qu'une troupe trop nombreuse se révolte : la Discipline la réduit.</summary>
    public double RevoltChance(CreatureType type, int count) =>
        Command.RevoltChanceFor(type, count, CommandLimit) * (100 - SkillValue(HoMM.Core.Skills.Discipline)) / 100.0;

    /// <summary>Le rang du héros dans l'école de magie de ce sort (0 sans elle).</summary>
    private (SkillType? Skill, int Rank) SchoolOf(SpellType spell) =>
        spell.School is { } school && _skills.FirstOrDefault(p => p.Key.School == school) is { Key: { } skill, Value: var rank }
            ? (skill, rank) : (null, 0);

    /// <summary>Ce que coûte un sort à ce héros : son école le rend moins cher (au moins 1 point).</summary>
    public int SpellCost(SpellType spell) =>
        SchoolOf(spell) is { Skill: { } skill, Rank: var rank }
            ? System.Math.Max(1, (int)System.Math.Round(spell.Cost * (100 - skill.ValueAt(rank)) / 100.0)) : spell.Cost;

    /// <summary>La force en plus (en pourcentage) des sorts de son école.</summary>
    public int SpellBonus(SpellType spell) => SchoolOf(spell) is { Skill: { } skill, Rank: var rank } ? skill.Value2At(rank) : 0;

    // Caractéristiques. Attaque et défense s'ajoutent à celles des créatures en combat.
    // (internal set : remises telles quelles au chargement d'une partie)
    public int Attack { get; internal set; } = 1;
    public int Defense { get; internal set; } = 1;
    public int Power { get; internal set; } = 1;
    public int Knowledge { get; internal set; } = 1;
    public int Charisma { get; internal set; } = StartingCharisma;

    /// <summary>Le moral et la chance donnés par un lieu de la carte (temple, cercle des fées), jusqu'au prochain combat.</summary>
    public int SiteMorale { get; internal set; }
    public int SiteLuck { get; internal set; }

    /// <summary>Après un combat : les bonus des lieux sont épuisés.</summary>
    internal void ClearSiteBonuses() => SiteMorale = SiteLuck = 0;

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

    /// <summary>
    /// Le héros prend sa classe à sa création : ses caractéristiques de départ, ses sorts, sa compétence de départ (au
    /// rang de base), tous ses points de sort.
    /// </summary>
    internal void TakeClass(HeroClass heroClass, SpellCatalog spells, SkillCatalog? skills = null)
    {
        Class = heroClass;
        Attack = heroClass.Attack;
        Defense = heroClass.Defense;
        Power = heroClass.Power;
        Knowledge = heroClass.Knowledge;
        Charisma = heroClass.Charisma;
        foreach (var id in heroClass.Spells)
            Learn(spells.Get(id));
        if (heroClass.Skill is { } skill && skills?.Find(skill) is { } type && !_skills.ContainsKey(type))
            AddSkillRank(type);
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

    /// <summary>
    /// L'expérience du niveau 2 ; le niveau n demande <see cref="ExperienceStep"/> × (n − 1) × n / 2. 700 depuis que les
    /// anciens monstres neutres (Gobelin, Loup, Orc, Ogre) coûtent le prix du Château (1000 avant) : la simulation gagne
    /// au niveau 21 environ, comme avant les Barbares (niveau 20), au lieu de 17 ou 18 avec 1000.
    /// </summary>
    public const int ExperienceStep = 700;

    /// <summary>Expérience totale nécessaire pour atteindre ce niveau.</summary>
    public static int ExperienceForLevel(int level) => ExperienceStep * (level - 1) * level / 2;

    /// <summary>Un nouveau jour : tout le mouvement, et un point de sort de plus (plus avec le Mysticisme).</summary>
    public void NewDay()
    {
        Movement = MaxMovement;
        if (SpellPoints < MaxSpellPoints)
            SpellPoints = System.Math.Min(MaxSpellPoints, SpellPoints + 1 + SkillValue(HoMM.Core.Skills.Mysticism));
    }

    public bool CanStepTo(AdventureMap map, Cell next) =>
        Pathfinder.StepCost(map, Position, next, RoughTerrainReduction) <= Movement;

    /// <summary>L'Orientation : le surcoût des terrains difficiles baisse d'autant (en pourcentage).</summary>
    public int RoughTerrainReduction => SkillValue(HoMM.Core.Skills.Pathfinding);

    public void SpendMovement(int amount) => Movement = System.Math.Max(0, Movement - amount);

    public void StepTo(AdventureMap map, Cell next)
    {
        SpendMovement(Pathfinder.StepCost(map, Position, next, RoughTerrainReduction));
        Position = next;
        Owner.Fog.Reveal(Position, VisionRadius);
    }

    public void AddExperience(int amount) => Experience += amount;

    /// <summary>Passe au niveau suivant : +1 charisme (automatique), +1 à la caractéristique choisie, un point de compétence.</summary>
    public void LevelUp(HeroStat stat)
    {
        Level++;
        Charisma++;
        SkillPoints++;
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
