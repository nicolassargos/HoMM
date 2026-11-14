using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Un bateau amarré sur l'eau (la mer, conçue avec l'utilisateur le 2026-11-14, comme Heroes 3). Un héros y monte en
/// entrant sur sa case depuis la terre : le bateau quitte la carte et le porte (un héros sur l'eau est en bateau) ; en
/// débarquant, il le laisse sur la case d'eau qu'il quitte. Monter à bord ou débarquer prend tout le mouvement qui reste.
/// Un bateau n'appartient à personne : le premier qui monte le prend.
/// </summary>
public sealed class Boat : MapObject
{
    public Boat(Cell position) : base(position)
    {
    }

    public override bool RemovedOnVisit => true;

    /// <summary>Monter à bord se fait par <see cref="GameState"/> (le héros part sur l'eau), pas par une visite.</summary>
    public override ObjectVisited Visit(Hero hero) =>
        throw new InvalidOperationException("On monte dans un bateau (GameState), on ne le visite pas.");
}

/// <summary>Le héros est monté dans le bateau de cette case : il est en mer, son mouvement du jour est fini.</summary>
public sealed record BoatBoarded(Hero Hero, Cell Position) : GameEvent;

/// <summary>Le héros a débarqué : son bateau reste amarré sur <paramref name="BoatPosition"/>.</summary>
public sealed record BoatLeft(Hero Hero, Cell BoatPosition) : GameEvent;

/// <summary>
/// Un chantier naval sur la côte (un lieu de la carte) : on y achète un bateau, qui apparaît sur l'eau à côté
/// (<see cref="BoatSpot"/>). Il reste sur la carte.
/// </summary>
public sealed class Shipyard : MapObject
{
    public Shipyard(Cell position, Cell boatSpot) : base(position) => BoatSpot = boatSpot;

    /// <summary>La case d'eau où le chantier met ses bateaux.</summary>
    public Cell BoatSpot { get; }

    public override bool RemovedOnVisit => false;

    public override ObjectVisited Visit(Hero hero) => new ShipyardVisited(hero, this);
}

/// <summary>Le héros passe au chantier naval de la côte : son joueur peut y acheter un bateau (<see cref="GameState.BuyBoat"/>).</summary>
public sealed record ShipyardVisited(Hero Hero, Shipyard Shipyard) : ObjectVisited(Hero, Shipyard);

/// <summary>Un bateau construit par un chantier naval (d'une ville ou de la côte), sur <paramref name="Position"/>.</summary>
public sealed record BoatBuilt(Player Player, Cell Position) : GameEvent;

/// <summary>Pourquoi un bateau peut (ou ne peut pas) se construire.</summary>
public enum BoatResult { Ok, NoShipyard, NotOwner, SpotTaken, NotEnoughResources }

/// <summary>Les règles des bateaux qui ne dépendent pas de la partie.</summary>
public static class Boats
{
    /// <summary>Le prix d'un bateau, comme Heroes 3.</summary>
    public static readonly IReadOnlyDictionary<ResourceType, int> Cost = new Dictionary<ResourceType, int>
    {
        [ResourceType.Gold] = 1000,
        [ResourceType.Wood] = 10,
    };

    /// <summary>
    /// La case d'eau où une ville met ses bateaux : une case d'eau qui touche ses murs ou la case devant sa porte, la plus
    /// proche de sa porte (null si la ville n'est pas au bord de l'eau).
    /// </summary>
    public static Cell? SpotFor(AdventureMap map, Town town)
    {
        var around = town.Footprint.Append(town.Approach).ToHashSet();
        return around.SelectMany(c => new[] { c.Offset(1, 0), c.Offset(-1, 0), c.Offset(0, 1), c.Offset(0, -1) })
            .Where(c => !around.Contains(c) && map.IsWater(c))
            .Distinct()
            .OrderBy(c => Math.Abs(c.X - town.Approach.X) + Math.Abs(c.Y - town.Approach.Y)).ThenBy(c => c.Y).ThenBy(c => c.X)
            .Cast<Cell?>().FirstOrDefault();
    }
}
