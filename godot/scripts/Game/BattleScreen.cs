using System;
using System.Collections.Generic;
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
/// Étape 7 : le champ s'élargit de chaque côté de la grille (le héros y est dessiné, un clic montre ses
/// caractéristiques ; plus tard les machines de guerre) ; un clic droit maintenu sur une troupe montre sa fiche ; un
/// journal du combat ; les touches W (attendre), D (se défendre), A (auto), F (fuir).
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
    private Hero? _hero;
    private readonly List<string> _log = new();

    private Panel _unitInfo = null!;
    private Label _unitInfoText = null!;

    private Label _title = null!;
    private Label _message = null!;
    private HBoxContainer _order = null!;

    /// <summary>Hauteur de la bande de l'ordre de jeu, en bas de l'écran.</summary>
    private const int OrderHeight = 96; // en pixels
    private BattleFieldView _field = null!;
    private Button _waitButton = null!, _defendButton = null!, _autoButton = null!, _fleeButton = null!, _spellButton = null!;

    /// <summary>Développement : ouvrir le livre de sorts dès que le joueur peut jouer (« --battle-book »).</summary>
    public bool OpenSpellBookWhenReady { get; set; }

    /// <summary>Développement : choisir ce sort dès que le joueur peut jouer (« --battle-book=id » : le choix de la cible).</summary>
    public string? ChooseSpellWhenReady { get; set; }

    /// <summary>Le sort choisi dans le livre, dont le joueur choisit la cible (null : pas de sort en cours).</summary>
    private SpellType? _spell;

    /// <summary>Pour une téléportation : la troupe choisie (sa case), dont le joueur choisit maintenant la destination.</summary>
    private BattleUnit? _teleported;

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
        float barY = screenSize.Y - Px(10) - OrderHeight;
        // les boutons, en une rangée à droite qui prend la largeur de leurs textes
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", (int)Px(6));
        _spellButton = AddButton(buttons, Texts.SpellBookButton, "S", OpenSpellBook);
        _waitButton = AddButton(buttons, Texts.BattleWait, "W", () => Player(b => b.Wait()));
        _defendButton = AddButton(buttons, Texts.BattleDefend, "D", () => Player(b => b.Defend()));
        _autoButton = AddButton(buttons, Texts.BattleAuto, "A", () => _auto = true);
        _fleeButton = AddButton(buttons, Texts.BattleFlee, "F", () => Player(b => b.Flee()));
        float buttonsWidth = buttons.GetCombinedMinimumSize().X;
        buttons.Position = new Vector2(screenSize.X - Px(16) - buttonsWidth, barY + (OrderHeight - Px(40)) / 2f);
        buttons.Size = new Vector2(buttonsWidth, Px(40));
        root.AddChild(buttons);

        _title = MakeLabel("", 16, TextColor);
        _title.Position = new Vector2(Px(16), barY);
        _title.Size = new Vector2(RoundWidth, OrderHeight);
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.MouseFilter = Control.MouseFilterEnum.Pass; // pour son info-bulle (l'adversaire)
        root.AddChild(_title);
        _order = new HBoxContainer
        {
            Position = new Vector2(Px(16 + 8) + RoundWidth, barY),
            Size = new Vector2(buttons.Position.X - RoundWidth - Px(16 + 8 + 16), OrderHeight),
            ClipContents = true,
        };
        _order.AddThemeConstantOverride("separation", (int)Px(4));
        // l'ordre de jeu (des vignettes à plat) dans une zone creusée
        var orderArea = new Panel
        {
            Position = _order.Position - V(4, 4), Size = _order.Size + V(8, 8), MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        orderArea.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        root.AddChild(orderArea);
        root.AddChild(_order);

        // Le champ de bataille, centré dessous (de la place reste de chaque côté : héros, machines de guerre…),
        // sous la ligne de la dernière action
        _field = new BattleFieldView();
        var size = BattleFieldView.FieldSize;
        float bandHeight = Px(40), bandGap = Px(6);
        float top = Px(10) + bandHeight + bandGap;
        _field.Position = new Vector2((screenSize.X - size.X) / 2, top + (barY - Px(8) - top - size.Y) / 2);
        _field.Clicked += OnClicked;
        root.AddChild(_field);

        // La dernière action, sur une ligne au-dessus du champ
        var band = new Panel
        {
            Position = new Vector2(_field.Position.X, _field.Position.Y - bandGap - bandHeight),
            Size = new Vector2(size.X, bandHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        band.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        root.AddChild(band);
        _message = At(MakeLabel("", 14, TextColor), 10, 4);
        _message.ClipText = true;
        _message.Size = band.Size - V(20 + 100, 8);
        _message.VerticalAlignment = VerticalAlignment.Center;
        band.AddChild(_message);
        // le journal du combat, au bout de la ligne
        // un bouton de taille fixe (un bouton ordinaire grandit à la hauteur de son texte et dépassait de la ligne),
        // à l'intérieur du biseau
        var log = FixedButton(Texts.BattleLogButton, 13, new Vector2(Px(94), bandHeight - Px(10)));
        log.Position = band.Position + new Vector2(band.Size.X - Px(100), Px(5));
        log.Pressed += ShowLog;
        root.AddChild(log);

        BuildUnitInfo(root);
        _field.HeroClicked += ShowHero;
        _field.UnitInfoPressed += ShowUnitInfo;
        _field.UnitInfoReleased += () => _unitInfo.Visible = false;
    }

    /// <summary>La fiche d'une troupe, tant que le clic droit reste enfoncé (comme dans Heroes).</summary>
    private void BuildUnitInfo(Control root)
    {
        _unitInfo = new Panel { Visible = false, Size = V(240, 10), MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 5 };
        _unitInfo.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        root.AddChild(_unitInfo);
        _unitInfoText = At(MakeLabel("", 13, TextColor), 14, 12);
        _unitInfoText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _unitInfoText.CustomMinimumSize = V(212, 0);
        _unitInfo.AddChild(_unitInfoText);
    }

    private void ShowUnitInfo(BattleUnit unit, Vector2 at)
    {
        if (_spell != null)
        {
            CancelSpell(); // le clic droit annule le sort en cours
            return;
        }
        _unitInfoText.Text = $"{Texts.Stack(new CreatureStack(unit.Type, _field.ShownCount(unit)))}\n{Texts.UnitStats(unit)}\n"
            + $"{Texts.UnitAbilities(unit)}\n{Texts.UnitState(unit, _battle?.Round ?? 0)}";
        _unitInfo.Size = new Vector2(Px(240), _unitInfoText.GetMinimumSize().Y + Px(26));
        // à côté de la souris, sans sortir de l'écran
        var position = _field.Position + at + new Vector2(Px(16), -_unitInfo.Size.Y / 2);
        _unitInfo.Position = position.Clamp(Vector2.Zero, _screen - _unitInfo.Size);
        _unitInfo.Visible = true;
    }

    /// <summary>Un clic sur le héros : ses caractéristiques, ses points de sort et ses sorts.</summary>
    private void ShowHero(Hero hero)
    {
        int points = _battle is { } battle && battle.HeroOf(BattleSide.Attacker) == hero ? battle.SpellPointsOf(BattleSide.Attacker)
            : _battle is { } b && b.HeroOf(BattleSide.Defender) == hero ? b.SpellPointsOf(BattleSide.Defender) : hero.SpellPoints;
        var dialog = new GameDialog
        {
            Title = hero.Owner.IsComputer ? Texts.HeroWithColor(hero) : hero.Name, // un héros adverse : avec sa couleur OkButtonText = Texts.CloseButton, Exclusive = true, Unresizable = true,
            DialogText = $"{Texts.LevelLine(hero)}\n{Texts.BattleHeroText(hero, points)}",
            DialogAutowrap = true,
        };
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(360), 0));
    }

    /// <summary>Le journal : tous les messages de ce combat, le plus récent en bas.</summary>
    private void ShowLog()
    {
        var dialog = new GameDialog { Title = Texts.BattleLogTitle, OkButtonText = Texts.CloseButton, Exclusive = true, Unresizable = true };
        var scroll = new ScrollContainer { CustomMinimumSize = V(620, 380), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var text = MakeLabel(_log.Count == 0 ? Texts.LogEmpty : string.Join("\n", _log), 14, TextColor);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = V(600, 0);
        scroll.AddChild(text);
        dialog.AddChild(scroll);
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(660), 0));
        Callable.From(() => scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue).CallDeferred();
    }

    /// <summary>Les touches du combat : S sorts, W attendre, D se défendre, A auto, F fuir ; Échap annule un sort.</summary>
    public override void _UnhandledInput(InputEvent e)
    {
        if (_battle == null || e is not InputEventKey { Pressed: true, Echo: false } key)
            return;
        switch (key.Keycode)
        {
            case Key.Escape when _spell != null: CancelSpell(); break;
            case Key.S: OpenSpellBook(); break;
            case Key.W: Player(b => b.Wait()); break;
            case Key.D: Player(b => b.Defend()); break;
            case Key.A: _auto = true; break;
            case Key.F: Player(b => b.Flee()); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    // ------------------------------------------------------------------ le livre de sorts

    /// <summary>Le héros du joueur, s'il en a un dans ce combat : son livre se lit à tout moment.</summary>
    private Hero? PlayerHero => _battle?.HeroOf(_playerSide);

    /// <summary>Le joueur peut-il lancer un sort maintenant : à son tour, avec un héros qui n'en a pas encore lancé ce round.</summary>
    private bool CanCastNow =>
        IsPlayerTurn && _battle is { } battle && battle.CasterSide == _playerSide && !battle.HasCast(_playerSide);

    /// <summary>
    /// Le livre de sorts : à tout moment, pour le lire ; à son tour, un clic sur un sort qu'on peut payer le choisit, puis
    /// sa cible sur le champ.
    /// </summary>
    private void OpenSpellBook()
    {
        if (_battle is not { } battle || PlayerHero is not { } hero)
            return;
        string? notice = CanCastNow ? null : !IsPlayerTurn ? Texts.NotYourTurnToCast : Texts.OneSpellPerRound;
        var book = new SpellBookWindow(hero, battle.SpellPointsOf(_playerSide), spell => CanCastNow && battle.CanCast(spell), notice,
            spell =>
            {
                _spell = spell;
                _message.Text = Texts.SpellTargetPrompt(spell);
            });
        AddChild(book);
        book.Open();
    }

    private void CancelSpell()
    {
        _spell = null;
        _teleported = null;
        _message.Text = "";
    }

    /// <summary>Largeur de « Round 2 », au début de la ligne du bas.</summary>
    private const float RoundWidth = 144; // en pixels

    private static Button AddButton(Control parent, string text, string key, Action pressed)
    {
        var button = MakeButton(text, 14);
        button.TooltipText = Texts.KeyTooltip(text, key);
        button.CustomMinimumSize = V(96, 40);
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    /// <summary>Ouvre l'écran pour ce combat ; <paramref name="finished"/> est appelé quand il est fini et montré.</summary>
    /// <param name="hero">Le héros de l'attaquant (à gauche).</param>
    /// <param name="defenderHero">Le héros qui se défend (à droite), dans un combat entre héros.</param>
    /// <param name="playerSide">Le camp que dirige le joueur : l'attaquant, ou le défenseur quand il est attaqué.</param>
    public void Open(TacticalBattle battle, string enemy, TerrainType terrain, Action finished, bool auto = false, Hero? hero = null,
        Hero? defenderHero = null, BattleSide playerSide = BattleSide.Attacker)
    {
        _battle = battle;
        _hero = hero;
        _spell = null;
        _playerSide = playerSide;
        _field.DefenderHero = defenderHero;
        _field.PlayerSide = playerSide;
        _log.Clear();
        _unitInfo.Visible = false;
        _field.AttackerHero = hero;
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

    private bool IsPlayerTurn => _battle is { Active: { } active } && active.Side == _playerSide && !_auto && !_field.IsAnimating;

    /// <summary>Le camp que dirige le joueur (l'attaquant, ou le défenseur quand un adversaire l'attaque).</summary>
    private BattleSide _playerSide = BattleSide.Attacker;

    private void OnClicked(Hex hex, Vector2 point)
    {
        if (_battle is not { Active: { } unit } battle || !IsPlayerTurn)
            return;
        // un sort choisi : le clic est sa cible
        if (_spell is { } spell)
        {
            if (spell.Effect == SpellEffect.Teleport)
            {
                // deux clics : la troupe, puis sa destination
                if (_teleported is { } moving)
                {
                    if (TeleportHead(battle, moving, hex) is { } to)
                    {
                        var from = moving.Position;
                        CancelSpell();
                        Player(b => b.Cast(spell, from, to));
                    }
                }
                else if (battle.IsValidTarget(spell, hex))
                {
                    _teleported = battle.UnitAt(hex);
                    _message.Text = Texts.TeleportDestinationPrompt(spell);
                }
                return;
            }
            if (battle.IsValidTarget(spell, hex))
            {
                _spell = null;
                Player(b => b.Cast(spell, hex));
            }
            return;
        }
        if (battle.UnitAt(hex) is { } other && TacticalBattle.AreEnemies(unit, other))
        {
            if (battle.CanShoot(unit, other))
                Player(b => b.Shoot(other));
            else if (battle.AttackPositions(unit, other).OrderBy(h => BattleFieldView.UnitCenter(unit, h).DistanceSquaredTo(point)).Cast<Hex?>().FirstOrDefault() is { } from)
                Player(b => b.Attack(other, from));
            return;
        }
        if (Destination(battle, unit, hex) is { } head)
            Player(b => b.Move(head));
    }

    /// <summary>Où une téléportation met la tête de la troupe si le joueur clique sur cette case (null : impossible).</summary>
    private static Hex? TeleportHead(TacticalBattle battle, BattleUnit unit, Hex hex)
    {
        var heads = battle.TeleportDestinations(unit).ToList();
        if (heads.Contains(hex))
            return hex;
        // une grande créature : la tête qui met son arrière sur cette case
        return heads.Where(h => unit.HexesAt(h).Contains(hex)).Cast<Hex?>().FirstOrDefault();
    }

    /// <summary>
    /// Où va la troupe si le joueur clique sur cette case : la case elle-même, ou pour une grande créature, la tête qui
    /// met son arrière sur cette case. Null si elle ne peut pas y aller.
    /// </summary>
    private static Hex? Destination(TacticalBattle battle, BattleUnit unit, Hex hex)
    {
        var reachable = battle.Reachable(unit);
        if (reachable.ContainsKey(hex) && hex != unit.Position)
            return hex;
        foreach (var head in reachable.Keys)
            if (head != unit.Position && unit.HexesAt(head).Contains(hex))
                return head;
        return null;
    }

    private void Show(System.Collections.Generic.List<BattleAction> actions)
    {
        _field.Show(actions, action =>
        {
            if (Texts.BattleMessage(action) is { } text)
            {
                _message.Text = text;
                _log.Add(text);
            }
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
        if (!_field.IsAnimating && (battle.Active is { } active && active.Side != _playerSide || _auto))
        {
            _wait -= delta;
            if (_wait <= 0)
            {
                BattleAI.PlayTurn(battle);
                Show(battle.TakeLog());
                _wait = _auto ? AiDelay / 2 : AiDelay;
            }
        }
        if (OpenSpellBookWhenReady && CanCastNow)
        {
            OpenSpellBookWhenReady = false;
            if (ChooseSpellWhenReady is { } id && battle.HeroOf(_playerSide)!.Spells.FirstOrDefault(s => s.Id == id) is { } chosen)
            {
                _spell = chosen;
                _message.Text = Texts.SpellTargetPrompt(chosen);
            }
            else
                OpenSpellBook();
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
        _spellButton.Disabled = PlayerHero == null;
        // le sort choisi n'est plus possible (le tour est passé à l'adversaire…) : on l'oublie
        if (_spell is { } chosen && (!mine || !battle.CanCast(chosen)))
            CancelSpell();
        // on ne fuit qu'en attaquant (celui qui se défend n'a nulle part où aller)
        _fleeButton.Disabled = !mine || _playerSide != BattleSide.Attacker;
        _waitButton.Disabled = !mine || unit!.HasWaited;
        _autoButton.Disabled = _auto;

        _field.ActiveUnit = unit;
        // pour une grande créature, ses deux cases à chaque arrivée possible
        _field.Reachable = mine ? battle.Reachable(unit!).Keys.SelectMany(h => unit!.HexesAt(h)).Distinct().ToList() : Array.Empty<Hex>();
        _field.Target = null;
        _field.SpellTargets = Array.Empty<Hex>();
        _field.SpellArea = Array.Empty<Hex>();
        var cursor = CursorKind.Pointer;
        if (mine && _spell is { } spell)
        {
            // choisir la cible d'un sort : les troupes visables en valeur, et ce que toucherait le sort sous la souris
            _field.Reachable = Array.Empty<Hex>();
            cursor = CursorKind.Blocked;
            if (_teleported is { } moving)
            {
                // la destination d'une téléportation : les cases où la troupe tient
                _field.SpellTargets = battle.TeleportDestinations(moving).SelectMany(h => moving.HexesAt(h)).Distinct().ToList();
                if (_field.Hovered is { } over && TeleportHead(battle, moving, over) is { } head)
                {
                    cursor = CursorKind.Move;
                    _field.SpellArea = moving.HexesAt(head).ToList();
                }
            }
            else
            {
                if (spell.Target != SpellTarget.Area)
                    _field.SpellTargets = battle.SpellTargets(spell).ToList();
                if (_field.Hovered is { } aimed && battle.IsValidTarget(spell, aimed))
                {
                    var aimedUnit = battle.UnitAt(aimed);
                    cursor = aimedUnit != null && aimedUnit.Side == _playerSide ? CursorKind.Visit : CursorKind.Attack;
                    _field.SpellArea = spell.Target switch
                    {
                        SpellTarget.Area => new[] { aimed }.Concat(aimed.Neighbours()).Where(Battlefield.InBounds).ToList(),
                        SpellTarget.Chain => battle.AffectedBy(spell, aimed).SelectMany(u => u.Hexes).ToList(),
                        _ => aimedUnit!.Hexes.ToList(),
                    };
                }
            }
        }
        else if (mine && _field.Hovered is { } hovered)
        {
            if (battle.UnitAt(hovered) is { } other && TacticalBattle.AreEnemies(unit!, other))
            {
                bool canHit = battle.CanShoot(unit!, other) || battle.AttackPositions(unit!, other).Any();
                cursor = canHit ? CursorKind.Attack : CursorKind.Blocked;
                if (canHit)
                    _field.Target = hovered;
            }
            else if (Destination(battle, unit!, hovered) != null)
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
        var now = coming.Where(u => _field.ShownCount(u) > 0).ToList();
        var next = played.Where(u => _field.ShownCount(u) > 0).ToList();
        // des cases plus étroites quand il y a beaucoup de troupes, pour que la bande ne déborde pas sur les boutons
        float label = next.Count > 0 ? Px(110) : 0;
        float width = Math.Clamp((_order.Size.X - label - Px(4) * (now.Count + next.Count)) / Math.Max(1, now.Count + next.Count), Px(34), Px(58));
        foreach (var (unit, i) in now.Select((u, i) => (u, i)))
            _order.AddChild(OrderSlot(unit, _field.ShownCount(unit), i == 0, dim: false, width, _playerSide));
        if (next.Count > 0)
        {
            var text = MakeLabel(Texts.BattleNextRound, 12, SoftTextColor);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.CustomMinimumSize = new Vector2(0, OrderHeight);
            _order.AddChild(text);
            foreach (var unit in next)
                _order.AddChild(OrderSlot(unit, _field.ShownCount(unit), false, dim: true, width, _playerSide));
        }
    }

    private static Control OrderSlot(BattleUnit unit, int count, bool active, bool dim, float width, BattleSide playerSide)
    {
        var slot = new PanelContainer { CustomMinimumSize = new Vector2(width, OrderHeight), TooltipText = Texts.Stack(new CreatureStack(unit.Type, count)) };
        var side = unit.Side == playerSide ? MapRenderer.Rgb(40, 60, 120) : MapRenderer.Rgb(110, 40, 30);
        int border = active ? 3 : 1;
        slot.AddThemeStyleboxOverride("panel", Box(side, active ? MapRenderer.Rgb(240, 200, 80) : DimColor, border, border, border, border));
        if (dim)
            slot.Modulate = new Color(1, 1, 1, 0.5f);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        // l'image ×3, ou ×2 si la case est étroite
        if (CreatureImage(unit.Type, width >= 78 ? 3 : 2) is { } picture)
        {
            picture.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            picture.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            picture.StretchMode = TextureRect.StretchModeEnum.KeepCentered;
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
