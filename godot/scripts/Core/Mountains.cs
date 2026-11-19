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

    /// <summary>
    /// Toutes les cases que l'image couvre un peu (les sommets dépassent au-dessus des cases bloquées), en rangées alignées
    /// en bas sur <see cref="Rows"/> ; vide : les cases bloquées seulement.
    /// </summary>
    public List<string> Drawn { get; init; } = new();

    /// <summary>
    /// Sa place dans une chaîne (2026-11-17) : 3 un grand sommet (au cœur des montagnes larges), 2 un morceau de chaîne,
    /// 1 un contrefort (au bord), 0 un sommet seul d'une case.
    /// </summary>
    public int Tier { get; init; }

    /// <summary>
    /// Le sol où ses pentes se fondent (2026-11-17, comme Heroes 3 qui a des montagnes pour chaque terrain) : l'herbe, le
    /// sable ou le marais. Le générateur prend les massifs du sol qui entoure chaque montagne.
    /// </summary>
    public TerrainType Ground { get; init; } = TerrainType.Grass;

    /// <summary>Les cases que couvre l'image de ce massif posé en <paramref name="at"/> (2026-11-17 : pas sur l'eau).</summary>
    public IEnumerable<Cell> DrawnCellsAt(Cell at)
    {
        if (Drawn.Count == 0)
            return CellsAt(at);
        int top = Height - Drawn.Count;
        return Drawn.SelectMany((row, dy) => row.Select((ch, dx) => (ch, dx)).Where(p => p.ch == '#')
            .Select(p => at.Offset(p.dx, top + dy)));
    }
}

/// <summary>Un massif posé sur la carte : sa forme et le coin en haut à gauche de son rectangle.</summary>
public sealed record MountainMassif(string Id, Cell At);

/// <summary>Les formes de massifs, lues dans data/mountains.json.</summary>
public sealed class MassifCatalog
{
    private sealed record CatalogFile(List<MassifFile> Massifs);
    private sealed record MassifFile(string Id, string Image, List<string> Cells, List<string>? Drawn, int? Tier, string? Ground);

    public MassifCatalog(IEnumerable<MassifShape> shapes)
    {
        All = shapes.ToList();
        foreach (var shape in All.Where(s => !s.Cells.Any()))
            throw new FormatException($"Massif {shape.Id} : aucune case bloquée.");
    }

    public static readonly MassifCatalog Empty = new(Array.Empty<MassifShape>());

    public IReadOnlyList<MassifShape> All { get; }

    /// <summary>Les sommets seuls, d'une case : ils comblent les cases que les massifs ne couvrent pas.</summary>
    public IEnumerable<MassifShape> Singles => All.Where(s => s.Width == 1 && s.Height == 1 && s.Cells.Count() == 1);

    public MassifShape? Find(string id) => All.FirstOrDefault(s => s.Id == id);

    public static MassifCatalog FromJson(string json) =>
        new(GameData.Parse<CatalogFile>(json, "montagnes").Massifs.Select(m => new MassifShape
        {
            Id = m.Id, Image = m.Image, Rows = m.Cells, Drawn = m.Drawn ?? new(),
            Ground = m.Ground is { } ground ? Enum.Parse<TerrainType>(ground, ignoreCase: true) : TerrainType.Grass,
            Tier = m.Tier ?? (m.Cells.Sum(r => r.Count(ch => ch == '#')) switch { 1 => 0, >= 6 => 2, _ => 1 }),
        }));
}

/// <summary>Le sol de chaque montagne (2026-11-17) : celui qui l'entoure, pour ses images et pour le sol dessiné dessous.</summary>
public static class MountainGrounds
{
    /// <summary>
    /// Pour chaque case de montagne, le sol le plus courant tout près (à deux cases au plus), sinon autour de sa montagne
    /// (les cases qui se touchent) : l'herbe, le sable ou le marais ; la forêt compte comme de l'herbe, l'eau ne compte
    /// pas ; de l'herbe s'il n'y a rien autour. Une chaîne entre l'herbe et le sable change ainsi de pentes en chemin.
    /// </summary>
    public static Dictionary<Cell, TerrainType> Of(AdventureMap map) => Under(map, TerrainType.Mountain);

    /// <summary>
    /// Le sol sous chaque case de <paramref name="covered"/> (montagne ou forêt), pris autour comme pour
    /// <see cref="Of"/> ; pour une forêt, les montagnes ne comptent pas.
    /// </summary>
    internal static Dictionary<Cell, TerrainType> Under(AdventureMap map, TerrainType covered)
    {
        var result = new Dictionary<Cell, TerrainType>();
        bool Rock(Cell c) => map.InBounds(c) && map.GetTerrain(c) == covered;
        bool Counts(TerrainType t) => t != TerrainType.Water && t != covered && (covered != TerrainType.Forest || t != TerrainType.Mountain);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var start = new Cell(x, y);
            if (!Rock(start) || result.ContainsKey(start))
                continue;
            var group = new List<Cell> { start };
            var seen = new HashSet<Cell> { start };
            var around = new Dictionary<TerrainType, int>();
            for (int i = 0; i < group.Count; i++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var n = group[i].Offset(dx, dy);
                    if (!map.InBounds(n) || !seen.Add(n))
                        continue;
                    if (Rock(n))
                        group.Add(n);
                    else if (map.GetTerrain(n) is var t && Counts(t))
                    {
                        var ground = TerrainInfo.SoilOf(t);
                        around[ground] = around.GetValueOrDefault(ground) + 1;
                    }
                }
            var main = around.Count == 0 ? TerrainType.Grass : around.MaxBy(p => p.Value).Key;
            foreach (var c in group)
            {
                var near = new Dictionary<TerrainType, int>();
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    var n = c.Offset(dx, dy);
                    if (map.InBounds(n) && map.GetTerrain(n) is var t && Counts(t) && t != TerrainType.Mountain)
                    {
                        var ground = TerrainInfo.SoilOf(t);
                        near[ground] = near.GetValueOrDefault(ground) + 1;
                    }
                }
                result[c] = near.Count == 0 ? main : near.MaxBy(p => p.Value).Key;
            }
        }
        return result;
    }
}

/// <summary>
/// Le sol de chaque forêt (2026-11-19) : celui qui l'entoure, comme pour les montagnes ; une forêt du désert pousse sur
/// le sable, avec les arbres de ce sol (et plus sur une tache d'herbe).
/// </summary>
public static class ForestGrounds
{
    public static Dictionary<Cell, TerrainType> Of(AdventureMap map) => MountainGrounds.Under(map, TerrainType.Forest);
}
