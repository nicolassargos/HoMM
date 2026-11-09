using System;
using System.Collections.Generic;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Une rangée d'onglets en marque-pages (choix de l'utilisateur, 2026-11-09) : chaque onglet est un ruban de soie
/// (<c>art/ui/ribbon.png</c>, généré par PixelLab, crème) teint par le jeu de sa couleur, qui sort du haut de sa page
/// comme le marque-page d'un livre de sorts. Le choisi sort plus haut et s'éclaire ; un clic montre sa page et cache
/// les autres. Les pages sont posées par l'appelant juste sous la rangée (<see cref="PageTop"/>) et ajoutées
/// <b>après</b> elle : les rubans descendent de <see cref="Overlap"/> sur le bord de la page, qui cache le bas de ceux de
/// derrière ; le choisi passe devant. Sans image, le ruban est dessiné (même forme).
/// </summary>
public partial class TabStrip : Control
{
    public TabStrip() { } // pour Godot

    /// <summary>De combien un ruban descend sur le bord de sa page (en unités).</summary>
    public const float Overlap = 4;

    /// <summary>Combien un ruban de derrière sort moins que le choisi (en unités).</summary>
    private const float Behind = 6;

    /// <summary>L'agrandissement du ruban (en pixels de l'image ×2, comme le champ de bataille).</summary>
    private const int RibbonScale = 2;

    /// <summary>La profondeur de l'échancrure en haut du ruban (en pixels de l'image) : le nom s'écrit dessous.</summary>
    private const int Notch = 4;

    /// <summary>La taille du ruban dessiné, faute d'image (en unités).</summary>
    private static readonly Vector2 DrawnSize = new(128, 30);

    /// <summary>Les couleurs des rubans, tour à tour, si l'onglet n'a pas la sienne : ocre, pourpre, bleu nuit, vert.</summary>
    public static readonly Color[] Colors =
    {
        MapRenderer.Rgb(140, 100, 36), MapRenderer.Rgb(118, 34, 46), MapRenderer.Rgb(40, 58, 108), MapRenderer.Rgb(58, 96, 50),
    };

    private readonly List<(Button Button, Control? Page, Color Color)> _tabs = new();
    private readonly int _fontSize;
    private readonly float _gap;
    private readonly Vector2 _tabSize;

