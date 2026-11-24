using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>Un point du tracé d'un ruisseau, en cases (le milieu de la case (2, 3) est (2,5 ; 3,5)).</summary>
public readonly record struct CoursePoint(double X, double Y);

/// <summary>D'où vient un ruisseau.</summary>
public enum StreamSource
{
    /// <summary>D'une source au milieu des prés : une petite mare entourée de pierres.</summary>
    Spring = 0,

    /// <summary>Du pied d'une montagne : la pluie et la neige fondue descendent des pentes.</summary>
    Mountain = 1,

    /// <summary>Du bord d'un marais, qui déborde.</summary>
    Swamp = 2,
}

/// <summary>Comment finit un ruisseau.</summary>
public enum StreamEnd
{
    /// <summary>Dans une mare, au creux d'un vallon.</summary>
    Pond = 0,

    /// <summary>Dans un lac, la mer ou une rivière : son dernier point est au milieu de cette case d'eau.</summary>
    Mouth = 1,

    /// <summary>Dans un autre ruisseau (un affluent) : son dernier point est sur le tracé de l'autre.</summary>
    Join = 2,
}

/// <summary>
/// Le tracé d'un ruisseau, de sa source (<paramref name="Source"/>) à son bout : une ligne de points qui suit les creux d'une
/// carte des hauteurs. Ses cases (<see cref="RiverKind.Stream"/>) sont celles que touche le tracé.
/// </summary>
public sealed record StreamCourse(IReadOnlyList<CoursePoint> Points, StreamEnd End, StreamSource Source = StreamSource.Spring);
