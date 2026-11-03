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

        _title = At(MakeLabel("", 20, TextColor), 24, 12);
        root.AddChild(_title);
        // l'ordre de jeu, à droite du titre (la troupe qui joue la plus à gauche)
        _order = new HBoxContainer { Position = new Vector2(screenSize.X - 24 - 700, 6), Size = new Vector2(700, 48), Alignment = BoxContainer.AlignmentMode.End };
        _order.AddThemeConstantOverride("separation", 4);
        root.AddChild(_order);

        _field = new BattleFieldView();
        var size = BattleFieldView.FieldSize;
        _field.Position = new Vector2((screenSize.X - size.X) / 2, 62);
        _field.Clicked += OnClicked;
        root.AddChild(_field);

        float y = screenSize.Y - 52;
        _message = At(MakeLabel("", 14, TextColor), 24, y + 8);
        _message.Size = new Vector2(screenSize.X - 24 - 4 * 136 - 24, 30);
        _message.ClipText = true;
        root.AddChild(_message);
        float x = screenSize.X - 24 - 4 * 136;
        _waitButton = AddButton(root, Texts.BattleWait, ref x, y, () => Player(b => b.Wait()));
        _defendButton = AddButton(root, Texts.BattleDefend, ref x, y, () => Player(b => b.Defend()));
        _autoButton = AddButton(root, Texts.BattleAuto, ref x, y, () => _auto = true);
        _fleeButton = AddButton(root, Texts.BattleFlee, ref x, y, () => Player(b => b.Flee()));
    }

    private static Button AddButton(Control parent, string text, ref float x, float y, Action pressed)
    {
        var button = MakeButton(text, 15);
        button.Position = new Vector2(x, y);
        button.Size = new Vector2(130, 40);
        button.Pressed += pressed;
        parent.AddChild(button);
        x += 136;
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
        _title.Text = Texts.BattleTitle(enemy);
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
        _field.Show(actions);
        foreach (var action in actions)
            if (Texts.BattleMessage(action) is { } text)
                _message.Text = text;
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
        _title.Text = $"{Texts.BattleTitle(_opponent)} · {Texts.BattleRound(battle.Round)}";
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

    /// <summary>La barre de l'ordre de jeu : les images des troupes qui vont jouer dans ce round, la première encadrée.</summary>
    private void RefreshOrder(TacticalBattle battle)
    {
        var order = battle.TurnOrder;
        string key = string.Join(",", order.Select(u => $"{u.Id}:{u.Count}"));
        if (key == _orderKey)
            return;
        _orderKey = key;
        foreach (var child in _order.GetChildren())
        {
            _order.RemoveChild(child);
            child.QueueFree();
        }
        foreach (var (unit, i) in order.Take(12).Select((u, i) => (u, i)))
        {
            var slot = new PanelContainer { CustomMinimumSize = new Vector2(54, 46) };
            slot.AddThemeStyleboxOverride("panel", Box(unit.Side == BattleSide.Attacker ? MapRenderer.Rgb(40, 60, 120) : MapRenderer.Rgb(110, 40, 30),
                i == 0 ? MapRenderer.Rgb(240, 200, 80) : DimColor, 2, 2, 2, 2));
            slot.TooltipText = Texts.Stack(unit.Stack);
            if (CreatureImage(unit.Type) is { } picture)
            {
                picture.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
                slot.AddChild(picture);
            }
            _order.AddChild(slot);
        }
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
