using System;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'écran du combat tactique (étape 6), en plein écran : le titre et le round, l'ordre de jeu, le champ de bataille
/// (<see cref="BattleFieldView"/>), les boutons Attendre, Se défendre, Auto, Fuir, et une ligne qui raconte le dernier
/// coup. Le joueur fait jouer ses troupes à la souris : un clic sur une case pour y aller, sur un ennemi pour
/// l'attaquer (du côté le plus proche de la souris) ou lui tirer dessus. L'IA joue les troupes ennemies, à un rythme
/// lisible ; « Auto » la laisse jouer aussi les troupes du joueur jusqu'à la fin.
/// </summary>
public partial class BattleScreen : CanvasLayer
{
    private const float AiDelay = 0.35f;

    private Vector2 _screen;
    private TacticalBattle? _battle;
    private Action? _finished;
    private bool _auto;
    private double _wait;
    private string _opponent = "";

    private Label _title = null!;
    private Label _message = null!;
    private HBoxContainer _order = null!;

    /// <summary>Hauteur de la bande de l'ordre de jeu, en bas de l'écran.</summary>
    private const int OrderHeight = 64;
    private BattleFieldView _field = null!;
    private Button _waitButton = null!, _defendButton = null!, _autoButton = null!, _fleeButton = null!;

    public bool IsOpen => _battle != null;

