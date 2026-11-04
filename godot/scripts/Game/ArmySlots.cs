using System;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Les 7 emplacements d'une armée (étape 7) : l'image de chaque troupe et son nombre, ses caractéristiques au survol,
/// et une bordure rouge si elle est trop nombreuse pour le charisme du héros (risque de révolte). Servira aussi à
/// l'écran du héros et à la ville.
/// </summary>
public partial class ArmySlots : HBoxContainer
{
    /// <summary>Taille d'un emplacement, et agrandissement de l'image de la créature (son image de la carte, 24 px).</summary>
    public Vector2 SlotSize { get; set; } = new(32, 44);
    public int ImageScale { get; set; } = 1;
    public int CountFontSize { get; set; } = 12;

    /// <summary>Le joueur a cliqué sur un emplacement (son numéro, de 0 à 6).</summary>
    public event Action<int>? SlotClicked;

    private string? _key;

    /// <summary>Montre cette armée ; <paramref name="charisma"/> : celui du héros (null pour une garnison, sans limite).</summary>
    public void Show(Army? army, int? charisma)
    {
        var stacks = army?.Stacks ?? new System.Collections.Generic.List<CreatureStack>();
        string key = string.Join(",", stacks.Select(s => $"{s.Type.Id}:{s.Count}")) + $"#{charisma}";
        if (key == _key)
            return;
        _key = key;
        AddThemeConstantOverride("separation", 2);
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        for (int i = 0; i < Army.MaxStacks; i++)
            AddChild(Slot(i < stacks.Count ? stacks[i] : null, charisma, i));
    }

    private Control Slot(CreatureStack? stack, int? charisma, int index)
    {
        bool risky = stack != null && charisma is { } c && Command.IsOverLimit(stack.Type, stack.Count, c);
        var slot = new Panel { CustomMinimumSize = SlotSize, MouseFilter = MouseFilterEnum.Stop };
        slot.AddThemeStyleboxOverride("panel", Box(DarkPanelColor, risky ? MapRenderer.Rgb(220, 80, 60) : DimColor, 1, 1, 1, 1));
        slot.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                SlotClicked?.Invoke(index);
        };
        if (stack == null)
            return slot;
        slot.TooltipText = Texts.StackTooltip(stack, charisma);
        if (CreatureImage(stack.Type, ImageScale) is { } image)
        {
            var size = image.CustomMinimumSize;
            image.Position = new Vector2((SlotSize.X - size.X) / 2, Math.Max(1, (SlotSize.Y - 14 - size.Y) / 2));
            image.Size = size;
            slot.AddChild(image);
        }
        var count = MakeLabel(stack.Count.ToString(), CountFontSize, risky ? MapRenderer.Rgb(255, 150, 130) : TextColor);
        count.HorizontalAlignment = HorizontalAlignment.Center;
        count.Position = new Vector2(0, SlotSize.Y - 16);
        count.Size = new Vector2(SlotSize.X, 16);
        count.MouseFilter = MouseFilterEnum.Ignore;
        slot.AddChild(count);
        return slot;
    }
}
