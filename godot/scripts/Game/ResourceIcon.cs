using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>Petite icône de ressource pour l'interface.</summary>
public partial class ResourceIcon : Control
{
    public ResourceType Resource { get; set; }

    public override void _Draw() => MapRenderer.DrawResourceIcon(this, Resource, Size / 2);
}
