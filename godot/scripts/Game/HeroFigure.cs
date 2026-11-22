using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le héros en pied, dans l'onglet Équipement de l'écran du héros : ses 11 emplacements, deux par deux sur trois rangées
/// (six à sa gauche, cinq à sa droite ; maquette choisie par l'utilisateur, 2026-11-21), chacun relié par un trait à l'endroit du corps où il se porte. En attendant l'image en pied de
/// chaque héros (session images), une silhouette dessinée. Les emplacements eux-mêmes sont des enfants posés par
/// <see cref="HeroScreen"/> aux places de <see cref="SlotPosition"/>.
/// </summary>
public partial class HeroFigure : Control
{
    /// <summary>
    /// La taille d'un emplacement (et d'une place du coffre), en unités : la tuile d'un artefact (58 pixels, l'objet et son
    /// bord) ×2, 116 pixels (la règle du pixel art ×2), et son cadre.
    /// </summary>
    public const float SlotSize = 78;

    /// <summary>L'écart entre deux emplacements côte à côte (en unités).</summary>
    public const float SlotGap = 6;

    /// <summary>La taille de la figure entière, emplacements compris (en unités de l'interface).</summary>
    public static readonly Vector2 FigureSize = new(450, 404);

    /// <summary>La largeur d'une paire d'emplacements, de chaque côté du héros (en unités).</summary>
    private const float PairWidth = 2 * SlotSize + SlotGap;

    private const int LeftCount = 6;

    /// <summary>Le héros en pied, s'il a son image (sinon, la silhouette).</summary>
    public Texture2D? Picture { get; set; }

    /// <summary>
    /// La place de l'emplacement <paramref name="index"/> (dans l'ordre de <see cref="Equipment.Slots"/>), en unités : deux
    /// par rangée ; à gauche, le premier dehors et le second contre le héros ; à droite, l'inverse.
    /// </summary>
    public static Vector2 SlotPosition(int index)
    {
        bool left = index < LeftCount;
        int n = left ? index : index - LeftCount;
        float y = n / 2 * (FigureSize.Y - SlotSize) / 2;
        float x = left ? n % 2 * (SlotSize + SlotGap) : FigureSize.X - PairWidth + n % 2 * (SlotSize + SlotGap);
        return new Vector2(x, y);
    }

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
            var room = V(FigureSize.X - 2 * PairWidth - 40, FigureSize.Y);
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

        // les traits : du bord de l'emplacement, à l'horizontale jusqu'à un pas après la paire (sous l'emplacement voisin),
        // puis jusqu'à l'endroit du corps
        var line = new Color(SilverColor, 0.75f);
        var dot = MapRenderer.Rgb(214, 168, 74);
        for (int i = 0; i < Equipment.Slots.Length; i++)
        {
            bool left = i < LeftCount;
            var slot = SlotPosition(i);
            var start = V(left ? slot.X + SlotSize : slot.X, slot.Y + SlotSize / 2);
            var elbow = V(left ? PairWidth + 20 : FigureSize.X - PairWidth - 20, slot.Y + SlotSize / 2);
            var anchor = Mock(Anchors[i].X, Anchors[i].Y);
            DrawPolyline(new[] { start, elbow, anchor }, line, Px(1));
            DrawCircle(anchor, Px(3), dot);
        }
    }
}
