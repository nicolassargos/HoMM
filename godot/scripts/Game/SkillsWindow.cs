using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Les arbres de compétences d'un héros (étape 9) : ses points à dépenser, puis les trois arbres côte à côte (Aventure,
/// Commandement, Magie), rangée par rangée. Chaque compétence est un bouton à son nom et à son rang, avec l'effet du
/// rang suivant ; un clic dépense un point (<see cref="GameState.LearnSkill"/>). Une rangée fermée dit combien de
/// points il faut encore dépenser dans l'arbre.
/// </summary>
public partial class SkillsWindow : GameDialog
{
    public SkillsWindow() { } // pour Godot

    private readonly GameState _state = null!;
    private readonly Hero _hero = null!;
    private readonly Action? _changed;
    private readonly VBoxContainer _list = new();

    /// <param name="changed">Appelé après chaque point dépensé (l'écran du héros se met à jour).</param>
    public SkillsWindow(GameState state, Hero hero, Action? changed = null)
    {
        _state = state;
        _hero = hero;
        _changed = changed;
        Title = Texts.SkillsTitle(hero);
        OkButtonText = Texts.CloseButton;
        _list.AddThemeConstantOverride("separation", (int)Px(10));
        AddChild(_list);
        Confirmed += QueueFree;
        Canceled += QueueFree;
        Fill();
    }

    /// <summary>Remplit la fenêtre (de nouveau après chaque point dépensé).</summary>
    private void Fill()
    {
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }
        _list.AddChild(MakeLabel(Texts.SkillPointsLine(_hero.SkillPoints, _hero.Skills.Count), 15, TextColor));
        var trees = new HBoxContainer();
        trees.AddThemeConstantOverride("separation", (int)Px(20));
        foreach (var tree in Enum.GetValues<SkillTree>())
            trees.AddChild(Tree(tree));
        _list.AddChild(trees);
    }

    private Control Tree(SkillTree tree)
    {
        var column = new VBoxContainer { CustomMinimumSize = V(340, 0) };
        column.AddThemeConstantOverride("separation", (int)Px(6));
        column.AddChild(MakeLabel(Texts.SkillTreeName(tree), 17, TextColor));
        int spent = _hero.PointsIn(tree);
        column.AddChild(MakeLabel(Texts.SkillTreePoints(spent), 12, SoftTextColor));
        var skills = _state.Data.Skills.InTree(tree).ToList();
        foreach (var row in skills.Select(s => s.Row).Distinct().OrderBy(r => r))
        {
            int needed = Skills.PointsToOpenRow(row);
            if (row > 1)
                column.AddChild(MakeLabel(Texts.SkillRowLine(row, needed, spent >= needed), 12, spent >= needed ? SoftTextColor : MapRenderer.Rgb(200, 140, 120)));
            foreach (var skill in skills.Where(s => s.Row == row))
                column.AddChild(SkillEntry(skill));
        }
        return column;
    }

    private Control SkillEntry(SkillType skill)
    {
        int rank = _hero.Skills.GetValueOrDefault(skill);
        var entry = new VBoxContainer();
        entry.AddThemeConstantOverride("separation", (int)Px(2));
        var button = FixedButton(Texts.SkillButton(skill, rank), 14, V(340, 34));
        button.CustomMinimumSize = V(340, 34); // dans une colonne, sa taille vient de là
        var result = _hero.CanLearn(skill);
        button.Disabled = result != LearnSkillResult.Ok;
        button.TooltipText = result == LearnSkillResult.Ok ? Texts.SkillLearnTooltip : Texts.LearnSkillReason(result);
        button.Pressed += () =>
        {
            if (_state.LearnSkill(_hero, skill) != LearnSkillResult.Ok)
                return;
            Fill();
            _changed?.Invoke();
        };
        entry.AddChild(button);
        var effect = MakeLabel(Texts.SkillNextLine(skill, rank), 12, SoftTextColor);
        effect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        effect.CustomMinimumSize = V(340, 0);
        entry.AddChild(effect);
        return entry;
    }

    /// <summary>Montre la fenêtre au milieu de l'écran (après l'avoir ajoutée à l'arbre).</summary>
    public void Open() => PopupCentered(new Vector2I((int)Px(1120), 0));
}
