using System;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Une grande fenêtre par-dessus la carte (étape 7 : écran du héros, royaume) : la carte s'assombrit (la barre des
/// ressources reste visible), un cadre avec un titre et « Fermer » ; Échap ferme aussi.
/// </summary>
public partial class ScreenWindow : CanvasLayer
{
    /// <summary>Le contenu de la fenêtre : les écrans y ajoutent leurs éléments (coordonnées depuis son coin).</summary>
    protected Control Window { get; private set; } = null!;

    private Label _title = null!;

    public bool IsOpen => Visible;
    public event Action? Closed;

    /// <summary>Prépare le voile et le cadre ; <paramref name="window"/> : la place de la fenêtre à l'écran (en pixels).</summary>
    protected void BuildFrame(Vector2 screenSize, Rect2 window)
    {
        Layer = 4; // au-dessus de la carte et du panneau, sous l'écran de ville
        Visible = false;
        AddChild(new ColorRect
        {
            Position = new Vector2(0, Hud.TopBarHeight), Size = screenSize - new Vector2(0, Hud.TopBarHeight),
            Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop,
        });
        var frame = new Panel { Position = window.Position, Size = window.Size };
        frame.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        AddChild(frame);
        Window = frame;
        _title = At(MakeLabel("", 20, TextColor), 20, 12);
        frame.AddChild(_title);
        var close = MakeButton($"{Texts.CloseButton} (Échap)", 14);
        close.Position = new Vector2(window.Size.X - Px(20 + 150), Px(12));
        close.Size = V(150, 34);
        close.Pressed += Close;
        frame.AddChild(close);
    }

    protected void SetTitle(string title) => _title.Text = title;

    protected void OpenWindow() => Visible = true;

    public void Close()
    {
        if (!IsOpen)
            return;
        Visible = false;
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (IsOpen && e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
