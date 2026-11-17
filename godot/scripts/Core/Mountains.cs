using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Une forme de massif de montagnes (data/mountains.json, écrit par tools/art/empreintes.py d'après les images) : comme
/// dans Heroes 3, une montagne est une image posée sur un rectangle de cases dont seules certaines sont bloquées
/// (2026-11-17). <see cref="Cells"/> : les cases bloquées, depuis le coin en haut à gauche du rectangle.
/// </summary>
public sealed class MassifShape
{
    public string Id { get; init; } = "";
    /// <summary>L'image du massif (dans art/), posée par le milieu de son bas sur le milieu du bas du rectangle.</summary>
    public string Image { get; init; } = "";
    /// <summary>Les rangées du rectangle, de haut en bas : « # » bloquée, « . » libre.</summary>
    public List<string> Rows { get; init; } = new();

    public int Width => Rows.Count == 0 ? 0 : Rows.Max(r => r.Length);
    public int Height => Rows.Count;

    public IEnumerable<(int Dx, int Dy)> Cells =>
        Rows.SelectMany((row, dy) => row.Select((ch, dx) => (ch, dx)).Where(p => p.ch == '#').Select(p => (p.dx, dy)));

    /// <summary>Les cases bloquées par ce massif posé en <paramref name="at"/> (le coin en haut à gauche).</summary>
    public IEnumerable<Cell> CellsAt(Cell at) => Cells.Select(c => at.Offset(c.Dx, c.Dy));
}

/// <summary>Un massif posé sur la carte : sa forme et le coin en haut à gauche de son rectangle.</summary>
public sealed record MountainMassif(string Id, Cell At);

/// <summary>Les formes de massifs, lues dans data/mountains.json.</summary>
public sealed class MassifCatalog
{
    private sealed record CatalogFile(List<MassifFile> Massifs);
    private sealed record MassifFile(string Id, string Image, List<string> Cells);

    public MassifCatalog(IEnumerable<MassifShape> shapes)
    {
        All = shapes.ToList();
        foreach (var shape in All.Where(s => !s.Cells.Any()))
            throw new FormatException($"Massif {shape.Id} : aucune case bloquée.");
    }

    public static readonly MassifCatalog Empty = new(Array.Empty<MassifShape>());

    public IReadOnlyList<MassifShape> All { get; }

    /// <summary>Les sommets seuls, d'une case : ils comblent les cases que les massifs ne couvrent pas.</summary>
    public IEnumerable<MassifShape> Singles => All.Where(s => s.Cells.Count() == 1);

    public MassifShape? Find(string id) => All.FirstOrDefault(s => s.Id == id);

    public static MassifCatalog FromJson(string json) =>
        new(GameData.Parse<CatalogFile>(json, "montagnes").Massifs.Select(m => new MassifShape { Id = m.Id, Image = m.Image, Rows = m.Cells }));
}
