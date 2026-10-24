namespace HoMM.Core;

/// <summary>Un héros qui se déplace sur la carte d'aventure avec son armée.</summary>
public sealed class Hero
{
    public const int DefaultMovement = 1500;

    public Hero(string name, Player owner, Cell position)
    {
        Name = name;
        Owner = owner;
        Position = position;
        Movement = MaxMovement;
        owner.Heroes.Add(this);
        owner.Fog.Reveal(position, VisionRadius);
    }

    public string Name { get; }
    public Player Owner { get; }
    public Cell Position { get; private set; }
    public Army Army { get; } = new();
    public int MaxMovement { get; set; } = DefaultMovement;
    public int Movement { get; private set; }
    public int VisionRadius { get; set; } = 5;

    public void NewDay() => Movement = MaxMovement;

    public bool CanStepTo(AdventureMap map, Cell next) =>
        Pathfinder.StepCost(map, Position, next) <= Movement;

    public void SpendMovement(int amount) => Movement = System.Math.Max(0, Movement - amount);

    public void StepTo(AdventureMap map, Cell next)
    {
        SpendMovement(Pathfinder.StepCost(map, Position, next));
        Position = next;
        Owner.Fog.Reveal(Position, VisionRadius);
    }
}

/// <summary>Un héros a avancé d'une case.</summary>
public sealed record HeroMoved(Hero Hero, Cell From, Cell To) : GameEvent;

/// <summary>Un héros a perdu un combat et disparaît.</summary>
public sealed record HeroDefeated(Hero Hero) : GameEvent;
