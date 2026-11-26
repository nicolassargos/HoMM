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

    /// <summary>
    /// Le nombre écrit par-dessus l'image, en bas à droite (la ville, choix de l'utilisateur, 2026-11-21) : l'image prend
    /// tout l'emplacement.
    /// </summary>
    public bool CountOverlay { get; set; }

    /// <summary>Écrire aussi, sous le nombre, les points de commandement de la troupe (« 30 / 100 pts »).</summary>
    public bool ShowCommandPoints { get; set; }

    /// <summary>Derrière chaque créature, le paysage de sa faction (<c>art/ui/fonds-troupes/&lt;faction&gt;.png</c>, s'il existe).</summary>
    public bool Backdrops { get; set; }

    /// <summary>Les pieds de toutes les créatures sur une même ligne, un peu au-dessus du bas des emplacements.</summary>
    public bool AlignFeet { get; set; }

    /// <summary>Chaque emplacement dans le cadre mince de bois et d'or (comme Heroes 2), le paysage et la créature dedans.</summary>
    public bool Framed { get; set; }

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
        slot.AddThemeStyleboxOverride("panel", Framed && !risky ? ThinFrame(DarkPanelColor)
            : Inset(DarkPanelColor, risky ? MapRenderer.Rgb(220, 80, 60) : null));
        // l'épaisseur du bord, que le paysage et la créature ne couvrent pas
        float rim = Framed ? ThinFrameBorder() : 2;
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
        // derrière la créature, le paysage de sa faction, ×2, posé en bas de l'emplacement (comme Heroes 2)
        if (Backdrops && Art.Get($"ui/fonds-troupes/{stack.Type.Faction}") is { } backdrop)
        {
            var inside = new Control
            {
                Position = new Vector2(rim, rim), Size = SlotSize - new Vector2(2 * rim, 2 * rim), ClipContents = true,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            var size = backdrop.GetSize() * 2;
            inside.AddChild(Picture(backdrop, new Vector2(MathF.Floor((inside.Size.X - size.X) / 2), inside.Size.Y - size.Y), size));
            slot.AddChild(inside);
        }
        if (CanDrag)
            slot.MouseDefaultCursorShape = CursorShape.Drag;
        slot.TooltipText = _leader != null
            ? Texts.StackTooltip(stack, commandLimit, Morale.Of(_leader, stack), Morale.LuckOf(_leader).Total)
            : Texts.StackTooltip(stack, commandLimit);
        // la hauteur des lignes : celle du texte (une taille commune, voir Ui.TextSize)
        float countHeight = TextSize(CountFontSize), pointsHeight = TextSize(11);
        float textHeight = CountOverlay ? 0 : countHeight + (ShowCommandPoints ? pointsHeight : 0);
        float edge = Framed ? rim : Px(2);
        var place = new Rect2(edge, edge, SlotSize.X - 2 * edge, SlotSize.Y - textHeight - 2 * edge);
        if (!Compact && CreaturePicture(stack.Type, place, AlignFeet ? place.Size.Y - Px(10) : null) is { } image)
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
        float bottom = SlotSize.Y - (Framed ? rim : Px(2));
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
        count.HorizontalAlignment = CountOverlay ? HorizontalAlignment.Right : HorizontalAlignment.Center;
        count.Position = new Vector2(0, bottom - countHeight);
        count.Size = new Vector2(SlotSize.X - (CountOverlay ? Px(6) + (Framed ? rim : 0) : 0), countHeight);
        if (CountOverlay)
        {
            // un contour noir : le nombre se lit sur l'image
            count.AddThemeConstantOverride("outline_size", (int)Px(4));
            count.AddThemeColorOverride("font_outline_color", Colors.Black);
        }
        count.MouseFilter = MouseFilterEnum.Ignore;
        slot.AddChild(count);
        return slot;
    }
}
