using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// La disposition d'un écran, réglée à la souris par l'utilisateur (choix du 2026-11-21) : la page de
/// <c>tools/ui/disposition.py</c> écrit <c>data/layouts/{écran}.json</c>, la place (en pixels de l'écran de 1920 × 1080),
/// la taille, l'agrandissement (par demi : ×1,5 garde les pixels de l'art ×2 entiers) et l'affichage de chaque
/// élément nommé. Un élément absent du fichier garde la place que lui donne le code.
/// </summary>
public sealed class ScreenLayout
{
    private record Placement(float X, float Y, float? W, float? H, float Scale, bool Hidden);

    private readonly string _screen;
    private readonly List<(string Id, Control Control, bool Resizable)> _parts = new();
    private readonly Dictionary<string, Placement> _placed = new();

    public ScreenLayout(string screen)
    {
        _screen = screen;
        string path = $"res://data/layouts/{screen}.json";
        if (!Godot.FileAccess.FileExists(path))
            return;
        using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));
        foreach (var element in json.RootElement.GetProperty("elements").EnumerateObject())
        {
            var e = element.Value;
            float? Optional(string name) => e.TryGetProperty(name, out var v) ? v.GetSingle() : null;
            _placed[element.Name] = new Placement(e.GetProperty("x").GetSingle(), e.GetProperty("y").GetSingle(),
                Optional("w"), Optional("h"), Optional("scale") ?? 1, e.TryGetProperty("hidden", out var h) && h.GetBoolean());
        }
    }

    /// <summary>Nomme un élément de l'écran ; <paramref name="resizable"/> : la page peut aussi changer sa taille.</summary>
    public T Add<T>(string id, T control, bool resizable = false) where T : Control
    {
        _parts.Add((id, control, resizable));
        return control;
    }

    /// <summary>L'utilisateur a placé cet élément (le code ne doit plus le déplacer).</summary>
    public bool Placed(string id) => _placed.ContainsKey(id);

    /// <summary>Met chaque élément nommé à la place choisie (après la construction de l'écran).</summary>
    public void Apply()
    {
        foreach (var (id, control, resizable) in _parts)
        {
            if (!_placed.TryGetValue(id, out var p))
                continue;
            control.Visible = !p.Hidden;
            // un élément retiré garde la place du code (la page de disposition l'y montre, sans gêner les autres)
            if (p.Hidden)
                continue;
            control.Position = new Vector2(Snap(p.X), Snap(p.Y));
            if (resizable && p.W is { } w && p.H is { } h)
                control.Size = new Vector2(Snap(w), Snap(h));
            control.Scale = new Vector2(p.Scale, p.Scale);
        }
    }

    /// <summary>
    /// Pour la page de disposition : la place et la taille de chaque élément à l'écran (<c>elements.json</c>), puis cache
    /// les éléments (pour une capture du fond seul).
    /// </summary>
    public void Dump(string file)
    {
        var elements = _parts.Select(p =>
        {
            // la taille à l'écran, agrandissement compris
            var c = p.Control;
            return new Dictionary<string, object>
            {
                ["id"] = p.Id, ["x"] = c.GlobalPosition.X, ["y"] = c.GlobalPosition.Y, ["w"] = c.Size.X * c.Scale.X,
                ["h"] = c.Size.Y * c.Scale.Y, ["scale"] = c.Scale.X, ["resizable"] = p.Resizable, ["hidden"] = !c.Visible,
            };
        });
        using var f = Godot.FileAccess.Open(file, Godot.FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(new { screen = _screen, elements }, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Cache ou remontre tous les éléments nommés (capture du fond seul, puis de chaque élément).</summary>
    public void ShowParts(bool visible)
    {
        foreach (var (_, control, _) in _parts)
            control.Visible = visible;
    }
}
