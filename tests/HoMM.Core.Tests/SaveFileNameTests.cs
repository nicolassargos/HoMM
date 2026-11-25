using HoMM.Game;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Le nom d'un fichier de sauvegarde est sûr sur Windows.</summary>
public class SaveFileNameTests
{
    [Theory]
    [InlineData("Partie 1", "Partie 1")]
    [InlineData("  nom?.  ", "nom-")]
    [InlineData("A/B\\C", "A-B-C")]
    [InlineData("NUL", "_NUL")]
    [InlineData("con", "_con")]
    [InlineData("PRN.json", "_PRN.json")]
    [InlineData("aux.txt", "_aux.txt")]
    [InlineData("COM1", "_COM1")]
    [InlineData("COM9.sav", "_COM9.sav")]
    [InlineData("LPT1", "_LPT1")]
    [InlineData("LPT9.backup", "_LPT9.backup")]
    [InlineData("COM10", "COM10")]
    [InlineData("LPT10", "LPT10")]
    [InlineData("CONSOLE", "CONSOLE")]
    [InlineData("NULA.txt", "NULA.txt")]
    [InlineData("CON .txt", "_CON .txt")]
    public void Le_nom_est_nettoye_et_les_noms_reserves_sont_proteges(string asked, string expected)
    {
        Assert.Equal(expected, SaveFileName.FromSaveName(asked));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("....")]
    [InlineData("  . .  ")]
    public void Un_nom_vide_apres_nettoyage_reste_vide(string asked)
    {
        Assert.Equal(string.Empty, SaveFileName.FromSaveName(asked));
    }
}
