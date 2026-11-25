using System;
using Godot;

namespace HoMM.Game;

/// <summary>
/// Une pièce de la carte triée avec les autres (2026-11-25) : un objet ou un héros, posé à son pied (sa position) dans
/// un calque trié de haut en bas avec le décor (<see cref="ActorLayer"/>, <see cref="DecorLayer"/>). Ce qui est plus bas
/// à l'écran recouvre ce qui est plus haut. Elle se dessine à chaque image, en coordonnées de la carte.
/// </summary>
public partial class MapPiece : Node2D
{
    /// <summary>Dessine la pièce, en coordonnées de la carte (la pièce s'est décalée de sa position).</summary>
    public Action<CanvasItem> Paint { get; set; } = _ => { };

    public override void _Ready() => TextureFilter = TextureFilterEnum.Nearest;

    public override void _Draw()
    {
        DrawSetTransform(-Position);
        Paint(this);
    }
}
