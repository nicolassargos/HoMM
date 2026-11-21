using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>
/// Le héros est passé par un portail : il ressort par <paramref name="Exit"/> (null : un héros y était déjà, il reste).
/// </summary>
public sealed record PortalCrossed(Hero Hero, Portal Portal, Cell? Exit) : ObjectVisited(Hero, Portal);

/// <summary>
/// Un portail, à terre, comme les monolithes de Heroes 3 : il mène à l'autre portail de sa paire (<see cref="Exit"/>), dans
/// les deux sens (choix de l'utilisateur, 2026-11-21 : une région où aucun chemin ne mène s'atteint par un portail, gardé
/// par une forte armée, et cache une grande récompense). Il reste sur la carte. Un lieu haut (sa case et celle du dessus) :
/// assez grand pour se voir de loin (remarque de l'utilisateur).
/// </summary>
public sealed class Portal : MapObject
{
    public Portal(Cell position, Cell exit) : base(position) => Exit = exit;

    /// <summary>Le portail où l'on ressort.</summary>
    public Cell Exit { get; }

    public override bool RemovedOnVisit => false;

    public override IEnumerable<Cell> Footprint => TallSite.FootprintAt(Position);

    /// <summary>Rien d'autre : le héros est déplacé par <see cref="GameState"/> (s'il n'y a personne à la sortie).</summary>
    public override ObjectVisited Visit(Hero hero) => new PortalCrossed(hero, this, Exit);
}
