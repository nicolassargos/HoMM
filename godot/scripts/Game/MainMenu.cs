using System;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le menu principal (étape 7), montré quand le jeu est lancé sans option : Nouvelle partie, Continuer (la sauvegarde
/// la plus récente), Charger, Réglages, Quitter. Toute option de lancement (ou « --play ») le saute.
/// </summary>
public partial class MainMenu : CanvasLayer
{
    /// <summary>Le joueur commence une nouvelle partie.</summary>
    public event Action<GameSetup>? NewGame;

    /// <summary>Le joueur reprend cette sauvegarde.</summary>
    public event Action<SaveEntry>? LoadChosen;

    private NewGameWindow _newGame = null!;
    private SavesWindow _saves = null!;

    public void Build(Vector2 screen)
    {
        Layer = 6;
        AddChild(new ColorRect { Size = screen, Color = DarkPanelColor });

        var size = V(440, 470);
        var frame = new Panel { Position = (screen - size) / 2, Size = size };
        frame.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        AddChild(frame);
        var title = MakeLabel(Texts.GameTitle, 44, BorderColor);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Size = new Vector2(size.X, Px(60));
        title.Position = new Vector2(0, Px(24));
        frame.AddChild(title);

        var latest = SaveFiles.Latest();
        float y = Px(110);
        Button Add(string text, string tooltip, Action pressed, bool disabled = false)
        {
            var button = MakeButton(text, 18);
            button.Position = new Vector2((size.X - Px(320)) / 2, y);
            button.Size = V(320, 50);
            button.TooltipText = tooltip;
            button.Disabled = disabled;
            button.Pressed += pressed;
            frame.AddChild(button);
            y += Px(62);
            return button;
        }
        Add(Texts.NewGameButton, Texts.NewGameTooltip, () => _newGame.Open());
        Add(Texts.ContinueButton, latest == null ? Texts.NoSave : $"{Texts.SaveDisplayName(latest)}\n{Texts.SaveDetails(latest)}",
            () => LoadChosen?.Invoke(latest!), disabled: latest == null);
        Add(Texts.LoadGameButton, Texts.LoadTooltip, () => _saves.OpenToLoad());
        Add(Texts.SettingsButton, Texts.SettingsTooltip, ShowSettings);
        Add(Texts.QuitGameButton, Texts.QuitGameButton, () => GetTree().Quit());

        _newGame = new NewGameWindow();
        AddChild(_newGame);
        _newGame.Build(screen);
        _newGame.Started += setup => NewGame?.Invoke(setup);
        _saves = new SavesWindow();
        AddChild(_saves);
        _saves.Build(screen, overMenu: true);
        _saves.LoadChosen += entry => LoadChosen?.Invoke(entry);
    }

    /// <summary>Les réglages : pour l'instant, le plein écran.</summary>
    private void ShowSettings()
    {
        var dialog = new AcceptDialog
        {
            Title = Texts.SettingsButton, DialogText = Texts.SettingsText, OkButtonText = Texts.CloseButton,
            Exclusive = true, Unresizable = true,
        };
        dialog.AddButton(Texts.FullscreenButton, false, "fullscreen").TooltipText =
            Texts.KeyTooltip(Texts.FullscreenTooltip, "F11 ou Alt+Entrée");
        dialog.CustomAction += _ => Main.ToggleFullscreen();
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(480), 0));
    }

    /// <summary>Développement : ouvre une fenêtre du menu pour une capture d'écran (« new » ou « load »).</summary>
    public void OpenForCapture(string which)
    {
        if (which == "new")
            _newGame.Open();
        else if (which == "load")
            _saves.OpenToLoad();
    }
}