    public void Build(Vector2 screenSize)
    {
        _screen = screenSize;
        Layer = 6; // au-dessus de tout
        Visible = false;

        var root = new Panel { Size = screenSize, MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddThemeStyleboxOverride("panel", Frame(DarkPanelColor));
        AddChild(root);

        // En bas, sur une ligne (comme dans Heroes V) : le round, l'ordre de jeu complet (une bande de portraits),
        // et les boutons à droite
        float barY = screenSize.Y - 10 - OrderHeight;
        // les boutons, en une rangée à droite qui prend la largeur de leurs textes
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 6);
        _waitButton = AddButton(buttons, Texts.BattleWait, () => Player(b => b.Wait()));
        _defendButton = AddButton(buttons, Texts.BattleDefend, () => Player(b => b.Defend()));
        _autoButton = AddButton(buttons, Texts.BattleAuto, () => _auto = true);
        _fleeButton = AddButton(buttons, Texts.BattleFlee, () => Player(b => b.Flee()));
        float buttonsWidth = buttons.GetCombinedMinimumSize().X;
        buttons.Position = new Vector2(screenSize.X - 16 - buttonsWidth, barY + (OrderHeight - 40) / 2f);
        buttons.Size = new Vector2(buttonsWidth, 40);
        root.AddChild(buttons);

        _title = At(MakeLabel("", 16, TextColor), 16, barY);
        _title.Size = new Vector2(RoundWidth, OrderHeight);
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.MouseFilter = Control.MouseFilterEnum.Pass; // pour son info-bulle (l'adversaire)
        root.AddChild(_title);
        _order = new HBoxContainer
        {
            Position = new Vector2(16 + RoundWidth + 8, barY),
            Size = new Vector2(buttons.Position.X - 16 - RoundWidth - 8 - 16, OrderHeight),
            ClipContents = true,
        };
        _order.AddThemeConstantOverride("separation", 4);
        root.AddChild(_order);

        // Le champ de bataille, centré dessous (de la place reste de chaque côté : héros, machines de guerre…),
        // sous la ligne de la dernière action
        _field = new BattleFieldView();
        var size = BattleFieldView.FieldSize;
        const float bandHeight = 32, bandGap = 6;
        float top = 10 + bandHeight + bandGap;
        _field.Position = new Vector2((screenSize.X - size.X) / 2, top + (barY - 8 - top - size.Y) / 2);
        _field.Clicked += OnClicked;
        root.AddChild(_field);

        // La dernière action, sur une ligne au-dessus du champ
        var band = new Panel
        {
            Position = new Vector2(_field.Position.X, _field.Position.Y - bandGap - bandHeight),
            Size = new Vector2(size.X, bandHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        band.AddThemeStyleboxOverride("panel", Box(PanelColor, BorderColor, 1, 1, 1, 1));
        root.AddChild(band);
        _message = At(MakeLabel("", 14, TextColor), 10, 4);
        _message.Size = band.Size - new Vector2(20, 8);
        _message.ClipText = true;
        _message.VerticalAlignment = VerticalAlignment.Center;
        band.AddChild(_message);
    }

    /// <summary>Largeur de « Round 2 », au début de la ligne du bas.</summary>
    private const float RoundWidth = 96;

    private static Button AddButton(Control parent, string text, Action pressed)
    {
        var button = MakeButton(text, 14);
        button.CustomMinimumSize = new Vector2(96, 40);
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    /// <summary>Ouvre l'écran pour ce combat ; <paramref name="finished"/> est appelé quand il est fini et montré.</summary>
    public void Open(TacticalBattle battle, string enemy, TerrainType terrain, Action finished, bool auto = false)
    {
        _battle = battle;
        _finished = finished;
        _auto = auto;
        _wait = 0.5;
        _opponent = enemy;
        _title.Text = Texts.BattleRound(1);
        _title.TooltipText = Texts.BattleTitle(enemy);
        _message.Text = "";
        _field.Start(battle, terrain);
        Visible = true;
        Show(battle.TakeLog());
    }

    /// <summary>Le joueur fait une action avec sa troupe active (seulement à son tour).</summary>
    private void Player(Func<TacticalBattle, bool> action)
    {
        if (_battle is { } battle && IsPlayerTurn && action(battle))
            Show(battle.TakeLog());
    }

    private bool IsPlayerTurn => _battle is { Active: { Side: BattleSide.Attacker } } && !_auto && !_field.IsAnimating;

    private void OnClicked(Hex hex, Vector2 point)
    {
        if (_battle is not { Active: { } unit } battle || !IsPlayerTurn)
            return;
        if (battle.UnitAt(hex) is { } other && TacticalBattle.AreEnemies(unit, other))
        {
            if (battle.CanShoot(unit, other))
                Player(b => b.Shoot(other));
            else if (battle.AttackPositions(unit, other).OrderBy(h => BattleFieldView.Center(h).DistanceSquaredTo(point)).Cast<Hex?>().FirstOrDefault() is { } from)
                Player(b => b.Attack(other, from));
            return;
        }
        Player(b => b.Move(hex));
    }

    private void Show(System.Collections.Generic.List<BattleAction> actions)
    {
        _field.Show(actions, action =>
        {
            if (Texts.BattleMessage(action) is { } text)
                _message.Text = text;
        });
    }

    public override void _Process(double delta)
    {
        if (_battle is not { } battle)
            return;
        if (battle.IsOver)
        {
            if (!_field.IsAnimating)
                Close();
            return;
        }
        // L'IA joue les ennemis (et tout, en mode Auto), une action à la fois, quand l'animation précédente est finie
        if (!_field.IsAnimating && (battle.Active?.Side == BattleSide.Defender || _auto))
        {
            _wait -= delta;
            if (_wait <= 0)
            {
                BattleAI.PlayTurn(battle);
                Show(battle.TakeLog());
                _wait = _auto ? AiDelay / 2 : AiDelay;
            }
        }
        Refresh();
    }

    /// <summary>Ce qui change à chaque image : l'ordre de jeu, les cases mises en valeur, les boutons, le curseur.</summary>
    private void Refresh()
    {
        var battle = _battle!;
        var unit = battle.Active;
        bool mine = IsPlayerTurn;
        _title.Text = Texts.BattleRound(battle.Round);
        foreach (var button in new[] { _waitButton, _defendButton, _fleeButton })
            button.Disabled = !mine;
        _waitButton.Disabled = !mine || unit!.HasWaited;
        _autoButton.Disabled = _auto;

        _field.ActiveUnit = unit;
        _field.Reachable = mine ? battle.Reachable(unit!).Keys.ToList() : Array.Empty<Hex>();
        _field.Target = null;
        var cursor = CursorKind.Pointer;
        if (mine && _field.Hovered is { } hovered)
        {
            if (battle.UnitAt(hovered) is { } other && TacticalBattle.AreEnemies(unit!, other))
            {
                bool canHit = battle.CanShoot(unit!, other) || battle.AttackPositions(unit!, other).Any();
                cursor = canHit ? CursorKind.Attack : CursorKind.Blocked;
                if (canHit)
                    _field.Target = hovered;
            }
            else if (battle.Reachable(unit!).ContainsKey(hovered) && hovered != unit!.Position)
                cursor = CursorKind.Move;
            else
                cursor = CursorKind.Blocked;
        }
        MapCursor.Show(cursor);
        RefreshOrder(battle);
    }

    private string? _orderKey;

    /// <summary>
    /// L'ordre de jeu complet, de gauche à droite : d'abord les troupes qui jouent encore dans ce round (la première
    /// encadrée d'or), puis, après « Round suivant » et plus pâles, celles qui ont déjà joué. Chaque case : l'image de
    /// la créature et son nombre, sur la couleur du camp.
    /// </summary>
    private void RefreshOrder(TacticalBattle battle)
    {
        var coming = battle.TurnOrder;
        var played = battle.InitiativeOrder.Where(u => !coming.Contains(u)).ToList();
        string key = string.Join(",", coming.Select(u => $"{u.Id}:{_field.ShownCount(u)}")) + "|"
            + string.Join(",", played.Select(u => $"{u.Id}:{_field.ShownCount(u)}"));
        if (key == _orderKey)
            return;
        _orderKey = key;
        foreach (var child in _order.GetChildren())
        {
            _order.RemoveChild(child);
            child.QueueFree();
        }
        // les troupes tuées restent affichées tant que le coup n'est pas montré
        foreach (var (unit, i) in coming.Where(u => _field.ShownCount(u) > 0).Select((u, i) => (u, i)))
            _order.AddChild(OrderSlot(unit, _field.ShownCount(unit), i == 0, dim: false));
        var next = played.Where(u => _field.ShownCount(u) > 0).ToList();
        if (next.Count > 0)
        {
            var label = MakeLabel(Texts.BattleNextRound, 12, SoftTextColor);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.CustomMinimumSize = new Vector2(0, OrderHeight);
            _order.AddChild(label);
            foreach (var unit in next)
                _order.AddChild(OrderSlot(unit, _field.ShownCount(unit), false, dim: true));
        }
    }

    private static Control OrderSlot(BattleUnit unit, int count, bool active, bool dim)
    {
        var slot = new PanelContainer { CustomMinimumSize = new Vector2(58, OrderHeight), TooltipText = Texts.Stack(new CreatureStack(unit.Type, count)) };
        var side = unit.Side == BattleSide.Attacker ? MapRenderer.Rgb(40, 60, 120) : MapRenderer.Rgb(110, 40, 30);
        int border = active ? 3 : 1;
        slot.AddThemeStyleboxOverride("panel", Box(side, active ? MapRenderer.Rgb(240, 200, 80) : DimColor, border, border, border, border));
        if (dim)
            slot.Modulate = new Color(1, 1, 1, 0.5f);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        if (CreatureImage(unit.Type) is { } picture)
        {
            picture.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            column.AddChild(picture);
        }
        var number = MakeLabel(count.ToString(), 12, TextColor);
        number.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(number);
        slot.AddChild(column);
        return slot;
    }

    private void Close()
    {
        var finished = _finished;
        _battle = null;
        _finished = null;
        Visible = false;
        MapCursor.Show(CursorKind.Pointer);
        finished?.Invoke();
    }
}
