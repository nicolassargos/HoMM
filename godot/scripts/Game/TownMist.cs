using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Un banc de brume dans la scène d'une ville (<see cref="HoMM.Core.TownScene.Mist"/>) : deux voiles qui glissent vers
/// la droite à des vitesses différentes, plus épais près du sol, en quelques paliers de transparence posés sur la
/// grille des pixels d'art. Les voiles se répètent tous les 96 et 72 pixels et les parcourent en 24 secondes : la brume
/// revient au même instant à chaque tour, comme le reste de la scène ; une épaisseur qui ne bouge pas, tirée sur toute
/// la largeur, cache leur répétition.
/// </summary>
public partial class TownMist : ColorRect
{
    private const string MistShader = @"
shader_type canvas_item;
uniform vec2 size;          // en pixels d'art
uniform float seed = 0.0;
uniform vec4 tint : source_color = vec4(0.66, 0.7, 0.84, 1.0);
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
// un bruit doux qui se répète toutes les « period » cases en x
float noise(vec2 p, float period) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float x0 = mod(i.x, period), x1 = mod(i.x + 1.0, period);
    return mix(mix(hash(vec2(x0, i.y)), hash(vec2(x1, i.y)), f.x),
               mix(hash(vec2(x0, i.y + 1.0)), hash(vec2(x1, i.y + 1.0)), f.x), f.y);
}
void fragment() {
    vec2 p = floor(UV * size);
    float t = mod(TIME, 24.0);
    // deux voiles : des cases de 24 et 18 pixels, 4 cases de période ; 4 et 3 pixels par seconde
    float a = noise(vec2((p.x - t * 4.0) / 24.0, p.y / 7.0 + seed), 4.0);
    float b = noise(vec2((p.x - t * 3.0) / 18.0, p.y / 5.0 + seed + 9.0), 4.0);
    float thick = 0.45 + 0.75 * noise(vec2(p.x / 70.0 + seed * 3.0, 0.5), 1000.0);
    float y = p.y / size.y, x = p.x / size.x;
    // plus épaisse en bas (le sol), qui s'efface vers le haut et aux deux bouts
    float shape = smoothstep(0.0, 0.75, y) * (1.0 - smoothstep(0.88, 1.0, y))
        * smoothstep(0.0, 0.06, x) * (1.0 - smoothstep(0.94, 1.0, x));
    float d = (a * 0.6 + b * 0.4) * thick * shape;
    float alpha = d > 0.5 ? 0.3 : d > 0.38 ? 0.19 : d > 0.27 ? 0.09 : 0.0;
    COLOR = vec4(tint.rgb, alpha);
}";

    private static Shader? _shader;

    /// <param name="mist">[x, y, largeur, hauteur, devant], en pixels d'art</param>
    public TownMist(int[] mist, int seed)
    {
        _shader ??= new Shader { Code = MistShader };
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("size", new Vector2(mist[2], mist[3]));
        material.SetShaderParameter("seed", seed * 3.7f);
        Material = material;
        Position = new Vector2(mist[0], mist[1]) * ArtScale;
        Size = new Vector2(mist[2], mist[3]) * ArtScale;
        MouseFilter = MouseFilterEnum.Ignore;
    }
}
