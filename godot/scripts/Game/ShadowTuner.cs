using System;
using System.Globalization;
using Godot;

namespace HoMM.Game;

/// <summary>
/// Développement (« -- --shadow-tune ») : trois curseurs en haut à gauche de l'écran pour régler les ombres de la carte
/// pendant la partie (force, longueur, penche). Les valeurs choisies s'affichent sous la forme de l'option --shadow=…
/// </summary>
public partial class ShadowTuner : CanvasLayer
{
    private readonly ShadowLayer _shadows;
    private Label _summary = null!;

    public ShadowTuner() => _shadows = null!;
    public ShadowTuner(ShadowLayer shadows) => _shadows = shadows;

    public override void _Ready()
    {
        Layer = 50;
        var panel = new PanelContainer { Position = new Vector2(16, 120) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.1f, 0.08f, 0.06f, 0.9f),
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8 });
        var box = new VBoxContainer();
        panel.AddChild(box);
        box.AddChild(Ui.MakeLabel("Ombres", 18, Colors.White));
        AddSlider(box, "Force", 0, 1, () => ShadowLayer.Strength, v => ShadowLayer.Strength = v);
        AddSlider(box, "Longueur", 0, 1.5f, () => ShadowLayer.Length, v => ShadowLayer.Length = v);
        AddSlider(box, "Penche", -0.5f, 2, () => ShadowLayer.Lean, v => ShadowLayer.Lean = v);
        _summary = Ui.MakeLabel("", 14, MapRenderer.Rgb(230, 210, 150));
        box.AddChild(_summary);
        AddChild(panel);
        Refresh();
    }

    private void AddSlider(VBoxContainer box, string name, float min, float max, Func<float> get, Action<float> set)
    {
        var label = Ui.MakeLabel("", 14, Colors.White);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = 0.05, Value = get(), CustomMinimumSize = new Vector2(240, 20) };
        void Show() => label.Text = $"{name} : {get():0.00}";
        slider.ValueChanged += v =>
        {
            set((float)v);
            Show();
            Refresh();
        };
        Show();
        box.AddChild(label);
        box.AddChild(slider);
    }

    private void Refresh()
    {
        _shadows.Apply();
        string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        _summary.Text = $"--shadow={F(ShadowLayer.Strength)},{F(ShadowLayer.Length)},{F(ShadowLayer.Lean)}";
    }
}
