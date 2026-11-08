using System;
using Godot;

namespace HoMM.Game;

/// <summary>Un calque d'ombres (voir <see cref="ShadowLayer"/>) : il dessine ce qu'on lui donne, en pixels nets.</summary>
public partial class ShadowPainter : Node2D
{
    private readonly Action<CanvasItem> _draw;
    public ShadowPainter() => _draw = _ => { };
    public ShadowPainter(Action<CanvasItem> draw) => _draw = draw;
    public override void _Ready() => TextureFilter = TextureFilterEnum.Nearest;
    public override void _Draw() => _draw(this);
}
