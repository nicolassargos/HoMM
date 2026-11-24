using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Le sol de toute la carte en une seule image, dessinée par le shader <c>shaders/terrain.gdshader</c> :
/// les terrains se rejoignent par des bords ondulés au lieu de carrés (étape 5).
/// Le shader reçoit le terrain de chaque case et l'image de chaque terrain ; il n'y a pas d'images de bords
/// à préparer, et changer l'image d'un terrain change aussi ses bords.
/// </summary>
public partial class SmoothGround : ColorRect
{
    public const string ShaderPath = "res://shaders/terrain.gdshader";

    /// <summary>Images par terrain : l'image et sa variante (art/terrain/grass-2.png), mélangées par le shader.</summary>
    private const int Variants = 2;

    /// <summary>
    /// Rang de chaque terrain : un terrain recouvre les bords de ceux de rang plus faible.
    /// L'herbe est au-dessus de tout : les rives et les chemins deviennent ondulés.
    /// </summary>
    public static int Rank(TerrainType terrain) => terrain switch
    {
        TerrainType.Water => 0,
        TerrainType.Sand => 2,
        TerrainType.Swamp => 3,
        TerrainType.Rock => 4,
        TerrainType.Burnt => 5,
        TerrainType.Snow => 6,
        _ => 7, // herbe (et le sol des forêts et des montagnes)
    };

    /// <summary>
    /// Développement : « -- --ground=edge_width:9,foam:0.6 » (une couleur : « nom:0.2/0.18/0.2 ») change des réglages du shader sans recompiler,
    /// pour comparer des essais sur des captures d'écran.
    /// </summary>
    private static void ApplyDevelopmentSettings(ShaderMaterial material)
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ground="));
        if (arg == null)
            return;
        foreach (var setting in arg["--ground=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = setting.Split(':');
            var numbers = parts.Length == 2 ? parts[1].Split('/').Select(s => float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : float.NaN).ToArray() : Array.Empty<float>();
            // un nombre, ou une couleur « r/g/b » (de 0 à 1)
            if (numbers.Length == 1 && !float.IsNaN(numbers[0]))
                material.SetShaderParameter(parts[0], numbers[0]);
            else if (numbers.Length == 3 && !numbers.Any(float.IsNaN))
                material.SetShaderParameter(parts[0], new Vector3(numbers[0], numbers[1], numbers[2]));
            else
                GD.PrintErr($"Réglage du sol illisible : {setting}");
        }
    }

    /// <summary>
    /// Les images des sortes de routes (terre, gravier, pavés : art/terrain/road.png, road-gravel.png, road-cobbles.png).
    /// Sans image, une sorte prend celle de la terre teintée (en attendant ses images), ou la couleur unie de la route.
    /// </summary>
    private static Texture2DArray RoadTiles(Vector2I size)
    {
        Image? Load(string name) => Art.GetImage(name) is { } image && image.GetSize() == size ? (Image)image.Duplicate() : null;
        var dirt = Load("terrain/road");
        if (dirt == null)
        {
            dirt = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
            dirt.Fill(MapRenderer.RoadColor);
        }
        dirt.Convert(Image.Format.Rgba8);
        Image Tinted(Color tint, float light)
        {
            var image = (Image)dirt.Duplicate();
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
            {
                var c = image.GetPixel(x, y);
                float grey = (c.R + c.G + c.B) / 3f * light;
                image.SetPixel(x, y, new Color(grey * tint.R, grey * tint.G, grey * tint.B, c.A));
            }
            return image;
        }
        var gravel = Load("terrain/road-gravel") ?? Tinted(new Color(1f, 0.97f, 0.9f), 1.25f);
        var cobbles = Load("terrain/road-cobbles") ?? Tinted(new Color(0.92f, 0.95f, 1.05f), 1.05f);
        gravel.Convert(Image.Format.Rgba8);
        cobbles.Convert(Image.Format.Rgba8);
        var tiles = new Texture2DArray();
        tiles.CreateFromImages(new Godot.Collections.Array<Image> { dirt, gravel, cobbles });
        return tiles;
    }

