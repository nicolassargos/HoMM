using System.Collections.Generic;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// La barre des ressources (étape 7), en haut de la carte et de la ville, toujours à la même place : chaque ressource
/// avec son revenu du jour (d'où il vient au survol), et la date (l'objectif de la partie au survol).
/// </summary>
public partial class ResourceBar : Panel
{
    private readonly Dictionary<ResourceType, Label> _amounts = new();
    private Label? _date;

    /// <summary>Construit la barre, de la largeur et de la hauteur données (en pixels de l'écran).</summary>
    public void Build(float width, float height)
    {
        Position = Vector2.Zero;
        Size = new Vector2(width, height);
        AddThemeStyleboxOverride("panel", Frame(PanelColor));

        var resources = new HBoxContainer { Position = new Vector2(Px(16), 0), Size = new Vector2(width - Px(260), height) };
        resources.AddThemeConstantOverride("separation", (int)Px(6));
        AddChild(resources);
        foreach (var resource in ResourceInfo.All)
        {
            resources.AddChild(new ResourceIcon { Resource = resource, CustomMinimumSize = new Vector2(Px(18), height) });
            var label = MakeLabel("", 16, TextColor);
            // l'or a des nombres plus longs (« 12500 (+850) ») ; les autres tiennent dans moins de place (7 ressources)
            label.CustomMinimumSize = new Vector2(Px(resource == ResourceType.Gold ? 118 : 72), 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.MouseFilter = MouseFilterEnum.Stop; // pour son info-bulle
            resources.AddChild(label);
            _amounts[resource] = label;
        }

        _date = MakeLabel("", 16, TextColor);
        _date.HorizontalAlignment = HorizontalAlignment.Right;
        _date.VerticalAlignment = VerticalAlignment.Center;
        _date.Position = new Vector2(width - Px(238), 0);
        _date.Size = new Vector2(Px(220), height);
        _date.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_date);
    }

    /// <summary>
    /// Une grille au lieu d'une barre (la ville, comme Heroes 2 : en bas à droite) : deux ressources par rangée, la grande
    /// icône ×2 au-dessus de son nombre, l'or seul sur la dernière rangée. Sans la date (la barre d'état de la ville la
    /// montre). Sa hauteur vient des rangées.
    /// </summary>
    public void BuildColumn(float width)
    {
        const int icon = 64; // les icônes de 32 pixels, ×2
        float text = TextSize(16), cell = icon + text + Px(2), top = Px(6);
        var rows = new[]
        {
            new[] { ResourceType.Wood, ResourceType.Ore },
            new[] { ResourceType.Mercury, ResourceType.Sulfur },
            new[] { ResourceType.Crystal, ResourceType.Gems },
            new[] { ResourceType.Gold },
        };
        Position = Vector2.Zero;
        Size = new Vector2(width, top * 2 + cell * rows.Length);
        AddThemeStyleboxOverride("panel", Frame(PanelColor));

        float y = top;
        foreach (var row in rows)
        {
            float cellWidth = (width - Px(8)) / row.Length;
            for (int i = 0; i < row.Length; i++)
            {
                float x = Px(4) + i * cellWidth;
                AddChild(new ResourceIcon
                {
                    Resource = row[i], Large = true, Position = new Vector2(Snap(x + (cellWidth - icon) / 2), y),
                    Size = new Vector2(icon, icon), MouseFilter = MouseFilterEnum.Ignore,
                });
                var label = MakeLabel("", 16, TextColor);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.Position = new Vector2(x, y + icon);
                label.Size = new Vector2(cellWidth, text);
                label.MouseFilter = MouseFilterEnum.Pass; // pour son info-bulle ; le clic va à la colonne (le marché, en ville)
                AddChild(label);
                _amounts[row[i]] = label;
            }
            y += cell;
        }
    }

    /// <summary>Met à jour les nombres et les info-bulles (appelé à chaque image : très peu coûteux).</summary>
    public void Refresh(GameState state, Player player)
    {
        var income = player.DailyIncome();
        foreach (var (resource, label) in _amounts)
        {
            label.Text = Texts.ResourceAmount(player.Resources.GetValueOrDefault(resource), income.GetValueOrDefault(resource));
            label.TooltipText = Texts.ResourceTooltip(player, resource);
        }
        if (_date == null)
            return;
        _date.Text = Texts.Date(state.Day);
        _date.TooltipText = Texts.GoalTooltip(state, player);
    }
}
