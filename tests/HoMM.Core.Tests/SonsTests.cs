using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Le catalogue des sons (data/sounds.json) : chaque fichier nommé existe dans godot/audio/, et le jeu sait le lire.</summary>
public class SonsTests
{
    [Fact]
    public void Chaque_son_du_catalogue_existe()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "sounds.json"));
        using var document = JsonDocument.Parse(json);
        var audio = AudioFolder();
        var files = new[] { "music", "sounds", "ambience" }
            .SelectMany(section => document.RootElement.GetProperty(section).EnumerateObject())
            .SelectMany(entry => entry.Value.EnumerateArray().Select(f => (entry.Name, File: f.GetString()!)))
            .ToList();
        Assert.NotEmpty(files);
        Assert.All(files, f =>
        {
            Assert.True(File.Exists(Path.Combine(audio, f.File)), $"{f.Name} : {f.File} absent de godot/audio/");
            Assert.True(f.File.EndsWith(".ogg") || f.File.EndsWith(".wav"), $"{f.Name} : {f.File} ni .ogg ni .wav");
        });
    }

    [Fact]
    public void Les_sons_que_le_jeu_demande_sont_au_catalogue()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "sounds.json"));
        using var document = JsonDocument.Parse(json);
        var music = document.RootElement.GetProperty("music").EnumerateObject().Select(e => e.Name).ToHashSet();
        var sounds = document.RootElement.GetProperty("sounds").EnumerateObject().Select(e => e.Name).ToHashSet();
        Assert.Superset(music, new[] { "menu", "map", "town", "battle" }.ToHashSet());
        Assert.Superset(sounds,
            new[] { "click", "pickup", "chest", "capture", "build", "level-up", "new-week", "victory", "defeat" }.ToHashSet());
    }

    /// <summary>Le dossier godot/audio/, en remontant depuis celui des tests.</summary>
    private static string AudioFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var audio = Path.Combine(dir.FullName, "godot", "audio");
            if (Directory.Exists(audio))
                return audio;
        }
        throw new DirectoryNotFoundException("godot/audio/ introuvable");
    }
}
