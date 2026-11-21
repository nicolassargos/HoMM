using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>Petite icône de ressource pour l'interface.</summary>
public partial class ResourceIcon : Control
{
    public ResourceType Resource { get; set; }

    public override void _Ready() => TextureFilter = TextureFilterEnum.Nearest;

    /// <summary>L'image de 32 pixels (<c>icons/&lt;ressource&gt;-grand</c>), ×2 si la place le permet.</summary>
    public bool Large { get; set; }

    public override void _Draw()
    {
        if (Large && Art.Get($"icons/{Art.Name(Resource)}-grand") is { } image)
            Art.DrawSprite(this, image, new Rect2(Vector2.Zero, Size));
        else
            MapRenderer.DrawResourceIcon(this, Resource, Size / 2, Ui.Px(7));
    }
}
