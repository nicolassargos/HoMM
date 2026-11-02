using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les fautes dans data/ sont signalées au chargement, avec toutes les erreurs.</summary>
public class DataValidationTests
{
    private static string Creature(string id, int min = 1, int max = 1) =>
        "{\"id\":\"" + id + "\",\"name\":\"X\",\"plural\":\"Xs\",\"faction\":\"f\",\"tier\":1,\"attack\":1,\"defense\":1,"
        + "\"minDamage\":" + min + ",\"maxDamage\":" + max + ",\"hitPoints\":1,\"speed\":1,\"growth\":1,\"commandCost\":1,\"cost\":{\"Gold\":1}}";

    private static string Building(string id, string requires = "", string? dwelling = null) =>
        "{\"id\":\"" + id + "\",\"name\":\"B\",\"faction\":\"f\",\"cost\":{},\"requires\":[" + requires + "],\"dailyGold\":0"
        + (dwelling != null ? ",\"dwelling\":\"" + dwelling + "\"" : "") + "}";

    private static string Creatures(params string[] creatures) => "{\"creatures\":[" + string.Join(",", creatures) + "]}";

    private static string Buildings(params string[] buildings) => "{\"buildings\":[" + string.Join(",", buildings) + "]}";

    private static string LoadError(string creatures, string buildings) =>
        Assert.Throws<FormatException>(() => GameData.FromJson(creatures, buildings)).Message;

    [Fact]
    public void Les_donnees_du_jeu_sont_valides()
    {
        Assert.Empty(DataValidation.Check(TestData.Creatures.All, TestData.Data.Buildings.All));
    }

    [Fact]
    public void Une_creature_en_double_est_signalee()
    {
        var message = LoadError(Creatures(Creature("a"), Creature("a")), Buildings());

        Assert.Contains("créature en double : a", message);
    }

    [Fact]
    public void Un_prerequis_inconnu_est_signale()
    {
        var message = LoadError(Creatures(Creature("a")), Buildings(Building("b", requires: "\"x\"")));

        Assert.Contains("b : prérequis inconnu « x »", message);
    }

    [Fact]
    public void Une_habitation_d_une_creature_inconnue_est_signalee()
    {
        var message = LoadError(Creatures(Creature("a")), Buildings(Building("b", dwelling: "zzz")));

        Assert.Contains("habitation d'une créature inconnue « zzz »", message);
    }

    [Fact]
    public void Des_prerequis_circulaires_sont_signales()
    {
        var message = LoadError(Creatures(Creature("a")),
            Buildings(Building("b", requires: "\"c\""), Building("c", requires: "\"b\"")));

        Assert.Contains("prérequis circulaires", message);
    }

    [Fact]
    public void Des_degats_incoherents_sont_signales()
    {
        var message = LoadError(Creatures(Creature("a", min: 5, max: 2)), Buildings());

        Assert.Contains("a : dégâts invalides (5-2)", message);
    }

    [Fact]
    public void Toutes_les_erreurs_sont_donnees_ensemble()
    {
        var message = LoadError(Creatures(Creature("a", min: 5, max: 2), Creature("a")),
            Buildings(Building("b", dwelling: "zzz")));

        Assert.Contains("créature en double", message);
        Assert.Contains("dégâts invalides", message);
        Assert.Contains("habitation d'une créature inconnue", message);
    }
}