    public TabStrip(int fontSize = 14, float gap = 6)
    {
        _fontSize = fontSize;
        _gap = gap;
        _tabSize = Ribbon() is { } ribbon ? ribbon.GetSize() * RibbonScale / Scale - new Vector2(0, Overlap) : DrawnSize;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Où commence la page, depuis le haut de la rangée (en unités).</summary>
    public float PageTop => _tabSize.Y;

    /// <summary>L'onglet choisi (-1 sans onglet).</summary>
    public int Selected { get; private set; } = -1;

    /// <summary>Un autre onglet est choisi (par un clic ou par <see cref="Select"/>).</summary>
    public event Action<int>? Changed;

    /// <summary>Ajoute un onglet ; <paramref name="page"/> (facultative) n'est visible que quand il est choisi.</summary>
    public void AddTab(string title, Control? page = null, Color? color = null)
    {
        int index = _tabs.Count;
        var button = new Button { Text = title, FocusMode = FocusModeEnum.None, ClipText = true };
        SetFontSize(button, _fontSize);
        button.Pressed += () => Select(index);
        button.MouseEntered += () => button.GetNodeOrNull<Control>("Cloth")?.QueueRedraw();
        button.MouseExited += () => button.GetNodeOrNull<Control>("Cloth")?.QueueRedraw();
        AddChild(button);
        _tabs.Add((button, page, color ?? Colors[index % Colors.Length]));
        Size = V(_tabs.Count * (_tabSize.X + _gap) - _gap, _tabSize.Y);
        if (Selected < 0)
            Select(0, notify: false);
        else
            Style(index, false);
        if (page != null)
            page.Visible = index == Selected;
    }

    public void Select(int index, bool notify = true)
    {
        if (index < 0 || index >= _tabs.Count)
            return;
        bool changed = index != Selected;
        Selected = index;
        for (int i = 0; i < _tabs.Count; i++)
        {
            Style(i, i == index);
            if (_tabs[i].Page is { } page)
                page.Visible = i == index;
        }
        if (changed && notify)
            Changed?.Invoke(index);
    }

    // ------------------------------------------------------------------ l'apparence

    private void Style(int index, bool chosen)
    {
        var (button, _, color) = _tabs[index];
        // le ruban de derrière sort moins haut ; tous descendent sur le bord de la page, le choisi devant elle
        float drop = chosen ? 0 : Behind;
        button.Position = V(index * (_tabSize.X + _gap), drop);
        button.Size = V(_tabSize.X, _tabSize.Y + Overlap);
        button.ZIndex = chosen ? 1 : 0;
        // sur la soie teinte, l'or se lirait mal : le choisi en clair, les autres plus doux
        var text = chosen ? TextColor : SoftTextColor;
        button.AddThemeColorOverride("font_color", text);
        button.AddThemeColorOverride("font_pressed_color", text);
        button.AddThemeColorOverride("font_hover_color", chosen ? text : Godot.Colors.White);
        button.AddThemeColorOverride("font_disabled_color", DimColor);

        var tint = chosen ? color.Lightened(0.15f) : color;
        if (Ribbon() is { } ribbon)
        {
            StyleBoxTexture Dyed(Color dye) => new()
            {
                Texture = ribbon, ModulateColor = dye,
                ContentMarginTop = Notch * RibbonScale, ContentMarginBottom = Px(Overlap),
                ContentMarginLeft = Px(6), ContentMarginRight = Px(6),
            };
            var normal = Dyed(chosen ? tint : tint.Darkened(0.25f));
            foreach (var state in new[] { "normal", "pressed", "focus", "disabled" })
                button.AddThemeStyleboxOverride(state, normal);
            button.AddThemeStyleboxOverride("hover", Dyed(chosen ? tint : tint.Darkened(0.05f)));
            return;
        }
        // sans image : le ruban dessiné
        var empty = new StyleBoxEmpty { ContentMarginTop = Px(7), ContentMarginBottom = Px(Overlap), ContentMarginLeft = Px(6), ContentMarginRight = Px(6) };
        foreach (var state in new[] { "normal", "pressed", "focus", "disabled", "hover" })
            button.AddThemeStyleboxOverride(state, empty);
        if (button.GetNodeOrNull<Control>("Cloth") is { } old)
        {
            button.RemoveChild(old);
            old.QueueFree();
        }
        var cloth = new Control { Name = "Cloth", MouseFilter = MouseFilterEnum.Ignore, Size = button.Size, ShowBehindParent = true };
        cloth.Draw += () => DrawRibbon(cloth, button.IsHovered() && !chosen ? tint.Darkened(0.05f) : chosen ? tint : tint.Darkened(0.25f));
        button.AddChild(cloth);
    }

    /// <summary>Le ruban dessiné : le tissu, l'échancrure en queue d'aronde, des bords plus sombres, l'ombre de la page.</summary>
    private static void DrawRibbon(Control cloth, Color fill)
    {
        float w = cloth.Size.X, h = cloth.Size.Y, notch = Px(7), edge = Px(3);
        cloth.DrawColoredPolygon(new[] { new Vector2(0, 0), new Vector2(w / 2, notch), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h) }, fill);
        var dark = fill.Darkened(0.35f);
        cloth.DrawRect(new Rect2(0, 0, edge, h), dark);
        cloth.DrawRect(new Rect2(w - edge, 0, edge, h), dark);
        cloth.DrawPolyline(new[] { new Vector2(0, 0), new Vector2(w / 2, notch), new Vector2(w, 0) }, fill.Darkened(0.5f), Px(1));
        cloth.DrawRect(new Rect2(0, h - Px(Overlap), w, Px(Overlap)), new Color(0, 0, 0, 0.35f));
    }

    // ------------------------------------------------------------------ l'image

    private static Texture2D? _ribbon;
    private static bool _loaded;

    /// <summary>Le ruban, réduit à sa partie visible (null sans image).</summary>
    private static Texture2D? Ribbon()
    {
        if (_loaded)
            return _ribbon;
        _loaded = true;
        if (Art.GetImage("ui/ribbon") is { } image)
            _ribbon = ImageTexture.CreateFromImage(image.GetRegion(image.GetUsedRect()));
        return _ribbon;
    }
}
