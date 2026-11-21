using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Recherche du chemin le moins coûteux (A*) en tenant compte du terrain.</summary>
public static class Pathfinder
{
    private static readonly (int Dx, int Dy)[] Directions =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1),
    };

    /// <summary>
    /// Coût ajouté (pour la recherche seulement) quand le chemin entre dans la zone d'un monstre :
    /// le chemin contourne les monstres quand c'est possible, et ne les traverse que sinon.
    /// </summary>
    public const int GuardPenalty = 100_000;

    /// <summary>
    /// Coût ajouté (pour la recherche seulement) pour monter à bord ou débarquer : cela prend tout le mouvement qui reste,
    /// comme une journée de marche.
    /// </summary>
    public const int BoardingPenalty = Hero.DefaultMovement;

    /// <summary>Ce pas fait monter à bord d'un bateau ou débarquer (il passe de la terre à l'eau, ou l'inverse).</summary>
    public static bool Boards(AdventureMap map, Cell from, Cell to) => map.IsWater(from) != map.IsWater(to);

    /// <summary>
    /// Peut-on aller de <paramref name="from"/> à sa voisine <paramref name="to"/> ? À terre, sur une case où l'on marche,
    /// ou sur l'eau seulement pour monter dans un bateau ; en mer, sur l'eau (pas dans un autre bateau), ou débarquer sur
    /// une case de terre libre.
    /// </summary>
    public static bool CanEnter(AdventureMap map, Cell from, Cell to)
    {
        if (!map.IsWater(from))
            return map.IsWater(to) ? map.ObjectAt(to) is Boat : map.IsPassable(to);
        return map.IsWater(to) ? map.ObjectAt(to) is not Boat : map.IsPassable(to) && map.ObjectAt(to) == null;
    }

    /// <summary>Un pas en diagonale ne « coupe » pas un coin : à terre, deux coins où l'on marche ; en mer, deux coins d'eau.</summary>
    public static bool CornersOpen(AdventureMap map, Cell from, Cell to)
    {
        var corners = new[] { new Cell(to.X, from.Y), new Cell(from.X, to.Y) };
        bool fromWater = map.IsWater(from), toWater = map.IsWater(to);
        if (fromWater && toWater)
            return corners.All(map.IsWater);
        if (!fromWater && !toWater)
            return corners.All(map.IsPassable);
        return corners.All(c => map.IsPassable(c) || map.IsWater(c));
    }

    private static readonly int MinStepCost = TerrainInfo.RoadCost;
    private static readonly int MinDiagonalCost = (int)(MinStepCost * Math.Sqrt(2));

    /// <summary>
    /// Coût pour aller d'une case à une case voisine. Comme dans Heroes, c'est le terrain
    /// de départ qui compte, et un pas en diagonale coûte environ 1,41 fois plus.
    /// </summary>
    /// <paramref name="roughReduction"/> : la part (en pourcentage) du surcoût des terrains difficiles (plus de 100) que
    /// le héros ne paie pas (Orientation). <paramref name="ownGrounds"/> : les sols de sa faction, où il ne paie aucun surcoût.
    public static int StepCost(AdventureMap map, Cell from, Cell to, int roughReduction = 0, IReadOnlyList<TerrainType>? ownGrounds = null)
    {
        if (Teleports(map, from, to))
            return 0;
        int cost = CellCost(map, from, roughReduction, ownGrounds);
        bool diagonal = from.X != to.X && from.Y != to.Y;
        return diagonal ? (int)(cost * Math.Sqrt(2)) : cost;
    }

    /// <summary>Ce « pas » est le saut d'un portail à l'autre (sans coût : on a payé le pas qui entre dans le portail).</summary>
    public static bool Teleports(AdventureMap map, Cell from, Cell to) => map.ObjectAt(from) is Portal portal && portal.Exit == to;

    /// <summary>Le coût pour quitter une case, pour un héros (voir <see cref="StepCost"/>).</summary>
    public static int CellCost(AdventureMap map, Cell cell, int roughReduction = 0, IReadOnlyList<TerrainType>? ownGrounds = null)
    {
        int cost = map.MoveCost(cell);
        int grass = TerrainInfo.MoveCost(TerrainType.Grass);
        if (cost <= grass)
            return cost;
        return ownGrounds?.Contains(map.GetTerrain(cell)) == true ? grass : grass + (cost - grass) * (100 - roughReduction) / 100;
    }

    /// <summary>
    /// Renvoie les cases à parcourir, de la première après <paramref name="start"/> jusqu'à
    /// <paramref name="goal"/> incluse, ou null s'il n'y a pas de chemin.
    /// Un objet bloque le passage : on peut seulement s'arrêter dessus (un bateau : on y monte, et le chemin continue en
    /// mer ; un portail : on ressort par l'autre, et le chemin continue de là). Le chemin évite si possible les cases
    /// gardées par un monstre (voir <see cref="GuardPenalty"/>).
    /// <paramref name="blocked"/> : cases interdites en plus (les autres héros).
    /// </summary>
    public static List<Cell>? FindPath(AdventureMap map, Cell start, Cell goal, IReadOnlySet<Cell>? blocked = null, int roughReduction = 0,
        IReadOnlyList<TerrainType>? ownGrounds = null)
    {
        if (start == goal || !(map.IsPassable(goal) || map.IsWater(goal)) || blocked?.Contains(goal) == true)
            return null;

        // Distance « octile » au coût de la route, le terrain le moins cher.
        // Le pas en diagonale utilise le même arrondi que StepCost, pour ne jamais
        // surestimer le coût restant (sinon A* peut rater le meilleur chemin).
        static double Octile(Cell a, Cell b)
        {
            int dx = Math.Abs(a.X - b.X), dy = Math.Abs(a.Y - b.Y);
            int diagonals = Math.Min(dx, dy);
            int straights = Math.Max(dx, dy) - diagonals;
            return straights * MinStepCost + diagonals * MinDiagonalCost;
        }
        // par un portail, le reste peut être plus court : l'estimation prend le meilleur des deux
        var portals = map.Objects.OfType<Portal>().ToList();
        double Estimate(Cell c)
        {
            double best = Octile(c, goal);
            foreach (var portal in portals)
                best = Math.Min(best, Octile(c, portal.Position) + Octile(portal.Exit, goal));
            return best;
        }

        var open = new PriorityQueue<(Cell Cell, int Cost), double>();
        var bestCost = new Dictionary<Cell, int> { [start] = 0 };
        var cameFrom = new Dictionary<Cell, Cell>();
        open.Enqueue((start, 0), Estimate(start));

        while (open.TryDequeue(out var current, out _))
        {
            var (cell, cost) = current;
            if (cell == goal)
                return Rebuild(cameFrom, start, goal);
            if (cost > bestCost[cell])
                continue;
            // arrivé par un portail : on repart de sa sortie
            bool arrived = cell != start && Teleports(map, cameFrom[cell], cell);
            // un portail : on ressort par l'autre (sans coût), s'il n'y a personne
            if (cell != start && !arrived && map.ObjectAt(cell) is Portal { Exit: var exit })
            {
                if (exit != start && blocked?.Contains(exit) != true && cost < bestCost.GetValueOrDefault(exit, int.MaxValue))
                {
                    bestCost[exit] = cost;
                    cameFrom[exit] = cell;
                    open.Enqueue((exit, cost), cost + Estimate(exit));
                }
                continue;
            }
            if (cell != start && !arrived && map.ObjectAt(cell) is not (null or Boat))
                continue;

            foreach (var (dx, dy) in Directions)
            {
                var next = cell.Offset(dx, dy);
                if (!map.InBounds(next) || !CanEnter(map, cell, next) || blocked?.Contains(next) == true)
                    continue;
                // Pas de diagonale qui « coupe » un coin infranchissable
                if (dx != 0 && dy != 0 && !CornersOpen(map, cell, next))
                    continue;
                int newCost = cost + StepCost(map, cell, next, roughReduction, ownGrounds) + (Boards(map, cell, next) ? BoardingPenalty : 0)
                    + (map.IsGuarded(next) ? GuardPenalty : 0);
                if (newCost < bestCost.GetValueOrDefault(next, int.MaxValue))
                {
                    bestCost[next] = newCost;
                    cameFrom[next] = cell;
                    open.Enqueue((next, newCost), newCost + Estimate(next));
                }
            }
        }
        return null;
    }

    private static List<Cell> Rebuild(Dictionary<Cell, Cell> cameFrom, Cell start, Cell goal)
    {
        var path = new List<Cell>();
        for (var c = goal; c != start; c = cameFrom[c])
            path.Add(c);
        path.Reverse();
        return path;
    }
}