    /// <summary>Les réglages du shader qui sont des longueurs (en pixels d'art), tels qu'ils ont été choisis pour des cases de 24.</summary>
    private static readonly (string Name, float Value)[] LengthsAt24 =
    {
        ("edge_bias", 2.0f), ("edge_wave", 5.5f), ("wave_scale", 13.0f), ("blend", 3.5f), ("grain_scale", 3.0f),
        ("variant_scale", 30.0f), ("road_width", 3.75f), ("road_wave", 1.0f), ("road_wave_scale", 7.0f),
        ("road_jag", 0.75f), ("road_scatter", 1.875f), ("road_shadow_length", 2.25f), ("road_vary", 0.375f), ("road_vary_scale", 9.0f), ("shade_scale", 70.0f), ("beach_width", 6.0f), ("beach_wave", 1.5f), ("flower_cell", 7.0f),
        ("stream_width", 1.8f), ("stream_wave", 1.0f), ("river_bank", 7.0f), ("river_wave", 6.75f), ("bridge_width", 4.5f), ("stream_bend", 4.5f),
    };

    /// <summary>
    /// Le sol de la carte, ou null s'il manque le shader ou les images des terrains
    /// (le sol est alors dessiné case par case, sans bords).
    /// </summary>
    public static SmoothGround? Create(AdventureMap map)
    {
        // les forêts et les montagnes ont le sol qui les entoure (leur décor est dessiné à part)
        var mountainGrounds = MountainGrounds.Of(map);
        var forestGrounds = ForestGrounds.Of(map);
        var ground = Create(map.Width, map.Height, cell =>
        {
            var terrain = map.GetTerrain(cell);
            var under = terrain == TerrainType.Mountain ? mountainGrounds.GetValueOrDefault(cell, TerrainType.Grass)
                : terrain == TerrainType.Forest ? forestGrounds.GetValueOrDefault(cell, TerrainType.Grass)
                : DecorLayer.Covers(terrain) ? TerrainType.Grass : terrain;
            return (under, map.GetRoadKind(cell), map.GetRiver(cell));
        }, courses: map.Streams);
        if (ground?.Material is ShaderMaterial material)
        {
            // le pied des montagnes
            material.SetShaderParameter("mountain_field", ImageTexture.CreateFromImage(
                MountainField.Build(map.Width, map.Height, cell => map.GetTerrain(cell) == TerrainType.Mountain)));
            // les cases sans mares du marais : sous les objets (posés au début de la partie), les forêts et les montagnes
            var dry = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.L8);
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Cell(x, y);
                if (map.OccupantAt(cell) != null || DecorLayer.Covers(map.GetTerrain(cell)))
                    dry.SetPixel(x, y, Colors.White);
            }
            material.SetShaderParameter("dry_field", ImageTexture.CreateFromImage(dry));
        }
        return ground;
    }

    /// <summary>
    /// Le sol du champ de bataille : un seul terrain, qui couvre au moins la taille donnée en pixels de l'écran (des cases
    /// entières de la carte, à couper par le parent). Plus travaillé que la carte, vu de plus près (demandé par
    /// l'utilisateur, 2026-11-10) : des taches de terre battue (des cases de chemin groupées, placées selon
    /// <paramref name="seed"/>), des ombres et des éclaircies plus marquées, plus de fleurs et de cailloux.
    /// </summary>
    public static SmoothGround? CreateBattle(TerrainType terrain, Vector2 size, int seed)
    {
        int width = (int)Math.Ceiling(size.X / MapRenderer.TileSize), height = (int)Math.Ceiling(size.Y / MapRenderer.TileSize);
        var ground = DecorLayer.Covers(terrain) ? TerrainType.Grass : terrain;
        // la terre battue : quelques taches de 2 à 4 cases voisines, loin des bords
        var worn = new System.Collections.Generic.HashSet<Cell>();
        var random = new Random(seed);
        // le désert n'a pas de terre battue : son sol de pierres est dessiné à part (BattleFieldView.BuildStones) ; le marais
        // non plus : ses flaques le sont (BattleFieldView.BuildPuddles) ; ni la neige, épaisse, ni la roche (des taches orange), ni la terre brûlée
        int patches = ground is TerrainType.Sand or TerrainType.Swamp or TerrainType.Snow or TerrainType.Rock or TerrainType.Burnt ? 0 : 3 + random.Next(3);
        for (int i = 0; i < patches; i++)
        {
            var cell = new Cell(2 + random.Next(Math.Max(1, width - 4)), 3 + random.Next(Math.Max(1, height - 4)));
            for (int n = 2 + random.Next(3); n > 0; n--)
            {
                worn.Add(cell);
                cell = cell.Offset(random.Next(3) - 1, random.Next(3) - 1);
            }
        }
        var dirt = Art.GetImage("terrain/road");
        var result = Create(width, height, cell => (worn.Contains(cell) ? DirtId : ground, RoadKind.None, RiverKind.None), dirt,
            ground == TerrainType.Swamp ? FewerSpecks : null);
        if (result?.Material is ShaderMaterial material)
        {
            // des ombres plus discrètes sur le sable clair (remarque de l'utilisateur)
            material.SetShaderParameter("shade", ground == TerrainType.Sand ? 0.05f : 0.13f);
            material.SetShaderParameter("shade_scale", 45f * (float)material.GetShaderParameter("ART") / 24f);
            material.SetShaderParameter("flower_density", 0.22f);
            material.SetShaderParameter("pebble_share", 0.35f);
            material.SetShaderParameter("ground_stones", 0f); // le champ a ses propres pierres (BattleFieldView)
            material.SetShaderParameter("swamp_ground", -1); // et ses flaques (les mares de la carte iraient sous les troupes)
            if (ground == TerrainType.Rock)
            {
                // vue de près, la roche de la carte faisait du cuir, ses dalles un pavage (remarques de l'utilisateur) : au
                // combat, une terre désolée de gravier (2026-11-20 : entre le désert et la terre brûlée ; ses ravins et ses
                // fissures décalés selon le combat), et la roche en images posées dessus (BattleFieldView.BuildDetails)
                material.SetShaderParameter("rock_outcrops", 0.02f);
                material.SetShaderParameter("rock_grain", 0.1f);
                material.SetShaderParameter("rock_shift", new Vector2(random.Next(2000), random.Next(2000)));
            }
            ApplyDevelopmentSettings(material);
        }
        return result;
    }

    /// <summary>
    /// Moins de taches vertes sur le sol du marais au combat (remarque de l'utilisateur, 2026-11-16 : le motif de l'image se
    /// voyait trop, sur un grand champ sans décor de la carte par-dessus) : les touffes vertes de l'image (le vert l'emporte
    /// sur le rouge, avec leur contour olive sombre) sont reconnues une à une ; les petites (4 pixels au plus) restent, les
    /// grandes sont remplacées par la boue d'à côté (choix de l'utilisateur : en garder une sur trois en laissait trop). La
    /// carte garde son image.
    /// </summary>
    private static void FewerSpecks(Image image)
    {
        int w = image.GetWidth(), h = image.GetHeight();
        bool Speck(Color c) => c.G >= c.R * 0.9f && c.B < c.G * 0.6f;
        var seen = new bool[w, h];
        for (int y0 = 0; y0 < h; y0++)
        for (int x0 = 0; x0 < w; x0++)
        {
            if (seen[x0, y0] || !Speck(image.GetPixel(x0, y0)))
                continue;
            // la touffe : les pixels verts voisins (l'image se répète : les bords se rejoignent)
            var cluster = new List<(int X, int Y)>();
            var stack = new Stack<(int X, int Y)>();
            stack.Push((x0, y0));
            seen[x0, y0] = true;
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                cluster.Add((x, y));
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = (x + dx + w) % w, ny = (y + dy + h) % h;
                    if (!seen[nx, ny] && Speck(image.GetPixel(nx, ny)))
                    {
                        seen[nx, ny] = true;
                        stack.Push((nx, ny));
                    }
                }
            }
            if (cluster.Count <= 4)
                continue; // gardée
            // la boue d'à côté : le premier pixel qui n'est pas vert, en s'éloignant vers le haut, puis la gauche
            foreach (var (x, y) in cluster)
                for (int d = 1; d < Math.Max(w, h); d++)
                {
                    var c = image.GetPixel(x, (y - d + h * d) % h);
                    if (!Speck(c))
                    {
                        image.SetPixel(x, y, c);
                        break;
                    }
                    c = image.GetPixel((x - d + w * d) % w, y);
                    if (!Speck(c))
                    {
                        image.SetPixel(x, y, c);
                        break;
                    }
                }
        }
    }

    /// <summary>
    /// Le numéro de la terre battue du champ de bataille : la place libre du shader (le 1, l'ancienne route), avec l'image
    /// de la route, sous l'herbe (ses bords ondulés sont ceux de l'herbe, comme le sable sur la carte).
    /// </summary>
    private const TerrainType DirtId = (TerrainType)1;

    /// <summary>
    /// Le sol de <paramref name="width"/> × <paramref name="height"/> cases, chaque case : son terrain, sa route et sa rivière ;
    /// <paramref name="dirt"/> : l'image de la terre battue (DirtId), s'il y en a.
    /// </summary>
    private static SmoothGround? Create(int width, int height, Func<Cell, (TerrainType Terrain, RoadKind Road, RiverKind River)> cellAt, Image? dirt = null,
        Action<Image>? retouch = null, IReadOnlyList<StreamCourse>? courses = null)
    {
        if (!Godot.FileAccess.FileExists(ShaderPath))
            return null;
        var terrains = Enum.GetValues<TerrainType>();
        // Il faut au moins l'image de l'herbe ; un terrain sans image prend sa couleur unie
        if (Art.GetImage($"terrain/{Art.Name(TerrainType.Grass)}") is not { } grass)
            return null;
        var size = grass.GetSize();

        // Deux couches par numéro de terrain : l'image et sa variante (terrain/grass-2.png), ou deux fois l'image.
        // Les numéros sans terrain (le 1, ancienne route) ont des couches vides, pour que la couche = numéro × 2.
        var tiles = new Godot.Collections.Array<Image>();
        int maxId = terrains.Max(t => (int)t);
        for (int id = 0; id <= maxId; id++)
        {
            if (id == (int)DirtId && dirt != null && dirt.GetSize() == size)
            {
                dirt.Convert(Image.Format.Rgba8);
                tiles.Add(dirt);
                tiles.Add(dirt);
                continue;
            }
            if (!Enum.IsDefined(typeof(TerrainType), id))
            {
                var empty = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
                tiles.Add(empty);
                tiles.Add(empty);
                continue;
            }
            var terrain = (TerrainType)id;
            var image = Art.GetImage($"terrain/{Art.Name(terrain)}");
            if (image == null || image.GetSize() != size)
            {
                image = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
                image.Fill(MapRenderer.TerrainColor(terrain));
            }
            image.Convert(Image.Format.Rgba8);
            var variant = Art.GetImage($"terrain/{Art.Name(terrain)}-2");
            if (variant == null || variant.GetSize() != size)
                variant = image;
            variant.Convert(Image.Format.Rgba8);
            if (retouch != null && terrain != TerrainType.Grass)
            {
                retouch(image);
                if (variant != image)
                    retouch(variant);
            }
            tiles.Add(image);
            tiles.Add(variant);
        }
        var tileArray = new Texture2DArray();
        tileArray.CreateFromImages(tiles);

        // Le terrain de chaque case, la sorte de sa route (le numéro × 64 dans le vert) et sa rivière (de même dans le bleu)
        var cells = Image.CreateEmpty(width, height, false, Image.Format.Rgb8);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var (ground, road, river) = cellAt(new Cell(x, y));
            cells.SetPixel(x, y, new Color((int)ground / 255f, (int)road * 64 / 255f, (int)river * 64 / 255f));
        }

        var material = new ShaderMaterial { Shader = new Shader { Code = Godot.FileAccess.GetFileAsString(ShaderPath) } };
        material.SetShaderParameter("terrain_map", ImageTexture.CreateFromImage(cells));
        // les formes douces des rivières et des ruisseaux
        var field = RiverField.Build(width, height, cell =>
        {
            var (ground, _, river) = cellAt(cell);
            return (ground, river);
        }, size.X, courses);
        material.SetShaderParameter("water_field", ImageTexture.CreateFromImage(field));
        material.SetShaderParameter("tiles", tileArray);
        material.SetShaderParameter("road_tiles", RoadTiles(size));
        material.SetShaderParameter("variants", Variants);
        material.SetShaderParameter("map_cells", new Vector2I(width, height));
        var ranks = new int[10];
        foreach (var terrain in terrains)
            ranks[(int)terrain] = Rank(terrain);
        ranks[(int)DirtId] = 1;
        material.SetShaderParameter("priority", ranks);
        material.SetShaderParameter("water", (int)TerrainType.Water);
        material.SetShaderParameter("sand", (int)TerrainType.Sand);
        material.SetShaderParameter("rock_ground", (int)TerrainType.Rock);
        material.SetShaderParameter("burnt_ground", (int)TerrainType.Burnt);
        material.SetShaderParameter("snow_ground", (int)TerrainType.Snow);
        material.SetShaderParameter("swamp_ground", (int)TerrainType.Swamp);
        material.SetShaderParameter("grass", (int)TerrainType.Grass);
        // la taille d'une case en pixels d'art (celle des images des terrains) ; les longueurs du shader, réglées pour des
        // cases de 24, gardent les mêmes proportions
        material.SetShaderParameter("ART", (float)size.X);
        material.SetShaderParameter("bridge_art", BridgeLayer.ShaderFlags);
        float k = size.X / 24f;
        foreach (var (name, value) in LengthsAt24)
            material.SetShaderParameter(name, value * k);
        ApplyDevelopmentSettings(material);

        return new SmoothGround
        {
            Material = material,
            Size = new Vector2(width, height) * MapRenderer.TileSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
    }
}
