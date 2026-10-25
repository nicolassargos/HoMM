using System.Linq;
using HoMM.Core;
using HoMM.Game;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les textes affichés au joueur (fichier Game/Texts.cs).</summary>
public class TextsTests
{
    private static CreatureType Type(string id) => TestData.Creatures.Get(id);

    [Theory]
    [InlineData("loup", 3, "Quelques Loups")]
    [InlineData("loup", 15, "Une bande de Loups")]
    [InlineData("archer", 15, "Une bande d'Archers")]
    [InlineData("epeiste", 30, "Beaucoup d'Épéistes")]
    [InlineData("ogre", 1200, "Une légion d'Ogres")]
    public void La_taille_d_un_groupe_de_monstres_est_approximative(string id, int count, string expected) =>
        Assert.Equal(expected, Texts.MonsterGroup(Type(id), count));

    [Theory]
    [InlineData(0, "Mois 1, semaine 1, Lundi")]
    [InlineData(9, "Mois 1, semaine 2, Mercredi")]
    [InlineData(28, "Mois 2, semaine 1, Lundi")]
    public void La_date(int day, string expected) => Assert.Equal(expected, Texts.Date(day));

    [Fact]
    public void Les_pertes()
    {
        Assert.Equal("aucune", Texts.Losses(new CreatureStack[0]));
        Assert.Equal("1 Archer, 12 Piquiers",
            Texts.Losses(new[] { new CreatureStack(Type("archer"), 1), new CreatureStack(Type("piquier"), 12) }));
    }

    [Fact]
    public void Le_journal_raconte_les_visites_du_joueur()
    {
        var state = new GameState(7, TestData.Data);
        var hero = state.CurrentHero!;
        var player = state.Players[0];
        var mine = state.Map.Objects.OfType<Mine>().First(m => m.Resource == ResourceType.Gold);

        Assert.Equal("Mine d'or capturée ! +1000 or par jour.", Texts.Message(mine.Visit(hero), player));
        Assert.Equal("Mine d'or vous appartient déjà.", Texts.Message(mine.Visit(hero), player));
        Assert.Equal("Vous trouvez 7 bois.",
            Texts.Message(new ResourcesFound(hero, mine, ResourceType.Wood, 7), player));
    }

    [Fact]
    public void Le_journal_ignore_les_visites_des_autres_joueurs()
    {
        var state = new GameState(7, TestData.Data);
        var other = new Player("Bleu", state.Map);
        var mine = state.Map.Objects.OfType<Mine>().First();

        Assert.Null(Texts.Message(mine.Visit(state.CurrentHero!), other));
    }

    [Fact]
    public void Le_titre_du_combat_utilise_le_nombre_de_monstres_au_debut()
    {
        var state = new GameState(7, TestData.Data);
        var guard = new MonsterGuard(new Cell(0, 0), Type("loup"), 3);
        var report = new BattleReport { AttackerWon = true, AttackerLosses = new(), DefenderLosses = new() };

        Assert.Equal("Combat contre une bande de Loups",
            Texts.BattleTitle(new BattleFought(state.CurrentHero!, guard, new[] { new CreatureStack(Type("loup"), 15) }, report)));
    }

    private static (GameState State, Town Town) OwnedTown()
    {
        var state = new GameState(7, TestData.Data);
        var town = new Town(new Cell(0, 0), "Testville", "chateau", TestData.Data);
        town.AddBuilding(TestData.Data.Buildings.Get("salle-village"));
        town.AddBuilding(TestData.Data.Buildings.Get("corps-de-garde"));
        town.SetOwner(state.Players[0]);
        return (state, town);
    }

    [Fact]
    public void Le_bouton_de_construction_dit_ce_qui_manque()
    {
        var (state, town) = OwnedTown();
        var training = TestData.Data.Buildings.Get("terrain-entrainement");
        var result = town.CanBuild(training, state.Players[0]);

        Assert.Equal("Requiert : Tour des griffons, Caserne", Texts.BuildStatus(result, town, training));
        Assert.Equal("2500 or", Texts.Cost(TestData.Data.Buildings.Get("hotel-ville").Cost));
        Assert.Equal("Gratuit", Texts.Cost(TestData.Data.Buildings.Get("salle-village").Cost));
    }

    [Fact]
    public void Les_textes_du_recrutement()
    {
        var (state, town) = OwnedTown();
        var piquier = Type("piquier");

        Assert.Equal("Recruter des Piquiers", Texts.RecruitTitle(piquier));
        Assert.Equal("14 disponibles · 60 or l'unité", Texts.RecruitInfo(piquier, 14));
        Assert.Equal("10 Piquiers pour 600 or", Texts.RecruitTotal(piquier, 10));
        Assert.Equal("Ressources insuffisantes.", Texts.CannotRecruit(14, true, 0));
        Assert.Null(Texts.CannotRecruit(14, true, 3));
        Assert.Equal("10 Piquiers recrutés à Testville.",
            Texts.Message(new CreaturesRecruited(town, piquier, 10, false), state.Players[0]));
    }

    [Fact]
    public void Ce_qu_apporte_chaque_batiment()
    {
        var (_, town) = OwnedTown();
        var b = TestData.Data.Buildings;

        Assert.Equal("14 Piquiers disponibles (+14 par semaine)", Texts.BuildingEffect(town, b.Get("corps-de-garde")));
        Assert.Equal("Produit des Archers (+9 par semaine)", Texts.BuildingEffect(town, b.Get("tour-archers")));
        Assert.Equal("+1000 or par jour", Texts.BuildingEffect(town, b.Get("hotel-ville")));
        Assert.Equal("Permet de recruter des héros", Texts.BuildingEffect(town, b.Get("taverne")));
    }
}
