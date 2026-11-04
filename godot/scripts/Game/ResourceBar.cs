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
    private Label _date = null!;

    /// <summary>Construit la barre, de la largeur donnée.</summary>
    public void Build(float width, float height)
    {
        Position = Vector2.Zero;
        Size = new Vector2(width, height);
        AddThemeStyleboxOverride("panel", Frame(PanelColor));

        var resources = new HBoxContainer { Position = new Vector2(16, 0), Size = new Vector2(width - 260, height) };
        resources.AddThemeConstantOverride("separation", 6);
        AddChild(resources);
        foreach (var resource in ResourceInfo.All)
        {
            resources.AddChild(new ResourceIcon { Resource = resource, CustomMinimumSize = new Vector2(18, height) });
            var label = MakeLabel("", 16, TextColor);
            label.CustomMinimumSize = new Vector2(118, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.MouseFilter = MouseFilterEnum.Stop; // pour son info-bulle
            resources.AddChild(label);
            _amounts[resource] = label;
        }

        _date = MakeLabel("", 16, TextColor);
        _date.HorizontalAlignment = HorizontalAlignment.Right;
        _date.VerticalAlignment = VerticalAlignment.Center;
        _date.Position = new Vector2(width - 238, 0);
        _date.Size = new Vector2(220, height);
        _date.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_date);
    }

    /// <summary>Met à jour les nombres et les info-bulles (appelé à chaque image : très peu coûteux).</summary>
    public void Refresh(GameState state, Player player)
    {
        var income = player.DailyIncome();
        foreach (var (resource, label) in _amounts)
        {
            label.Text = Texts.ResourceAmount(player.Resources[resource], income.GetValueOrDefault(resource));
            label.TooltipText = Texts.ResourceTooltip(player, resource);
        }
        _date.Text = Texts.Date(state.Day);
        _date.TooltipText = Texts.GoalTooltip(state, player);
    }
}
