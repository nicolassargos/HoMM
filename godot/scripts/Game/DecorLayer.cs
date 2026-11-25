using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Le décor posé sur le sol, comme dans le premier Heroes : dans chaque case de forêt, quelques arbres seuls
/// (sapins, chênes, arbres d'automne) semés au hasard ; les montagnes, des massifs posés par le générateur comme dans
/// Heroes 3 (<see cref="AdventureMap.Massifs"/> ; sans eux, deux ou trois sommets par case).
/// Ils débordent un peu sur les cases voisines et sont dessinés de haut en bas : chaque arbre ou sommet recouvre
/// ceux de derrière, et les forêts et les chaînes de montagnes forment des masses continues.
/// Les règles ne changent pas : seul le dessin du terrain change (le sol de ces cases est celui qui les entoure).
/// Dessiné une seule fois, comme le terrain. Le hasard dépend de la case : le même décor revient à chaque partie.
/// </summary>
public partial class DecorLayer : Node2D
{
    public AdventureMap Map { get; set; } = null!;

    private static Texture2D[] Trees =>
        Art.VariantsOf("decor/pine").Concat(Art.VariantsOf("decor/oak")).Concat(Art.VariantsOf("decor/autumn")).ToArray();

    /// <summary>
    /// Les arbres d'une forêt sur ce sol (2026-11-19) : ceux du sol (decor/tree-sand, tree-snow…) s'il y en a, des sapins
    /// sur la roche, sinon ceux de l'herbe.
    /// </summary>
    private static Texture2D[] TreesOn(TerrainType ground) =>
        Art.VariantsOf($"decor/tree-{Art.Name(ground)}") is { Length: > 0 } own ? own
        : ground == TerrainType.Rock && Art.VariantsOf("decor/pine") is { Length: > 0 } pines ? pines
        : Trees;

    /// <summary>Les arbres des pentes (sapins et chênes, pas d'automne) : entre les massifs et à leur pied, comme dans Heroes 3.</summary>
    private static Texture2D[] SlopeTrees => Art.VariantsOf("decor/pine").Concat(Art.VariantsOf("decor/oak")).ToArray();

    /// <summary>Les sommets seuls, du même dessin que les massifs.</summary>
    private static Texture2D[] Peaks => Art.VariantsOf("decor/peak");

    /// <summary>Des éboulis (quelques blocs et pierres), au pied des chaînes de montagnes.</summary>
    private static Texture2D[] Scree => Art.VariantsOf("decor/scree");

    private static MassifCatalog? _shapes;
    /// <summary>Les formes des massifs (data/mountains.json), pour poser leur image.</summary>
    private static MassifCatalog Shapes => _shapes ??= MassifCatalog.FromJson(Godot.FileAccess.GetFileAsString("res://data/mountains.json"));

    private static HashSet<Texture2D>? _mountainImages;

    /// <summary>
    /// True pour l'image d'un massif ou d'un sommet seul (2026-11-17) : elle porte déjà sa lumière et ses pentes se fondent
    /// dans le sol, elle ne jette donc pas d'ombre (<see cref="ShadowLayer"/>), qui ferait une bande sombre autour.
    /// </summary>
    public static bool IsMountain(Texture2D image) =>
        (_mountainImages ??= Shapes.All.Select(s => Art.Get(s.Image)).Where(t => t != null).Select(t => t!).Concat(Peaks).ToHashSet())
        .Contains(image);

    /// <summary>True si ce terrain est dessiné par le décor (le sol de la case est alors de l'herbe).</summary>
    public static bool Covers(TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => Trees.Length > 0,
        TerrainType.Mountain => Peaks.Length > 0,
        _ => false,
    };

    /// <summary>La couleur qui marque une image de montagne pour le shader des pentes (le bleu à 0,998, invisible).</summary>
    private static readonly Color SlopeMark = new(1, 1, 0.998f);

    /// <summary>La couleur qui marque un arbre pour le shader : il penche dans le vent (le rouge à 0,998, invisible).</summary>
    private static readonly Color TreeMark = new(0.998f, 1, 1);

    private static HashSet<Texture2D>? _treeImages;
    private static HashSet<Texture2D>? _leafyImages;

    /// <summary>True pour l'image d'un arbre (tous les sols, brûlés compris) : il penche dans le vent.</summary>
    public static bool IsTree(Texture2D image) =>
        (_treeImages ??= new[] { "decor/pine", "decor/oak", "decor/autumn" }
            .Concat(Enum.GetValues<TerrainType>().Select(g => $"decor/tree-{Art.Name(g)}"))
            .SelectMany(Art.VariantsOf).ToHashSet()).Contains(image);

