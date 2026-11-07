using System;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Les 7 emplacements d'une armée (étape 7) : l'image de chaque troupe et son nombre, ses caractéristiques au survol,
/// et une bordure rouge si elle est trop nombreuse pour le charisme du héros (risque de révolte). Sert au panneau de la
/// carte, à l'écran du héros et à la ville. Avec un <see cref="DragGroup"/>, on glisse les troupes d'un emplacement à
/// l'autre (entre les armées du même groupe) ; Maj en lâchant, ou un clic droit, pour partager une troupe.
/// </summary>
public partial class ArmySlots : HBoxContainer
{
    /// <summary>
    /// Taille d'un emplacement (en pixels de l'écran), et agrandissement de l'image de la créature (son image de la carte,
    /// 24 px) : un nombre entier, pour des pixels nets.
    /// </summary>
    public Vector2 SlotSize { get; set; } = new(32, 44);
    public int ImageScale { get; set; } = 1;
    public int CountFontSize { get; set; } = 12;

    /// <summary>Écrire aussi, sous le nombre, les points de commandement de la troupe (« 30 / 100 pts »).</summary>
    public bool ShowCommandPoints { get; set; }

    /// <summary>
    /// Les armées d'un même groupe s'échangent des troupes en les glissant (la garnison et le héros d'une ville) ;
    /// null : on ne glisse pas.
    /// </summary>
    public object? DragGroup { get; set; }

    public bool CanDrag => DragGroup != null;
    public bool Accepts(ArmySlots source) => DragGroup != null && source.DragGroup == DragGroup;

    /// <summary>Un clic gauche (sans glisser) sur un emplacement (son numéro, de 0 à 6).</summary>
    public event Action<int>? SlotClicked;

    /// <summary>Un clic droit sur une troupe (pour la partager).</summary>
    public event Action<int>? SlotRightClicked;

    /// <summary>
    /// Une troupe a été lâchée sur un de nos emplacements : l'armée d'où elle vient, son numéro, l'emplacement visé,
    /// et si Maj était enfoncée (partager).
    /// </summary>
    public event Action<ArmySlots, int, int, bool>? TroopDropped;

    private string? _key;

    /// <summary>Montre cette armée ; <paramref name="charisma"/> : celui du héros (null pour une garnison, sans limite).</summary>
    public void Show(Army? army, int? charisma)
    {
        var stacks = army?.Stacks ?? new System.Collections.Generic.List<CreatureStack>();
        string key = string.Join(",", stacks.Select(s => $"{s.Type.Id}:{s.Count}")) + $"#{charisma}";
        if (key == _key)
            return;
        _key = key;
        AddThemeConstantOverride("separation", (int)Px(2));
        Clear(this);
        for (int i = 0; i < Army.MaxStacks; i++)
            AddChild(Slot(i < stacks.Count ? stacks[i] : null, charisma, i));
    }

    internal void Drop(ArmySlots source, int from, int to, bool split) => TroopDropped?.Invoke(source, from, to, split);

    private Control Slot(CreatureStack? stack, int? charisma, int index)
    {
        bool risky = stack != null && charisma is { } c && Command.IsOverLimit(stack.Type, stack.Count, c);
        var picture = stack != null ? Art.Get($"creatures/battle/{stack.Type.Id}") ?? Art.Get($"creatures/{stack.Type.Id}") : null;
        var slot = new ArmySlot { Slots = this, Index = index, Picture = picture, CustomMinimumSize = SlotSize, MouseFilter = MouseFilterEnum.Stop };
        slot.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor, risky ? MapRenderer.Rgb(220, 80, 60) : null));
        slot.GuiInput += e =>
        {
            // le clic compte au relâchement, s'il n'a pas servi à glisser la troupe
            if (e is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } && !GetViewport().GuiIsDragging())
                SlotClicked?.Invoke(index);
            else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && stack != null)
                SlotRightClicked?.Invoke(index);
        };
        if (stack == null)
            return slot;
        if (CanDrag)
            slot.MouseDefaultCursorShape = CursorShape.Drag;
        slot.TooltipText = Texts.StackTooltip(stack, charisma);
        float countHeight = Px(CountFontSize + 4), pointsHeight = Px(15);
        float textHeight = countHeight + (ShowCommandPoints ? pointsHeight : 0);
        // l'image de combat si elle tient, sinon la petite image de la carte (agrandies d'un nombre entier de fois)
        if (CreaturePicture(stack.Type, new Rect2(Px(2), Px(2), SlotSize.X - Px(4), SlotSize.Y - textHeight - Px(4))) is { } image)
            slot.AddChild(image);
        // le nombre (et les points de commandement) en bas de l'emplacement
        float bottom = SlotSize.Y - Px(2);
        if (ShowCommandPoints && charisma is { } limit)
        {
            var points = MakeLabel(Texts.CommandPoints(stack, limit), 11, risky ? MapRenderer.Rgb(255, 150, 130) : SoftTextColor);
            points.HorizontalAlignment = HorizontalAlignment.Center;
            points.Position = new Vector2(0, bottom - pointsHeight);
            points.Size = new Vector2(SlotSize.X, pointsHeight);
            points.MouseFilter = MouseFilterEnum.Ignore;
            slot.AddChild(points);
            bottom -= pointsHeight;
        }
        var count = MakeLabel(stack.Count.ToString(), CountFontSize, risky ? MapRenderer.Rgb(255, 150, 130) : TextColor);
        count.HorizontalAlignment = HorizontalAlignment.Center;
        count.Position = new Vector2(0, bottom - countHeight);
        count.Size = new Vector2(SlotSize.X, countHeight);
        count.MouseFilter = MouseFilterEnum.Ignore;
        slot.AddChild(count);
        return slot;
    }
}
