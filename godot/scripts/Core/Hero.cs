namespace HoMM.Core;

/// <summary>Un héros qui se déplace sur la carte d'aventure.</summary>
public sealed class Hero
{
    public const int DefaultMovement = 1500;

    public Hero(string name, Player owner, Cell position)
    {
        Name = name;
        Owner = owner;
        Position = position;
        Movement = MaxMovement;
    }

    public string Name { get; }
    public Player Owner { get; }
    public Cell Position { get; private set; }
    public int MaxMovement { get; set; } = DefaultMovement;
    public int Movement { get; private set; }
    public int VisionRadius { get; set; } = 5;

    public void NewDay() => Movement = MaxMovement;

    public bool CanStepTo(AdventureMap map, Cell next) =>
        Pathfinder.StepCost(map, Position, next) <= Movement;

    public void StepTo(AdventureMap map, Cell next)
    {
        Movement -= Pathfinder.StepCost(map, Position, next);
        Position = next;
        map.Reveal(Position, VisionRadius);
    }
}
