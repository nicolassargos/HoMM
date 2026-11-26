using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// La place d'un bâtiment dans la scène d'une ville : le coin haut-gauche de son image, sa profondeur (distance à la
/// caméra : les plus loin dessinés d'abord), et s'il y en a, le coin de son ombre et du décor qui passe devant lui, et
/// des runes qui flottent autour (la guilde des mages : [x, y, n], le coin de chacune et son image rune-&lt;n&gt;.png), et la
/// magie que le jeu anime autour de lui (<see cref="TownSceneMagic"/>). En pixels d'art de la scène.
/// </summary>
public sealed record TownScenePlace(string Id, int X, int Y, double Depth, int? ShadowX = null, int? ShadowY = null,
    int? FrontX = null, int? FrontY = null, List<int[]>? Runes = null, TownSceneMagic? Magic = null);

/// <summary>
/// La magie animée autour d'une tour (la guilde des morts-vivants), en pixels d'art de la scène ; ce qui passe derrière
/// la tour est dessiné avant elle, le reste après.
/// <list type="bullet">
/// <item>Rings : des anneaux de runes qui tournent, [cx, cy, rx, ry, h] (le centre, les deux rayons, la hauteur de la bande).</item>
/// <item>Swirls : des rubans qui montent en spirale, [cx, haut, bas, rayon en haut, rayon en bas].</item>
/// <item>Floats : des images qui flottent sur place (les cristaux), [x, y] et leur image.</item>
/// <item>Orbit, Orbiters : des images qui tournent autour, [cx, cy, rx, ry], et leurs images (livres, crânes).</item>
/// <item>GlowAxis, GlowRings : la magie peinte dans l'image elle-même, animée sur place : une vague de lumière parcourt
/// ses pixels verts, monte en tournant autour de l'axe de la tour (x) et tourne dans les anneaux peints,
/// [cx, cy, rx, ry].</item>
/// </list>
/// </summary>
public sealed record TownSceneMagic(List<int[]>? Rings = null, List<int[]>? Swirls = null, List<TownSceneFloat>? Floats = null,
    int[]? Orbit = null, List<string>? Orbiters = null, int? GlowAxis = null, List<int[]>? GlowRings = null);

/// <summary>Une image qui flotte sur place : son coin et son nom (sans « .png »).</summary>
public sealed record TownSceneFloat(int X, int Y, string Image);

/// <summary>
/// La scène d'une ville vue de face (art/towns/&lt;faction&gt;/scene.json, écrit par tools/art/ville_scene.py) : un fond,
/// et une image par bâtiment, à sa place.
/// </summary>
public sealed class TownScene
{
    private sealed record SceneFile(int Width, int Height, List<TownScenePlace> Buildings, TownScenePlace? Home = null,
        List<int[]>? Smoke = null);

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<TownScenePlace> Places { get; }

    /// <summary>La demeure du héros (son image : demeure.png), montrée tant que la ville n'a pas de fort, ou null.</summary>
    public TownScenePlace? Home { get; }

    /// <summary>
    /// Les cheminées du fond (la forge des morts-vivants, un décor peint dans le fond) : le haut de chacune, [x, y], d'où le
    /// jeu fait monter une fumée animée, derrière tous les bâtiments.
    /// </summary>
    public IReadOnlyList<int[]> Smoke { get; }

    public TownScene(int width, int height, IEnumerable<TownScenePlace> places, TownScenePlace? home = null,
        IEnumerable<int[]>? smoke = null)
    {
        (Width, Height, Places, Home, Smoke) = (width, height, places.ToList(), home, (smoke ?? []).ToList());
        foreach (var twice in Places.GroupBy(p => p.Id).Where(g => g.Count() > 1))
            throw new FormatException($"Scène de ville : le bâtiment {twice.Key} a deux places.");
    }

    public static TownScene FromJson(string json)
    {
        var file = GameData.Parse<SceneFile>(json, "scène de ville");
        return new TownScene(file.Width, file.Height, file.Buildings, file.Home, file.Smoke);
    }

    /// <summary>La demeure du héros est montrée tant qu'aucun bâtiment construit n'a de murs (le fort et ses suites).</summary>
    public bool HomeShown(IEnumerable<BuildingType> built) => Home != null && !built.Any(b => b.Walls > 0);

    public TownScenePlace? Find(string id) => Places.FirstOrDefault(p => p.Id == id);

    /// <summary>
    /// Ce que la scène montre des bâtiments construits, du plus loin au plus proche : ceux qu'un autre remplace ne
    /// sont plus dessinés ; un bâtiment pas encore dessiné prend l'image de celui qu'il remplace (l'hôtel de ville, celle
    /// de la salle du village), sinon il n'est pas montré.
    /// </summary>
    public IEnumerable<(BuildingType Building, TownScenePlace Place)> Shown(IEnumerable<BuildingType> built)
    {
        var all = built.ToList();
        var byId = all.ToDictionary(b => b.Id);
        var replaced = all.Where(b => b.Replaces != null).Select(b => b.Replaces!).ToHashSet();
        var shown = new List<(BuildingType, TownScenePlace)>();
        foreach (var building in all.Where(b => !replaced.Contains(b.Id)))
        {
            for (var drawn = building; drawn != null; drawn = drawn.Replaces is { } id ? byId.GetValueOrDefault(id) : null)
                if (Find(drawn.Id) is { } place)
                {
                    shown.Add((building, place));
                    break;
                }
        }
        return shown.OrderByDescending(s => s.Item2.Depth);
    }
}
