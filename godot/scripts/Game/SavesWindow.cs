using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// La liste des sauvegardes (étape 7), pour charger une partie (menu principal ou options) ou pour la sauvegarder
/// sous un nom. Chaque ligne : le nom, puis le jour, le héros, la taille de la carte et l'heure de la sauvegarde.
/// </summary>
public partial class SavesWindow : ScreenWindow
{
    /// <summary>Le joueur charge cette sauvegarde.</summary>
    public event Action<SaveEntry>? LoadChosen;

    /// <summary>Le joueur sauvegarde sous ce nom (déjà confirmé s'il remplace une sauvegarde).</summary>
    public event Action<string>? SaveChosen;

    private bool _saving;
    private VBoxContainer _rows = null!;
    private LineEdit _name = null!;
    private Label _nameLabel = null!;
    private Button _action = null!, _delete = null!;
    private SaveEntry? _selected;
    private readonly Dictionary<SaveEntry, (Button Button, StyleBox Normal)> _buttons = new();
    private Vector2 _size;

    private ScrollContainer _scroll = null!;
    private Panel _area = null!;

    /// <summary><paramref name="overMenu"/> : au menu principal (le voile couvre tout l'écran).</summary>
    public void Build(Vector2 screenSize, bool overMenu = false)
    {
        _size = V(760, 540);
        BuildFrame(screenSize, new Rect2((screenSize - _size) / 2, _size), veilAll: overMenu);
        Layer = 8; // au-dessus de tout (aussi du menu principal)

        _nameLabel = At(MakeLabel(Texts.SaveNameLabel, 15, SoftTextColor), 20, 62);
        Window.AddChild(_nameLabel);
        _name = new LineEdit { Position = V(150, 56), Size = new Vector2(Px(590), ButtonHeight), MaxLength = 40 };
        // le mot « Nom » centré sur la hauteur du champ
        _nameLabel.Position = new Vector2(_nameLabel.Position.X, _name.Position.Y + SnapDown((ButtonHeight - NormalText) / 2));
        SetFontSize(_name, 15);
        _name.TextChanged += _ => UpdateButtons();
        _name.TextSubmitted += _ => Act();
        Window.AddChild(_name);

        // la liste (des lignes à plat) dans une zone creusée
        _area = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        _area.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        Window.AddChild(_area);
        var scroll = _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        Window.AddChild(scroll);
        _rows = new VBoxContainer { CustomMinimumSize = new Vector2(_size.X - Px(60), 0) };
        _rows.AddThemeConstantOverride("separation", (int)Px(4));
        scroll.AddChild(_rows);

        _action = MakeButton("", 16);
        _action.Position = new Vector2(_size.X - Px(20 + 200), _size.Y - Px(56));
        Sized(_action, Px(200), ButtonHeight);
        _action.Pressed += Act;
        Window.AddChild(_action);
        _delete = MakeButton(Texts.DeleteSave, 14);
        _delete.Position = new Vector2(Px(20), _size.Y - Px(56));
        Sized(_delete, Px(160), ButtonHeight);
        _delete.Pressed += AskDelete;
        Window.AddChild(_delete);
    }

    /// <summary>Ouvre la liste pour charger une partie.</summary>
    public void OpenToLoad() => Open(saving: false, defaultName: "");

    /// <summary>Ouvre la liste pour sauvegarder ; <paramref name="defaultName"/> est proposé comme nom.</summary>
    public void OpenToSave(string defaultName) => Open(saving: true, defaultName);

    private void Open(bool saving, string defaultName)
    {
        _saving = saving;
        SetTitle(saving ? Texts.SaveTitle : Texts.LoadTitle);
        _action.Text = saving ? Texts.SaveAction : Texts.LoadAction;
        _name.Visible = _nameLabel.Visible = saving;
        // la liste commence sous le nom (pour sauvegarder), sinon sous le titre
        float top = Px(saving ? 104 : 60);
        _scroll.Position = new Vector2(Px(20), top);
        _scroll.Size = new Vector2(_size.X - Px(40), _size.Y - top - Px(70));
        _area.Position = _scroll.Position - V(6, 6);
        _area.Size = _scroll.Size + V(12, 12);
        _name.Text = defaultName;
        _selected = null;
        Refresh();
        OpenWindow();
        if (saving)
        {
            _name.GrabFocus();
            _name.SelectAll();
        }
    }

    private void Refresh()
    {
        Clear(_rows);
        _buttons.Clear();
        // pour sauvegarder, seules les sauvegardes nommées (la rapide et l'automatique ont leurs propres touches)
        var entries = SaveFiles.List().Where(e => !_saving || !(e.IsQuick || e.IsAuto)).ToList();
        if (entries.Count == 0)
            _rows.AddChild(MakeLabel(Texts.NoSave, 15, SoftTextColor));
        foreach (var entry in entries)
        {
            var button = MakeButton("", 14);
            button.CustomMinimumSize = new Vector2(_size.X - Px(60), Px(56));
            var lines = new VBoxContainer
            {
                Position = V(12, 4), Size = new Vector2(_size.X - Px(84), Px(48)), MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            lines.AddThemeConstantOverride("separation", 0);
            // chaque ligne coupée au bord du bouton (« … ») ; le détail entier au survol
            foreach (var line in new[] { MakeLabel(Texts.SaveDisplayName(entry), 16, entry.Summary == null ? DimColor : TextColor),
                         MakeLabel(Texts.SaveDetails(entry), 13, SoftTextColor) })
            {
                line.ClipText = true;
                line.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                lines.AddChild(line);
            }
            button.TooltipText = Texts.SaveDetails(entry);
            button.AddChild(lines);
            var chosen = entry;
            button.Pressed += () => Select(chosen);
            _rows.AddChild(button);
            _buttons[entry] = (button, button.GetThemeStylebox("normal"));
        }
        if (!_saving && entries.FirstOrDefault(e => e.Summary != null) is { } latest)
            Select(latest);
        UpdateButtons();
    }

    private void Select(SaveEntry entry)
    {
        if (_selected != null && _buttons.TryGetValue(_selected, out var previous))
            previous.Button.AddThemeStyleboxOverride("normal", previous.Normal);
        _selected = entry;
        MarkSelected(_buttons[entry].Button);
        if (_saving)
            _name.Text = entry.Name;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _action.Disabled = _saving ? SaveFiles.FileName(_name.Text).Length == 0 : _selected?.Summary == null;
        _delete.Disabled = _selected == null;
    }

    private void Act()
    {
        if (_action.Disabled)
            return;
        if (!_saving)
        {
            var entry = _selected!;
            Close();
            LoadChosen?.Invoke(entry);
            return;
        }
        string name = _name.Text.Trim();
        if (name == SaveFiles.QuickName || name == SaveFiles.AutoName || !SaveFiles.Exists(name))
        {
            Close();
            SaveChosen?.Invoke(name);
            return;
        }
        Confirm(Texts.OverwriteTitle, Texts.OverwriteText(name), Texts.SaveAction, () =>
        {
            Close();
            SaveChosen?.Invoke(name);
        });
    }

    private void AskDelete()
    {
        if (_selected is not { } entry)
            return;
        Confirm(Texts.DeleteTitle, Texts.DeleteText(Texts.SaveDisplayName(entry)), Texts.DeleteSave, () =>
        {
            SaveFiles.Delete(entry);
            _selected = null;
            Refresh();
        });
    }

    private void Confirm(string title, string text, string ok, Action confirmed)
    {
        var dialog = GameDialog.Confirm(title, text, ok);
        dialog.Confirmed += () => { dialog.QueueFree(); confirmed(); };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(480), 0));
    }
}
