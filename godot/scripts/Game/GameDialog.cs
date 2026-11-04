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

    public override void _Ready()
    {
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
        // les boutons du bas : une largeur minimale (un « OK » seul était minuscule)
        foreach (var child in GetOkButton().GetParent().GetChildren())
            if (child is Button button)
                button.CustomMinimumSize = V(140, 40);
    }
}
