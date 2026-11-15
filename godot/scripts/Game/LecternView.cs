using System;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le livre fermé du héros posé sur un lutrin, en perspective (l'onglet Magie de l'écran du héros, choix de l'utilisateur,
/// 2026-11-11) : il luit doucement, quelques étincelles montent ; au survol la lueur grandit ; un clic l'ouvre.
/// <para>Maquette : dessiné par le code en attendant l'image (le livre fermé sur son lutrin), même taille.</para>
/// </summary>
public partial class LecternView : Control
{
    /// <summary>La place du lutrin et du livre, en pixels d'art.</summary>
    public const int WidthArt = 160, HeightArt = 176;

    public event Action? Opened;

    /// <summary>Dessiner le lutrin ? Non quand l'image de la scène le contient déjà (même point de vue, même lumière : choix
    /// de l'utilisateur, 2026-11-12) : il ne reste alors que la lueur, les étincelles et le clic.</summary>
    public bool ShowImage { get; init; } = true;

    /// <summary>La couleur de la lueur et des étincelles : la lumière de la scène.</summary>
    public Color GlowColor { get; init; } = Glow;

    private static readonly Color Wood = MapRenderer.Rgb(104, 66, 38), WoodDark = MapRenderer.Rgb(62, 38, 22),
        WoodLight = MapRenderer.Rgb(140, 94, 56), Leather = MapRenderer.Rgb(96, 44, 30), LeatherDark = MapRenderer.Rgb(52, 22, 16),
        Pages = MapRenderer.Rgb(214, 196, 150), Gold = MapRenderer.Rgb(196, 150, 60), Glow = MapRenderer.Rgb(255, 226, 140);

    private bool _hover;
    private double _time;
    private readonly Random _random = new(3);
    private readonly System.Collections.Generic.List<(Vector2 At, float Age, float Life)> _sparks = new();

    public LecternView()
    {
        CustomMinimumSize = Size = new Vector2(ArtPx(WidthArt), ArtPx(HeightArt));
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += () => _hover = true;
        MouseExited += () => _hover = false;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            Open();
    }

    /// <summary>Ouvre le livre (comme un clic).</summary>
    public void Open() => Opened?.Invoke();

    public override void _Process(double delta)
    {
        _time += delta;
        // de temps en temps, une étincelle monte du livre (plus souvent au survol)
        if (_random.NextDouble() < (_hover ? 0.25 : 0.06))
            _sparks.Add((new Vector2(ArtPx(56 + (float)_random.NextDouble() * 48), ArtPx(34)), 0, 0.8f + (float)_random.NextDouble() * 0.6f));
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Age += (float)delta;
            if (s.Age >= s.Life)
                _sparks.RemoveAt(i);
            else
                _sparks[i] = s;
        }
        QueueRedraw();
    }

    private static Vector2 A(float x, float y) => new(ArtPx(x), ArtPx(y));

    /// <summary>L'image du lutrin et de son livre (<c>art/ui/lutrin.png</c>), dessinée à la place de la maquette.</summary>
    private Texture2D? _image;

    public override void _Ready() => _image = ShowImage ? Art.Get("ui/lutrin") : null;

    public override void _Draw()
    {
        // la lueur autour du livre : des ovales de lumière posés les uns sur les autres (plus clairs au centre), qui battent ;
        // plus forte au survol
        float pulse = (_hover ? 0.16f : 0.08f) * (0.8f + 0.2f * MathF.Sin((float)_time * 2.5f));
        for (int ring = 0; ring < 4; ring++)
            DrawOval(A(80, 52), ArtPx(30 + ring * 12), ArtPx(20 + ring * 8), new Color(GlowColor, pulse));
        if (!ShowImage)
        {
            DrawSparks();
            return;
        }
        if (_image != null)
        {
            var size = _image.GetSize() * ArtScale;
            DrawTextureRect(_image, new Rect2(new Vector2(SnapDown((Size.X - size.X) / 2), Size.Y - size.Y), size), false);
            DrawSparks();
            return;
        }
        // le pied du lutrin : un poteau et un socle
        DrawRect(new Rect2(A(72, 84), A(16, 72)), Wood);
        DrawRect(new Rect2(A(72, 84), A(4, 72)), WoodLight);
        DrawRect(new Rect2(A(84, 84), A(4, 72)), WoodDark);
        DrawRect(new Rect2(A(44, 156), A(72, 12)), Wood);
        DrawRect(new Rect2(A(44, 156), A(72, 4)), WoodLight);
        DrawRect(new Rect2(A(40, 168), A(80, 6)), WoodDark);
        // le pupitre penché : un trapèze vu d'en haut (plus étroit au fond)
        var desk = new[] { A(30, 56), A(130, 56), A(144, 88), A(16, 88) };
        DrawColoredPolygon(desk, Wood);
        DrawColoredPolygon(new[] { A(16, 88), A(144, 88), A(144, 94), A(16, 94) }, WoodDark);
        // le livre fermé, couché sur le pupitre : la tranche des pages, puis la couverture et son fer doré
        DrawColoredPolygon(new[] { A(38, 50), A(122, 50), A(132, 78), A(28, 78), A(28, 82), A(132, 82) }, Pages);
        DrawColoredPolygon(new[] { A(28, 78), A(132, 78), A(132, 84), A(28, 84) }, Pages.Darkened(0.2f));
        var cover = new[] { A(38, 44), A(122, 44), A(134, 78), A(26, 78) };
        DrawColoredPolygon(cover, Leather);
        DrawPolyline(new[] { cover[0], cover[1], cover[2], cover[3], cover[0] }, LeatherDark, Grid);
        var mark = new[] { A(70, 52), A(90, 52), A(94, 70), A(66, 70) };
        DrawColoredPolygon(mark, Gold.Darkened(0.25f));
        DrawPolyline(new[] { mark[0], mark[1], mark[2], mark[3], mark[0] }, Gold, Grid);
        DrawSparks();
    }

    /// <summary>Un ovale plein, en bandes d'un pixel d'art (net, sur la grille).</summary>
    private void DrawOval(Vector2 center, float rx, float ry, Color color)
    {
        for (float y = -ry; y < ry; y += Grid)
        {
            float v = (y + Grid / 2f) / ry;
            float half = SnapDown(rx * MathF.Sqrt(Math.Max(0, 1 - v * v)));
            if (half > 0)
                DrawRect(new Rect2(center.X - half, center.Y + y, 2 * half, Grid), color);
        }
    }

    /// <summary>Les étincelles qui montent du livre et pâlissent.</summary>
    private void DrawSparks()
    {
        foreach (var s in _sparks)
        {
            var at = s.At - new Vector2(0, ArtPx(36) * s.Age / s.Life);
            DrawRect(new Rect2(new Vector2(SnapDown(at.X), SnapDown(at.Y)), new Vector2(Grid, Grid)), new Color(GlowColor, 1 - s.Age / s.Life));
        }
    }
}
