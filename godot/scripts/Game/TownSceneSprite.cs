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
void fragment() {
    vec4 color = texture(TEXTURE, UV);
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

    /// <summary>La souris n'est sur le bâtiment que sur un pixel dessiné.</summary>
    public override bool _HasPoint(Vector2 point)
    {
        int x = (int)(point.X / ArtScale), y = (int)(point.Y / ArtScale);
        return x >= 0 && y >= 0 && x < _image.GetWidth() && y < _image.GetHeight() && _image.GetPixel(x, y).A > 0.5f;
    }
}
