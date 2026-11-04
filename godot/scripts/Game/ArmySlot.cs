using Godot;

namespace HoMM.Game;

/// <summary>
/// Un emplacement d'<see cref="ArmySlots"/> (étape 7) : on peut le glisser sur un autre emplacement du même groupe
/// (la garnison et le héros d'une ville, ou l'armée du héros), comme dans Heroes.
/// </summary>
public partial class ArmySlot : Panel
{
    public ArmySlots Slots { get; init; } = null!;
    public int Index { get; init; }

    /// <summary>L'image montrée sous la souris pendant qu'on glisse (null : un emplacement vide, qu'on ne glisse pas).</summary>
    public Texture2D? Picture { get; init; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Picture == null || !Slots.CanDrag)
            return default;
        var preview = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest, Texture = Picture,
            Size = Picture.GetSize() * Slots.ImageScale, Modulate = new Color(1, 1, 1, 0.8f),
        };
        // l'image centrée sous la souris
        var holder = new Control();
        holder.AddChild(preview);
        preview.Position = -preview.Size / 2;
        SetDragPreview(holder);
        return this;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.Obj is ArmySlot source && source != this && Slots.Accepts(source.Slots);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.Obj is ArmySlot source)
            Slots.Drop(source.Slots, source.Index, Index, Input.IsKeyPressed(Key.Shift));
    }
}
