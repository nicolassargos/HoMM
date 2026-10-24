using System;
using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>Recherche du chemin le moins coûteux (A*) en tenant compte du terrain.</summary>
public static class Pathfinder
{
    private static readonly (int Dx, int Dy)[] Directions =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1),
    };

    private static readonly int MinStepCost = TerrainInfo.MoveCost(TerrainType.Road);
    private static readonly int MinDiagonalCost = (int)(MinStepCost * Math.Sqrt(2));

    /// <summary>
    /// Coût pour aller d'une case à une case voisine. Comme dans Heroes, c'est le terrain
    /// de départ qui compte, et un pas en diagonale coûte environ 1,41 fois plus.
    /// </summary>
    public static int StepCost(AdventureMap map, Cell from, Cell to)
    {
        int cost = map.MoveCost(from);
        bool diagonal = from.X != to.X && from.Y != to.Y;
        return diagonal ? (int)(cost * Math.Sqrt(2)) : cost;
    }

    /// <summary>
    /// Renvoie les cases à parcourir, de la première après <paramref name="start"/> jusqu'à
    /// <paramref name="goal"/> incluse, ou null s'il n'y a pas de chemin.
    /// Un objet bloque le passage : on peut seulement s'arrêter dessus.
    /// </summary>
    public static List<Cell>? FindPath(AdventureMap map, Cell start, Cell goal)
    {
        if (start == goal || !map.IsPassable(goal))
            return null;

        double Estimate(Cell c)
        {
            int dx = Math.Abs(c.X - goal.X), dy = Math.Abs(c.Y - goal.Y);
            // Distance « octile » au coût de la route, le terrain le moins cher.
            // Le pas en diagonale utilise le même arrondi que StepCost, pour ne jamais
            // surestimer le coût restant (sinon A* peut rater le meilleur chemin).
            int diagonals = Math.Min(dx, dy);
            int straights = Math.Max(dx, dy) - diagonals;
            return straights * MinStepCost + diagonals * MinDiagonalCost;
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
            if (cell != start && map.ObjectAt(cell) != null)
                continue;

            foreach (var (dx, dy) in Directions)
            {
                var next = cell.Offset(dx, dy);
                if (!map.IsPassable(next))
                    continue;
                // Pas de diagonale qui « coupe » un coin infranchissable
                if (dx != 0 && dy != 0 && !(map.IsPassable(cell.Offset(dx, 0)) && map.IsPassable(cell.Offset(0, dy))))
                    continue;
                int newCost = cost + StepCost(map, cell, next);
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
