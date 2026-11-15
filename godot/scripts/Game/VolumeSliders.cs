using System;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Les curseurs du volume (musique, effets, ambiance), dans les options de la partie, du combat et du menu principal.
/// Chaque changement s'entend aussitôt et reste d'une partie à l'autre (<see cref="Settings"/>).
/// </summary>
public static class VolumeSliders
{
    /// <summary>Le titre « Son » et un curseur par volume, de la largeur donnée (en unités de <see cref="Px"/>).</summary>
    public static Control Make(float width)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", (int)Px(4));
        box.AddChild(MakeLabel(Texts.SoundTitle, 15, SoftTextColor));
        Add(box, width, Texts.MusicVolumeLabel, () => Settings.MusicVolume, v => Settings.MusicVolume = v);
        // au lâcher du curseur des effets, un clic pour entendre le nouveau volume
        Add(box, width, Texts.EffectsVolumeLabel, () => Settings.EffectsVolume, v => Settings.EffectsVolume = v, () => Audio.Play("click"));
        Add(box, width, Texts.AmbienceVolumeLabel, () => Settings.AmbienceVolume, v => Settings.AmbienceVolume = v);
        return box;
    }

    private static void Add(Control box, float width, string text, Func<double> get, Action<double> set, Action? released = null)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)Px(8));
        var label = MakeLabel(text, 14, TextColor);
        label.CustomMinimumSize = V(96, 0);
        row.AddChild(label);
        var slider = new HSlider
        {
            MinValue = 0, MaxValue = 100, Step = 5, Value = Math.Round(get() * 100),
            FocusMode = Control.FocusModeEnum.None, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        slider.CustomMinimumSize = V(width - 104, 24);
        slider.ValueChanged += value => set(value / 100);
        if (released != null)
            slider.DragEnded += _ => released();
        row.AddChild(slider);
        box.AddChild(row);
    }
}