    /// <summary>
    /// True pour un arbre qui perd des feuilles dans le vent (<see cref="LeafLayer"/>) : les chênes, les arbres d'automne,
    /// ceux du marais ; pas les sapins, les palmiers ni les arbres brûlés.
    /// </summary>
    public static bool SheddingLeaves(Texture2D image) =>
        (_leafyImages ??= new[] { "decor/oak", "decor/autumn", "decor/tree-swamp" }.SelectMany(Art.VariantsOf).ToHashSet()).Contains(image);

    private ShaderMaterial? _material;

    /// <summary>Les bandes du décor (voir <see cref="_Ready"/>), dessinées une fois, libérées avec le calque.</summary>
    private readonly List<Rid> _bands = new();

    /// <summary>La hauteur d'une bande du décor, en pixels de la carte (un pixel d'art).</summary>
    private const int BandHeight = MapRenderer.ArtScale;

    // Pixel art : les images agrandies restent nettes ; les pentes des montagnes prennent le sol de dessous ; les arbres
    // penchent dans le vent.
    // Le décor est trié avec les objets et les héros (2026-11-25, <see cref="ActorLayer"/>) : les images dont le pied est
    // sur la même rangée de pixels d'art forment une bande, posée à ce pied et dessinée une seule fois
    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        YSortEnabled = true;
        if (ResourceLoader.Exists("res://shaders/mountain_slopes.gdshader"))
            Material = _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/mountain_slopes.gdshader") };
        Wind.Apply(_material);
        foreach (var band in Sprites(Map).GroupBy(s => (int)MathF.Floor(s.Foot.Y / BandHeight)))
        {
            var rid = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(rid, GetCanvasItem());
            RenderingServer.CanvasItemSetUseParentMaterial(rid, true);
            RenderingServer.CanvasItemSetDefaultTextureFilter(rid, RenderingServer.CanvasItemTextureFilter.Nearest);
            float y = band.Key * BandHeight;
            RenderingServer.CanvasItemSetTransform(rid, new Transform2D(0, new Vector2(0, y)));
            foreach (var (foot, image) in band)
            {
                var size = image.GetSize() * MapRenderer.ArtScale;
                var mark = IsMountain(image) ? SlopeMark : IsTree(image) ? TreeMark : Colors.White;
                RenderingServer.CanvasItemAddTextureRect(rid, new Rect2(foot - new Vector2(size.X / 2, size.Y + y), size), image.GetRid(), false, mark);
            }
            _bands.Add(rid);
        }
    }

    public override void _ExitTree()
    {
        foreach (var rid in _bands)
            RenderingServer.FreeRid(rid);
        _bands.Clear();
    }

    public override void _Process(double delta) => _material?.SetShaderParameter("wind_time", Wind.Now);

    /// <summary>
    /// Les arbres et les sommets de la carte, repérés par leur pied (le milieu du bas de l'image), triés de haut en bas
    /// (l'ordre du dessin). Toujours les mêmes pour une carte : les ombres (<see cref="ShadowLayer"/>) les relisent.
    /// </summary>
    public static List<(Vector2 Foot, Texture2D Image)> Sprites(AdventureMap map)
    {
        var trees = Trees;
        var forestGrounds = ForestGrounds.Of(map);
        var peaks = Peaks;
        const int t = MapRenderer.TileSize;
        // les placements ont été réglés pour des cases de 48 pixels de la carte : ils suivent la taille des cases
        const float k = t / 48f;
        int R(int min, int max, Random rng) => (int)MathF.Round(rng.Next(min, max) * k);

        // Chaque image est repérée par son pied (le milieu de son bas), puis tout est trié de haut en bas
        var sprites = new List<(Vector2 Foot, Texture2D Image)>();
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var o = new Vector2(x, y) * t;
            var rng = new Random(Seed(x, y));
            switch (map.GetTerrain(new Cell(x, y)))
            {
                case TerrainType.Forest when trees.Length > 0:
                    // quatre arbres en carré, un peu décalés, et parfois un cinquième n'importe où
                    // dans le désert, une palmeraie plus clairsemée : trois arbres, sans le cinquième
                    var ground = forestGrounds.GetValueOrDefault(new Cell(x, y), TerrainType.Grass);
                    var own = TreesOn(ground);
                    bool sparse = ground == TerrainType.Sand;
                    int gap = sparse ? rng.Next(4) : -1;
                    for (int row = 0; row < 2; row++)
                    for (int col = 0; col < 2; col++)
                    {
                        var at = o + new Vector2((12 + col * 24) * k + R(-6, 7, rng), (20 + row * 22) * k + R(-4, 5, rng));
                        var tree = own[rng.Next(own.Length)];
                        if (row * 2 + col != gap)
                            sprites.Add((at, tree));
                    }
                    if (!sparse && rng.Next(2) == 0)
                        sprites.Add((o + new Vector2(R(6, 42, rng), R(14, 48, rng)), own[rng.Next(own.Length)]));
                    break;

                case TerrainType.Mountain when peaks.Length > 0 && map.Massifs.Count == 0:
                    bool Rock(int dx, int dy) => map.InBounds(new Cell(x + dx, y + dy)) && map.GetTerrain(new Cell(x + dx, y + dy)) == TerrainType.Mountain;
                    // une carte sans massifs (une sauvegarde d'avant eux) : un sommet derrière, sur un côté ; au milieu
                    // d'une chaîne (et une fois sur deux ailleurs), un second grand sommet derrière, décalé, qui bouche le
                    // creux entre les rangées ; puis le grand devant, qui déborde sur les cases voisines
                    bool inner = Rock(1, 0) && Rock(-1, 0) && Rock(0, 1) && Rock(0, -1);
                    sprites.Add((o + new Vector2((rng.Next(2) == 0 ? 12 : 36) * k, 26 * k + R(-3, 4, rng)),
                        peaks[rng.Next(peaks.Length)]));
                    if (inner || rng.Next(2) == 0)
                        sprites.Add((o + new Vector2(t / 2 + (rng.Next(2) == 0 ? -1 : 1) * R(12, 18, rng), t / 2 + R(0, 6, rng)),
                            peaks[rng.Next(peaks.Length)]));
                    sprites.Add((o + new Vector2(t / 2 + R(-10, 11, rng), t + R(-2, 3, rng)), peaks[rng.Next(peaks.Length)]));
                    break;
            }
        }

        // les massifs posés par le générateur : l'image par le milieu de son bas sur le milieu du bas de son rectangle
        var rocks = new List<(Vector2 Foot, Texture2D Image)>();
        foreach (var massif in map.Massifs)
            if (Shapes.Find(massif.Id) is { } shape && Art.Get(shape.Image) is { } image)
                rocks.Add((new Vector2(massif.At.X + shape.Width / 2f, massif.At.Y + shape.Height) * t, image));
        FillGaps(map, rocks, peaks);
        sprites.AddRange(rocks);
        AddScree(map, sprites);

        sprites.Sort((a, b) => a.Foot.Y != b.Foot.Y ? a.Foot.Y.CompareTo(b.Foot.Y) : a.Foot.X.CompareTo(b.Foot.X));
        return sprites;
    }

    /// <summary>Les pixels opaques de chaque image (lus une fois), pour savoir ce qu'un massif recouvre.</summary>
    private static readonly Dictionary<Texture2D, (int W, int H, bool[] Opaque)> _opaque = new();

    private static (int W, int H, bool[] Opaque) OpaqueOf(Texture2D texture)
    {
        if (_opaque.TryGetValue(texture, out var known))
            return known;
        var image = texture.GetImage();
        if (image.IsCompressed())
            image.Decompress();
        int w = image.GetWidth(), h = image.GetHeight();
        var opaque = new bool[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            opaque[y * w + x] = image.GetPixel(x, y).A > 0.5f;
        return _opaque[texture] = (w, h, opaque);
    }

    /// <summary>
    /// Les trous entre les massifs (2026-11-17) : une case de montagne que leurs images couvrent mal (le sol s'y voit) prend
    /// en plus un sommet seul, posé sous la partie découverte. Seulement le dessin : la case est bloquée de toute façon.
    /// </summary>
    private static void FillGaps(AdventureMap map, List<(Vector2 Foot, Texture2D Image)> rocks, Texture2D[] peaks)
    {
        if (peaks.Length == 0)
            return;
        const int tile = MapRenderer.ArtTile, samples = 8, step = tile / samples; // 8 × 8 points par case, en pixels d'image
        var seen = new Dictionary<Cell, bool[]>();
        foreach (var (foot, image) in rocks)
        {
            var (w, h, opaque) = OpaqueOf(image);
            // le coin en haut à gauche de l'image, en pixels d'image de la carte
            int left = (int)MathF.Round(foot.X / MapRenderer.ArtScale) - w / 2, top = (int)MathF.Round(foot.Y / MapRenderer.ArtScale) - h;
            for (int cy = Math.Max(0, top / tile); cy <= Math.Min(map.Height - 1, (top + h) / tile); cy++)
            for (int cx = Math.Max(0, left / tile); cx <= Math.Min(map.Width - 1, (left + w) / tile); cx++)
            {
                var cell = new Cell(cx, cy);
                if (map.GetTerrain(cell) != TerrainType.Mountain)
                    continue;
                if (!seen.TryGetValue(cell, out var covered))
                    seen[cell] = covered = new bool[samples * samples];
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    int px = cx * tile + sx * step + step / 2 - left, py = cy * tile + sy * step + step / 2 - top;
                    if (px >= 0 && px < w && py >= 0 && py < h && opaque[py * w + px])
                        covered[sy * samples + sx] = true;
                }
            }
        }
        var fillers = new List<(Vector2 Foot, Texture2D Image)>();
        // les sommets du sol de chaque montagne (ceux de l'herbe s'il n'y en a pas)
        var grounds = MountainGrounds.Of(map);
        Texture2D[] PeaksOn(TerrainType ground) => ground switch
        {
            TerrainType.Sand when Art.VariantsOf("decor/peak-sable") is { Length: > 0 } sand => sand,
            TerrainType.Swamp when Art.VariantsOf("decor/peak-marais") is { Length: > 0 } swamp => swamp,
            _ => peaks,
        };
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var cell = new Cell(x, y);
            if (map.GetTerrain(cell) != TerrainType.Mountain)
                continue;
            var covered = seen.GetValueOrDefault(cell) ?? new bool[samples * samples];
            var open = Enumerable.Range(0, samples * samples).Where(i => !covered[i]).ToList();
            if (open.Count < samples * samples * GapShare)
                continue;
            // le sommet sous le milieu de la partie découverte, son pied au bas de celle-ci
            float mx = (float)open.Average(i => i % samples) + 0.5f, my = open.Max(i => i / samples) + 1f;
            var rng = new Random(Seed(x, y) + 7);
            // un sommet seul (pas d'arbre : au milieu des massifs, on n'en voyait que la cime derrière les pentes)
            var own = PeaksOn(grounds.GetValueOrDefault(cell, TerrainType.Grass));
            fillers.Add((new Vector2(x + mx / samples, y + Math.Max(0.6f, my / samples)) * MapRenderer.TileSize, own[rng.Next(own.Length)]));
        }
        rocks.AddRange(fillers);
    }

    /// <summary>
    /// Le pied des chaînes (2026-11-17) : sous une case de montagne dont la case du dessous est libre, souvent un éboulis
    /// posé sur le bas de la case, qui casse la rangée régulière des pointes du bord. Seulement le dessin.
    /// </summary>
    private static void AddScree(AdventureMap map, List<(Vector2 Foot, Texture2D Image)> sprites)
    {
        var scree = Scree;
        var trees = SlopeTrees;
        if (scree.Length + trees.Length == 0 || map.Massifs.Count == 0)
            return;
        bool Rock(Cell c) => map.InBounds(c) && map.GetTerrain(c) == TerrainType.Mountain;
        var grounds = MountainGrounds.Of(map);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var cell = new Cell(x, y);
            var below = cell.Offset(0, 1);
            // (des arbres au pied d'une montagne d'herbe seulement)
            if (!Rock(cell) || Rock(below) || grounds.GetValueOrDefault(cell) != TerrainType.Grass
                || map.InBounds(below) && (map.GetTerrain(below) == TerrainType.Water || map.GetRiver(below) != RiverKind.None))
                continue;
            var rng = new Random(Seed(x, y) + 13);
            if (rng.NextDouble() >= ScreeShare)
                continue;
            float fx = x + 0.2f + (float)rng.NextDouble() * 0.6f, fy = y + 1f - (float)rng.NextDouble() * 0.12f;
            // sans éboulis, rien que des arbres
            var image = trees.Length > 0 && (scree.Length == 0 || rng.NextDouble() < SlopeTreeShare)
                ? trees[rng.Next(trees.Length)] : scree[rng.Next(scree.Length)];
            sprites.Add((new Vector2(fx, fy) * MapRenderer.TileSize, image));
        }
    }

    /// <summary>La part des pieds de montagne qui prennent un arbre plutôt qu'un éboulis.</summary>
    private const double SlopeTreeShare = 0.5;

    /// <summary>La part des cases du bas d'une chaîne qui ont un éboulis.</summary>
    private const double ScreeShare = 0.6;

    /// <summary>La part découverte d'une case de montagne à partir de laquelle on y ajoute un sommet.</summary>
    private const float GapShare = 0.15f;

    /// <summary>Un nombre fixe par case, mélangé pour que les cases voisines n'aient pas le même décor.</summary>
    private static int Seed(int x, int y)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return (int)(h & 0x7fffffff);
    }
}
