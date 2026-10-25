using Godot;

namespace HoMM.Game;

/// <summary>Couleurs et petits outils communs à toute l'interface (panneau, écran de ville…).</summary>
public static class Ui
{
    public static readonly Color PanelColor = MapRenderer.Rgb(40, 32, 24);
    public static readonly Color DarkPanelColor = MapRenderer.Rgb(28, 22, 16);
    public static readonly Color BorderColor = MapRenderer.Rgb(190, 160, 90);
    public static readonly Color TextColor = MapRenderer.Rgb(240, 228, 200);
    public static readonly Color SoftTextColor = MapRenderer.Rgb(180, 168, 140);
    public static readonly Color GoodColor = MapRenderer.Rgb(90, 190, 90);
    public static readonly Color DimColor = MapRenderer.Rgb(110, 100, 85);

    public static T At<T>(T control, float x, float y) where T : Control
    {
        control.Position = new Vector2(x, y);
        return control;
    }

    public static Label MakeLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    public static StyleBoxFlat Box(Color background, Color? border,
        int left = 0, int top = 0, int right = 0, int bottom = 0, int radius = 0)
    {
        var box = new StyleBoxFlat { BgColor = background };
        if (border is { } b)
        {
            box.BorderColor = b;
            box.BorderWidthLeft = left;
            box.BorderWidthTop = top;
            box.BorderWidthRight = right;
            box.BorderWidthBottom = bottom;
        }
        box.SetCornerRadiusAll(radius);
        return box;
    }

    /// <summary>Un bouton aux couleurs du jeu. Il ne prend pas le focus, sinon Espace/Entrée le « cliqueraient ».</summary>
    public static Button MakeButton(string text, int fontSize = 16)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", TextColor);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", DimColor);
        button.AddThemeStyleboxOverride("normal", Box(MapRenderer.Rgb(90, 66, 30), BorderColor, 2, 2, 2, 2, radius: 6));
        button.AddThemeStyleboxOverride("hover", Box(MapRenderer.Rgb(120, 90, 40), BorderColor, 2, 2, 2, 2, radius: 6));
        button.AddThemeStyleboxOverride("pressed", Box(MapRenderer.Rgb(70, 50, 20), BorderColor, 2, 2, 2, 2, radius: 6));
        button.AddThemeStyleboxOverride("disabled", Box(MapRenderer.Rgb(50, 42, 32), DimColor, 2, 2, 2, 2, radius: 6));
        return button;
    }
}
