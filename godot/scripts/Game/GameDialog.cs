using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Une petite fenêtre du jeu (étape 7), dans le même style que les grandes (<see cref="ScreenWindow"/>) : pas de barre
/// de titre de Godot ni de croix, le cadre du jeu, le titre en haut à gauche, les boutons en bas ; Échap annule.
/// S'utilise comme un <see cref="AcceptDialog"/> : <c>Title</c>, <c>DialogText</c> ou un contenu ajouté avant
/// l'affichage, <c>AddButton</c>, <c>AddCancelButton</c>, signaux <c>Confirmed</c>, <c>Canceled</c>, <c>CustomAction</c>.
/// </summary>
public partial class GameDialog : AcceptDialog
{
    public GameDialog()
    {
        Borderless = true; // ni barre de titre ni croix : le cadre est le fond de la fenêtre (thème « AcceptDialog »)
        Exclusive = true;
        Unresizable = true;
    }

    /// <summary>Une fenêtre de confirmation : <paramref name="ok"/> confirme, « Annuler » annule.</summary>
    public static GameDialog Confirm(string title, string text, string ok)
    {
        var dialog = new GameDialog { Title = title, DialogText = text, OkButtonText = ok };
        dialog.AddCancelButton(Texts.CancelButton);
        return dialog;
    }

    /// <summary>Le voile qui assombrit l'écran derrière la fenêtre, tant qu'elle est ouverte (comme les grandes fenêtres).</summary>
    private CanvasLayer? _veil;

    /// <summary>L'opacité du voile (plus forte derrière le livre de sorts, qui s'ouvre par-dessus une autre fenêtre).</summary>
    protected float VeilOpacity { get; init; } = 0.45f;

    private void ShowVeil(bool shown)
    {
        if (shown && _veil == null)
        {
            // une fenêtre intégrée se dessine par-dessus toutes les couches : le voile reste dessous
            _veil = new CanvasLayer { Layer = 90 };
            _veil.AddChild(new ColorRect
            {
                Size = GetTree().Root.GetVisibleRect().Size, Color = new Color(0, 0, 0, VeilOpacity),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            // plus tard : la racine peut être occupée à ajouter des nœuds
            var veil = _veil;
            var root = GetTree().Root;
            Callable.From(() =>
            {
                if (IsInstanceValid(veil) && !veil.IsQueuedForDeletion())
                    root.AddChild(veil);
            }).CallDeferred();
        }
        else if (!shown && _veil != null)
        {
            _veil.QueueFree();
            _veil = null;
        }
    }

    public override void _Process(double delta)
    {
        if (Visible != (_veil != null))
            ShowVeil(Visible);
    }

    public override void _ExitTree() => ShowVeil(false);

    public override void _Ready()
    {
        // le voile suit la fenêtre à chaque image (_Process) : une fenêtre que Godot retire puis remet dans l'arbre au moment
        // de l'ouvrir (ainsi dans l'écran de ville) perdait son voile avec les seuls signaux
        if (Visible)
            ShowVeil(true);

        // le titre, puis le texte ou le contenu déjà ajouté, dans une même colonne
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", (int)Px(12));
        if (Title.Length > 0)
            column.AddChild(MakeLabel(Title, 20, TextColor));
        foreach (var child in GetChildren())
        {
            if (child is not Control control)
                continue;
            RemoveChild(control);
            column.AddChild(control);
        }
        if (DialogText.Length > 0)
        {
            var text = MakeLabel(DialogText, 15, TextColor);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.CustomMinimumSize = V(400, 0);
            column.AddChild(text);
            DialogText = "";
        }
        AddChild(column);
        // la taille des boutons du bas vient du thème (« buttons_min_width », Ui.ApplyTheme)
    }
}
