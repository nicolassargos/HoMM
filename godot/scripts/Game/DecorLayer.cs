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

    /// <summary>Les formes des arbres de l'herbe (2026-11-28 : plus seulement ronds ou pointus), chacune avec ses variantes.</summary>
    private static readonly string[] GrassTreeShapes =
    {
        "decor/pine", "decor/oak", "decor/autumn", "decor/tree-poplar", "decor/tree-birch", "decor/tree-willow",
        "decor/tree-crooked", "decor/tree-fir", "decor/tree-young",
    };

    private static Texture2D[]? _trees;

    /// <summary>
    /// Les arbres de l'herbe, chaque forme aussi souvent que les autres quel que soit son nombre de variantes : chaque image
    /// revient autant de fois qu'il le faut (une forme de deux variantes, deux fois plus que celle de quatre).
    /// </summary>
    private static Texture2D[] Trees => _trees ??= Evenly(GrassTreeShapes);

    /// <summary>
    /// Les bosquets (2026-11-28, idée de l'utilisateur) : chaque forme a sa carte de présence, un bruit doux d'environ
    /// GroveSize cases ; élevé à la puissance GroveFocus, il laisse dominer deux ou trois formes à chaque endroit. Chaque
    /// forêt paraît ainsi plus uniforme, et la carte reste variée d'une forêt à l'autre ; GroveMix : la part d'une forme
    /// absente, pour qu'il reste quelques arbres d'autres sortes.
    /// </summary>
    private const float GroveSize = 6f, GroveFocus = 4f, GroveMix = 0.04f;

    private static Texture2D[][]? _groveShapes;

    /// <summary>
    /// Les résineux (sapins, épicéas) restent entre eux (l'utilisateur, 2026-11-28) : un autre bruit doux sépare les bois de
    /// résineux des bois de feuillus ; au-dessus de ConiferLevel, les résineux, en dessous, les feuillus, mêlés le long de
    /// la limite ; ConiferStray : la part de l'autre sorte qui reste dans chaque bois (quelques arbres, pas tout à fait purs).
    /// </summary>
    private static readonly string[] Conifers = { "decor/pine", "decor/tree-fir" };

    private const float ConiferLevel = 0.55f, ConiferStray = 0.1f;

    /// <summary>Le poids de chaque forme d'arbre de l'herbe sur la case (x, y) (voir <see cref="GroveSize"/>).</summary>
    private static float[] GroveWeights(int x, int y, int count)
    {
        var weights = new float[count];
        float n = SmoothNoise(x / GroveSize + 101.9f, y / GroveSize + 57.1f);
        float conifer = Math.Clamp((n - ConiferLevel) / 0.15f + 0.5f, ConiferStray, 1 - ConiferStray);
        // la part de chaque sorte (résineux, feuillus), puis les formes de la sorte selon leurs bruits
        float coniferSum = 0, leafySum = 0;
        for (int i = 0; i < count; i++)
        {
            weights[i] = GroveMix + MathF.Pow(SmoothNoise(x / GroveSize + 17.3f * i, y / GroveSize + 31.7f * i), GroveFocus);
            if (Conifers.Contains(GrassTreeShapes[i]))
                coniferSum += weights[i];
            else
                leafySum += weights[i];
        }
        for (int i = 0; i < count; i++)
            weights[i] *= Conifers.Contains(GrassTreeShapes[i]) ? conifer / coniferSum : (1 - conifer) / leafySum;
        return weights;
    }

    /// <summary>Un arbre de l'herbe tiré selon les poids des formes, puis une de ses variantes.</summary>
    private static Texture2D GroveTree(float[] weights, Random rng)
    {
        var shapes = _groveShapes!;
        float pick = (float)rng.NextDouble() * weights.Sum();
        int i = 0;
        while (i < shapes.Length - 1 && (pick -= weights[i]) > 0)
            i++;
        return shapes[i][rng.Next(shapes[i].Length)];
    }

    /// <summary>
    /// La part d'une case, depuis son bord, que la rive peut prendre (le shader du sol la fait onduler jusque dans la case
    /// de terre, et arrondit les coins) : le pied d'un arbre reste au-delà, sinon il paraît planté dans l'eau.
    /// </summary>
    private const float ShoreMargin = 0.45f;

    /// <summary>
    /// Le pied d'un arbre de la case (x, y), en pixels de la carte, ramené loin des bords qui touchent un lac, la mer ou une
    /// rivière (2026-11-28 : des arbres paraissaient pousser dans l'eau, l'utilisateur).
    /// </summary>
    private static Vector2 AwayFromWater(AdventureMap map, int x, int y, Vector2 foot)
    {
        const float t = MapRenderer.TileSize;
        bool Wet(int dx, int dy)
        {
            var c = new Cell(x + dx, y + dy);
            return map.InBounds(c) && (map.GetTerrain(c) == TerrainType.Water || map.GetRiver(c) == RiverKind.River);
        }
        var o = new Vector2(x, y) * t;
        var local = foot - o;
        float lo = t * ShoreMargin, hi = t * (1 - ShoreMargin);
        if (Wet(-1, 0)) local.X = Math.Max(local.X, lo);
        if (Wet(1, 0)) local.X = Math.Min(local.X, hi);
        if (Wet(0, -1)) local.Y = Math.Max(local.Y, lo);
        if (Wet(0, 1)) local.Y = Math.Min(local.Y, hi);
        // un coin d'eau seul (en diagonale) : la rive arrondie y entre en quart de cercle
        foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            if (Wet(dx, dy) && !Wet(dx, 0) && !Wet(0, dy))
            {
                var corner = new Vector2(dx > 0 ? t : 0, dy > 0 ? t : 0);
                var away = local - corner;
                float reach = t * ShoreMargin * 1.2f;
                if (away.Length() < reach)
                    local = corner + (away.Length() > 0.01f ? away.Normalized() : new Vector2(-dx, -dy).Normalized()) * reach;
            }
        return o + local;
    }

    /// <summary>Un bruit doux de 0 à 1 (des valeurs tirées aux points entiers, mêlées en courbe entre eux).</summary>
    private static float SmoothNoise(float x, float y)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        float V(int i, int j) => Seed(i + 1000, j + 2000) % 10000 / 10000f;
        float top = V(x0, y0) + (V(x0 + 1, y0) - V(x0, y0)) * fx;
        float bottom = V(x0, y0 + 1) + (V(x0 + 1, y0 + 1) - V(x0, y0 + 1)) * fx;
        return top + (bottom - top) * fy;
    }

    private static Texture2D[] Evenly(string[] shapes)
    {
        var groups = shapes.Select(Art.VariantsOf).Where(g => g.Length > 0).ToList();
        int Lcm(int a, int b) { int x = a, y = b; while (y != 0) (x, y) = (y, x % y); return a / x * b; }
        int each = groups.Aggregate(1, (m, g) => Lcm(m, g.Length));
        return groups.SelectMany(g => g.SelectMany(image => Enumerable.Repeat(image, each / g.Length))).ToArray();
    }

    /// <summary>
    /// Les arbres d'une forêt sur ce sol (2026-11-19) : ceux du sol (decor/tree-sand, tree-snow…) s'il y en a, des sapins
    /// sur la roche, sinon ceux de l'herbe.
    /// </summary>
    private static Texture2D[] TreesOn(TerrainType ground) =>
        Art.VariantsOf($"decor/tree-{Art.Name(ground)}") is { Length: > 0 } own ? own
        : ground == TerrainType.Rock && Art.VariantsOf("decor/pine") is { Length: > 0 } pines ? pines
        : Trees;

    /// <summary>Sur la terre brûlée, une case de forêt sur tant garde trois arbres (les autres, deux).</summary>
    private const int BurntThird = 3;

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
    public static readonly Color SlopeMark = new(1, 1, 0.998f);

    /// <summary>La couleur qui marque un arbre pour le shader : il penche dans le vent (le rouge à 0,998, invisible).</summary>
    private static readonly Color TreeMark = new(0.998f, 1, 1);

    private static HashSet<Texture2D>? _treeImages;
    private static HashSet<Texture2D>? _leafyImages;

    /// <summary>True pour l'image d'un arbre (tous les sols, brûlés compris) : il penche dans le vent.</summary>
    public static bool IsTree(Texture2D image) =>
        (_treeImages ??= GrassTreeShapes
            .Concat(Enum.GetValues<TerrainType>().Select(g => $"decor/tree-{Art.Name(g)}"))
            .SelectMany(Art.VariantsOf).ToHashSet()).Contains(image);

    /// <summary>
    /// True pour un arbre qui perd des feuilles dans le vent (<see cref="LeafLayer"/>) : les chênes, les arbres d'automne,
    /// ceux du marais, les arbres à feuilles de l'herbe ; pas les sapins, les épicéas, les palmiers ni les arbres brûlés.
    /// </summary>
    public static bool SheddingLeaves(Texture2D image) =>
        (_leafyImages ??= new[]
        {
            "decor/oak", "decor/autumn", "decor/tree-swamp", "decor/tree-poplar", "decor/tree-birch", "decor/tree-willow",
            "decor/tree-crooked", "decor/tree-young",
        }.SelectMany(Art.VariantsOf).ToHashSet()).Contains(image);

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
                    // les arbres de l'herbe : en bosquets (voir GroveSize)
                    _groveShapes ??= GrassTreeShapes.Select(Art.VariantsOf).Where(g => g.Length > 0).ToArray();
                    var grove = own == trees ? GroveWeights(x, y, _groveShapes.Length) : null;
                    Texture2D Pick() => grove != null ? GroveTree(grove, rng) : own[rng.Next(own.Length)];
                    // sur la terre brûlée, les arbres morts, sombres sur un sol sombre, se fondaient en une masse : deux
                    // seulement par case, parfois trois, pour voir chacun
                    bool sparse = ground == TerrainType.Sand || ground == TerrainType.Burnt;
                    int gap = sparse ? rng.Next(4) : -1;
                    int gap2 = ground == TerrainType.Burnt && rng.Next(BurntThird) != 0 ? (gap + 1 + rng.Next(3)) % 4 : -1;
                    for (int row = 0; row < 2; row++)
                    for (int col = 0; col < 2; col++)
                    {
                        var at = o + new Vector2((12 + col * 24) * k + R(-6, 7, rng), (20 + row * 22) * k + R(-4, 5, rng));
                        at = AwayFromWater(map, x, y, at);
                        var tree = Pick();
                        if (row * 2 + col != gap && row * 2 + col != gap2)
                            sprites.Add((at, tree));
                    }
                    if (!sparse && rng.Next(2) == 0)
                        sprites.Add((AwayFromWater(map, x, y, o + new Vector2(R(6, 42, rng), R(14, 48, rng))), Pick()));
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
        var mineBacks = MineBacks(map);
        foreach (var massif in map.Massifs)
            if (Shapes.Find(massif.Id) is { } shape && Art.Get(shape.Image) is { } image)
            {
                var foot = new Vector2(massif.At.X + shape.Width / 2f, massif.At.Y + shape.Height) * t;
                rocks.Add((foot + new Vector2(0, BehindMineDrop(shape, massif.At, image, mineBacks)), image));
            }
        FillGaps(map, rocks, peaks);
        sprites.AddRange(rocks);
        AddScree(map, sprites);

        sprites.Sort((a, b) => a.Foot.Y != b.Foot.Y ? a.Foot.Y.CompareTo(b.Foot.Y) : a.Foot.X.CompareTo(b.Foot.X));
        return sprites;
    }

    /// <summary>Les trois cases juste derrière chaque mine creusée dans la roche (son image : la mine et sa montagne).</summary>
    private static HashSet<Cell> MineBacks(AdventureMap map) =>
        map.Objects.OfType<Mine>().Where(m => Art.Get($"objects/mine-{Art.Name(m.Resource)}-mountain") != null)
            .SelectMany(m => new[] { -1, 0, 1 }.Select(dx => m.Position.Offset(dx, -2))).ToHashSet();

    /// <summary>
    /// Un massif adossé à une mine creusée (2026-11-27) : sous sa roche, ses pentes prennent la couleur du sol, et la roche
    /// de la mine ne monte qu'à son sommet ; une bande de sol les séparait. Si toute sa rangée du bas est juste derrière la
    /// mine, il descend (d'une case au plus, sur les cases de la mine, qui le recouvre) jusqu'à ce que sa roche passe
    /// derrière les épaules de la mine. Seulement le dessin, en pixels d'écran.
    /// </summary>
    private static float BehindMineDrop(MassifShape shape, Cell at, Texture2D image, HashSet<Cell> mineBacks)
    {
        var bottom = shape.Cells.Where(c => c.Dy == shape.Height - 1).Select(c => at.Offset(c.Dx, c.Dy)).ToList();
        if (mineBacks.Count == 0 || !bottom.All(mineBacks.Contains))
            return 0;
        var (w, h, lowest) = RockBottomOf(image);
        const int tile = MapRenderer.ArtTile;
        // la bande de pente sous la roche, au-dessus des cases de la mine (au milieu de chacune)
        int left = w / 2 - shape.Width * tile / 2, band = 0;
        foreach (var (dx, _) in shape.Cells.Where(c => c.Dy == shape.Height - 1))
            for (int x = left + dx * tile + tile / 4; x < left + (dx + 1) * tile - tile / 4; x++)
                if (x >= 0 && x < w && lowest[x] >= 0)
                    band = Math.Max(band, h - 1 - lowest[x]);
        const int intoMine = 20; // la roche descend encore de tant derrière la mine
        return Math.Min(band + intoMine, tile) * MapRenderer.ArtScale;
    }

    /// <summary>Pour chaque colonne d'une image, le pixel de roche le plus bas (pas une pente verte, que le shader repeint) ; -1 sans roche.</summary>
    private static readonly Dictionary<Texture2D, (int W, int H, int[] Lowest)> _rockBottom = new();

    private static (int W, int H, int[] Lowest) RockBottomOf(Texture2D texture)
    {
        if (_rockBottom.TryGetValue(texture, out var known))
            return known;
        var image = texture.GetImage();
        if (image.IsCompressed())
            image.Decompress();
        int w = image.GetWidth(), h = image.GetHeight();
        var lowest = new int[w];
        for (int x = 0; x < w; x++)
        {
            lowest[x] = -1;
            for (int y = h - 1; y >= 0 && lowest[x] < 0; y--)
                if (image.GetPixel(x, y) is var c && c.A > 0.5f && !(c.G > c.R + 0.06f && c.G > c.B + 0.06f))
                    lowest[x] = y;
        }
        return _rockBottom[texture] = (w, h, lowest);
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
