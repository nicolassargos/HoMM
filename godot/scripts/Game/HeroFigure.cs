using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le héros en pied, dans l'onglet Équipement de l'écran du héros : ses 11 emplacements en deux colonnes (six à sa gauche,
/// cinq à sa droite), chacun relié par un trait à l'endroit du corps où il se porte. En attendant l'image en pied de
/// chaque héros (session images), une silhouette dessinée. Les emplacements eux-mêmes sont des enfants posés par
/// <see cref="HeroScreen"/> aux places de <see cref="SlotPosition"/>.
/// </summary>
public partial class HeroFigure : Control
{
    /// <summary>La taille d'un emplacement (et d'une place du coffre) : un artefact de 32 pixels, ×3.</summary>
    public const float SlotSize = 64;

    /// <summary>La taille de la figure entière, emplacements compris (en unités de l'interface).</summary>
    public static readonly Vector2 FigureSize = new(386, 404);

    private const int LeftCount = 6;

    /// <summary>Le héros en pied, s'il a son image (sinon, la silhouette).</summary>
    public Texture2D? Picture { get; set; }

    /// <summary>La place de l'emplacement <paramref name="index"/> (dans l'ordre de <see cref="Equipment.Slots"/>), en unités.</summary>
    public static Vector2 SlotPosition(int index) => index < LeftCount
        ? new Vector2(0, index * 68)
        : new Vector2(FigureSize.X - SlotSize, (index - LeftCount) * 85);

    // La silhouette, dessinée dans un repère de 300 × 262 (celui de la maquette), puis agrandie autour du milieu.
    private const float BodyScale = 1.55f, MockCenter = 150, MockTop = 6;

    private static Vector2 Mock(float x, float y) => V(FigureSize.X / 2 + (x - MockCenter) * BodyScale, (y - MockTop) * BodyScale);

    /// <summary>L'endroit du corps où se porte chaque emplacement (même ordre que <see cref="Equipment.Slots"/>).</summary>
    private static readonly (float X, float Y)[] Anchors =
    {
        (140, 28),  // tête
        (128, 58),  // épaules : la cape
        (120, 126), // main de l'arme
        (120, 136), // anneau : la main de l'arme
        (140, 124), // ceinture
        (140, 244), // pieds
        (156, 48),  // cou
        (160, 82),  // torse
        (180, 126), // main du bouclier
        (180, 136), // anneau : la main du bouclier
        (168, 130), // divers : la sacoche
    };

    public override void _Draw()
    {
        var body = MapRenderer.Rgb(75, 64, 54);
        if (Picture is { } picture)
        {
            // l'image en pied, au milieu, à un nombre entier de fois sa taille
            var room = V(FigureSize.X - 2 * SlotSize - 40, FigureSize.Y);
            int times = Mathf.Max(1, (int)Mathf.Min(room.X / picture.GetWidth(), room.Y / picture.GetHeight()));
            var size = picture.GetSize() * times;
            DrawTextureRect(picture, new Rect2(new Vector2(V(FigureSize.X, 0).X / 2 - size.X / 2, room.Y - size.Y), size), false);
        }
        else
        {
            DrawCircle(Mock(150, 30), Px(15 * BodyScale), body);
            foreach (var (x, y, w, h) in new (float, float, float, float)[]
                     {
                         (131, 47, 38, 82), (113, 50, 14, 80), (173, 50, 14, 80), (133, 128, 15, 118), (152, 128, 15, 118),
                     })
                DrawRect(new Rect2(Mock(x, y), Mock(x + w, y + h) - Mock(x, y)), body);
        }

        // les traits : du bord de l'emplacement, un pas à l'horizontale, puis jusqu'à l'endroit du corps
        var line = new Color(SilverColor, 0.75f);
        var dot = MapRenderer.Rgb(214, 168, 74);
        for (int i = 0; i < Equipment.Slots.Length; i++)
        {
            bool left = i < LeftCount;
            var slot = SlotPosition(i);
            var start = V(left ? slot.X + SlotSize : slot.X, slot.Y + SlotSize / 2);
            var elbow = start + V(left ? 20 : -20, 0);
            var anchor = Mock(Anchors[i].X, Anchors[i].Y);
            DrawPolyline(new[] { start, elbow, anchor }, line, Px(1));
            DrawCircle(anchor, Px(3), dot);
        }
    }
}
