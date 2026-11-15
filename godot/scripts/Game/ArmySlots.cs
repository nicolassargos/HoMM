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
    /// Taille d'un emplacement (en pixels de l'écran). Des emplacements étroits (<see cref="Compact"/>, le panneau de la
    /// carte) montrent la petite image de la créature ; les autres, son image de combat ×2 (voir Ui.CreaturePicture).
    /// </summary>
    public Vector2 SlotSize { get; set; } = new(32, 44);
    public bool Compact { get; set; }
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
    private Hero? _leader;

    /// <summary>Montre cette armée ; <paramref name="commandLimit"/> : les points de commandement par troupe du héros
    /// (<see cref="Hero.CommandLimit"/> ; null pour une garnison, sans limite). <paramref name="leader"/> : le héros qui
    /// la mène, pour montrer le moral et la chance de chaque troupe.</summary>
    public void Show(Army? army, int? commandLimit, Hero? leader = null)
    {
        var stacks = army?.Stacks ?? new System.Collections.Generic.List<CreatureStack>();
        string key = string.Join(",", stacks.Select(s => $"{s.Type.Id}:{s.Count}")) + $"#{commandLimit}"
                     + (leader != null ? $"#{Morale.OfArmy(leader)}#{Morale.LuckOf(leader)}" : "");
        if (key == _key)
            return;
        _key = key;
        _leader = leader;
        AddThemeConstantOverride("separation", (int)Px(2));
        Clear(this);
        for (int i = 0; i < Army.MaxStacks; i++)
            AddChild(Slot(i < stacks.Count ? stacks[i] : null, commandLimit, i));
    }

    internal void Drop(ArmySlots source, int from, int to, bool split) => TroopDropped?.Invoke(source, from, to, split);

    private Control Slot(CreatureStack? stack, int? commandLimit, int index)
    {
        bool risky = stack != null && commandLimit is { } c && Command.Cost(stack.Type, stack.Count) > c;
        // l'image qui suit la souris quand on glisse la troupe : celle de l'emplacement, entière
        var small = stack != null ? Art.Get($"creatures/{stack.Type.Id}") : null;
        var battle = stack != null && !Compact ? Art.Get($"creatures/battle/{stack.Type.Id}") : null;
        var picture = battle ?? small;
        var pictureSize = battle != null ? battle.GetSize() * CreatureArtScale : small != null ? small.GetSize() * 2 : Vector2.Zero;
        var slot = new ArmySlot
        {
            Slots = this, Index = index, Picture = picture, PictureSize = pictureSize, CustomMinimumSize = SlotSize,
            MouseFilter = MouseFilterEnum.Stop,
        };
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
        slot.TooltipText = _leader != null
            ? Texts.StackTooltip(stack, commandLimit, Morale.Of(_leader, stack), Morale.LuckOf(_leader).Total)
            : Texts.StackTooltip(stack, commandLimit);
        // la hauteur des lignes : celle du texte (une taille commune, voir Ui.TextSize)
        float countHeight = TextSize(CountFontSize), pointsHeight = TextSize(11);
        float textHeight = countHeight + (ShowCommandPoints ? pointsHeight : 0);
        var place = new Rect2(Px(2), Px(2), SlotSize.X - Px(4), SlotSize.Y - textHeight - Px(4));
        if (!Compact && CreaturePicture(stack.Type, place) is { } image)
            slot.AddChild(image);
        else if (Compact)
        {
            // toute la largeur de l'emplacement : la petite image entière, ×1 avec les images de 32 (choix de l'utilisateur,
            // 2026-11-08 : plutôt que ×2 coupée)
            var full = new Rect2(0, Px(1), SlotSize.X, SlotSize.Y - textHeight - Px(1));
            if (SmallCreatureArt(stack.Type, full.Size) is { } art)
                slot.AddChild(Picture(art.Texture, full.Position + ((full.Size - art.Size) / 2).Floor(), art.Size));
        }
        // le nombre (et les points de commandement) en bas de l'emplacement
        float bottom = SlotSize.Y - Px(2);
        if (ShowCommandPoints && commandLimit is { } limit)
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
