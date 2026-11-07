using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Le marché (étape 9) : on vend à 20 % de la valeur avec un marché, 10 % de plus par marché, 60 % au plus ; on achète à
/// la valeur divisée par la même part. Valeurs : or 1, bois et minerai 100, ressource rare 250.
/// </summary>
public class MarcheTests
{
    [Fact]
    public void Les_taux_dependent_du_nombre_de_marches()
    {
        Assert.Equal(0, Market.RateTenths(0));
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 6 }, Enumerable.Range(1, 6).Select(Market.RateTenths));

        // un marché : 1 bois rapporte 20 or ; 1 bois coûte 500 or ; une gemme coûte 62,5 bois, donc 63
        Assert.Equal(20, Market.MaxReceivable(ResourceType.Wood, ResourceType.Gold, 1, 1));
        Assert.Equal(500, Market.Cost(ResourceType.Gold, ResourceType.Wood, 1, 1));
        Assert.Equal(63, Market.Cost(ResourceType.Wood, ResourceType.Gems, 1, 1));
        // cinq marchés : 60 or le bois vendu, 167 or le bois acheté
        Assert.Equal(60, Market.MaxReceivable(ResourceType.Wood, ResourceType.Gold, 1, 5));
        Assert.Equal(167, Market.Cost(ResourceType.Gold, ResourceType.Wood, 1, 5));
        // jamais plus que ce qu'on a
        Assert.All(Enumerable.Range(1, 200), have =>
            Assert.True(Market.Cost(ResourceType.Ore, ResourceType.Crystal, Market.MaxReceivable(ResourceType.Ore, ResourceType.Crystal, have, 2), 2) <= have));
    }

    private static (GameState State, Player Player, Town Town) WithMarket()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small);
        var player = state.Players[0];
        var town = player.Towns.Single();
        player.Resources[ResourceType.Gold] = 10_000;
        player.Resources[ResourceType.Wood] = 50;
        return (state, player, town);
    }

    [Fact]
    public void Sans_marche_on_ne_peut_pas_echanger()
    {
        var (state, player, _) = WithMarket();

        Assert.Equal(0, state.Trade(ResourceType.Wood, ResourceType.Gold, 10));
        Assert.Equal(50, player.Resources[ResourceType.Wood]);
    }

    [Fact]
    public void On_echange_au_marche_au_taux_du_joueur()
    {
        var (state, player, town) = WithMarket();
        Assert.Equal(BuildResult.Ok, state.Build(town, "marche"));
        state.TakeEvents();
        int gold = player.Resources[ResourceType.Gold];

        // 50 bois rapportent 1000 or ; on en demande 400 : il faut 20 bois
        Assert.Equal(20, state.Trade(ResourceType.Wood, ResourceType.Gold, 400));

        Assert.Equal(25, player.Resources[ResourceType.Wood]); // 50 - 5 (le marché) - 20
        Assert.Equal(gold + 400, player.Resources[ResourceType.Gold]);
        Assert.Contains(state.TakeEvents(), e => e is ResourcesTraded { Given: 20, Received: 400 });
        // pas plus que ce qu'on peut payer, ni deux fois la même ressource
        Assert.Equal(0, state.Trade(ResourceType.Wood, ResourceType.Gems, 1));
        Assert.Equal(0, state.Trade(ResourceType.Gold, ResourceType.Gold, 10));
    }

    [Fact]
    public void L_adversaire_bati_un_marche_et_s_en_sert()
    {
        int withMarket = 0, traded = 0;
        foreach (int seed in new[] { 3, 7, 11 })
        {
            var state = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1);
            state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
            state.Players[0].Heroes[0].Army.Add(TestData.Creatures.Get("ange"), 50); // le joueur ne se fait pas prendre
            var rival = state.Players[1];
            bool tradedHere = false;
            for (int day = 0; day < 42 && state.Outcome == GameOutcome.Playing; day++)
            {
                state.EndTurn();
                while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
                {
                    ComputerPlayer.PlayTurn(state);
                    state.EndTurn();
                }
                tradedHere |= state.TakeEvents().Any(e => e is ResourcesTraded t && t.Player == rival);
            }
            withMarket += Market.Count(rival) > 0 ? 1 : 0;
            traded += tradedHere ? 1 : 0;
        }
        Assert.True(withMarket >= 2, $"{withMarket} adversaires sur 3 ont un marché");
        Assert.True(traded >= 1, $"{traded} adversaires sur 3 s'en sont servis");
    }
}
