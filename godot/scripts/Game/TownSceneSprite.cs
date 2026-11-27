using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Un bâtiment dans la scène d'une ville (<see cref="HoMM.Core.TownScene"/>) : son image ×2, qui ne prend la souris que
/// sur ses pixels, avec un léger contour clair au survol (l'image a un pixel transparent de marge pour lui).
/// </summary>
public partial class TownSceneSprite : TextureRect
{
    private const string OutlineShader = @"
shader_type canvas_item;
uniform bool hovered = false;
uniform vec4 outline_color : source_color = vec4(1.0, 0.94, 0.72, 0.85);
// la magie peinte animée sur place : l'axe de la tour et les anneaux peints, en pixels de l'image
uniform bool glow = false;
uniform float glow_axis = 0.0;
uniform vec4 glow_rings[2];
uniform int glow_ring_count = 0;
void fragment() {
    vec4 color = texture(TEXTURE, UV);
    if (glow && color.a > 0.5 && color.g > color.r + 0.137 && color.g > color.b + 0.078) {
        vec2 p = floor(UV / TEXTURE_PIXEL_SIZE);
        // dans un anneau, la lumière tourne ; ailleurs (les rubans), elle monte en tournant autour de la tour ;
        // des tours de 2 et 3 secondes : tout revient au même instant à chaque tour de l'orbite (TownMagic, 24 secondes)
        float wave = sin(p.y * 0.28 + atan(1.0, (p.x - glow_axis) / 18.0) * 2.0 + TIME * PI);
        for (int i = 0; i < glow_ring_count; i++) {
            vec4 r = glow_rings[i];
            vec2 q = (p - r.xy) / (r.zw + vec2(3.0, 5.0));
            float d = dot(q, q);
            if (d > 0.35 && d < 1.4)
                wave = sin(atan((p.y - r.y) / r.w, (p.x - r.x) / r.z) * 3.0 - TIME * TAU / 3.0);
        }
        color.rgb = min(color.rgb * (0.72 + 0.5 * pow(clamp(wave, 0.0, 1.0), 2.0)), vec3(1.0));
    }
    if (hovered && color.a < 0.5) {
        vec2 px = TEXTURE_PIXEL_SIZE;
        float around = texture(TEXTURE, UV + vec2(px.x, 0.0)).a + texture(TEXTURE, UV - vec2(px.x, 0.0)).a
            + texture(TEXTURE, UV + vec2(0.0, px.y)).a + texture(TEXTURE, UV - vec2(0.0, px.y)).a;
        if (around > 0.5)
            color = outline_color;
    }
    COLOR = color;
}";
    private static Shader? _shader;

    private readonly Image _image;

    public TownSceneSprite(Image image)
    {
        _image = image;
        Texture = ImageTexture.CreateFromImage(image);
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.Scale;
        TextureFilter = TextureFilterEnum.Nearest;
        Size = new Vector2(image.GetWidth(), image.GetHeight()) * ArtScale;
        PivotOffset = new Vector2(Size.X / 2, Size.Y); // pour monter du sol quand il vient d'être bâti
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        Material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = OutlineShader } };
        MouseEntered += () => ((ShaderMaterial)Material).SetShaderParameter("hovered", true);
        MouseExited += () => ((ShaderMaterial)Material).SetShaderParameter("hovered", false);
    }

    /// <summary>
    /// Anime la magie peinte dans l'image (une vague de lumière dans ses pixels verts) : l'axe de la tour et les anneaux
    /// peints ([cx, cy, rx, ry]), en pixels de la scène ; <paramref name="corner"/> : le coin de l'image dans la scène.
    /// </summary>
    public void Glow(Vector2 corner, int axis, IReadOnlyList<int[]> rings)
    {
        var material = (ShaderMaterial)Material;
        material.SetShaderParameter("glow", true);
        material.SetShaderParameter("glow_axis", axis - corner.X);
        material.SetShaderParameter("glow_rings", rings.Take(2)
            .Select(r => new Vector4(r[0] - corner.X, r[1] - corner.Y, r[2], r[3])).ToArray());
        material.SetShaderParameter("glow_ring_count", Math.Min(rings.Count, 2));
    }

    /// <summary>La souris n'est sur le bâtiment que sur un pixel dessiné.</summary>
    public override bool _HasPoint(Vector2 point)
    {
        int x = (int)(point.X / ArtScale), y = (int)(point.Y / ArtScale);
        return x >= 0 && y >= 0 && x < _image.GetWidth() && y < _image.GetHeight() && _image.GetPixel(x, y).A > 0.5f;
    }
}
