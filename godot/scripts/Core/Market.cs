using System;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Le marché (étape 9, choix de l'utilisateur, 2026-11-07 : les taux sont à revoir en jouant) : on échange une ressource
/// contre une autre, en passant par sa valeur en or. Chaque ressource vaut 1 (or), 100 (bois, minerai) ou 250 (rare) ;
/// on vend à une part de cette valeur et on achète à cette valeur divisée par la même part. La part grandit avec le
/// nombre de marchés du joueur, comme dans Heroes 3 : 20 % avec un, 10 % de plus par marché, 60 % au plus (5 marchés).
/// </summary>
public static class Market
{
    /// <summary>La valeur d'une ressource, en or.</summary>
    public static int Value(ResourceType resource) => resource switch
    {
        ResourceType.Gold => 1,
        _ when ResourceInfo.IsRare(resource) => 250,
        _ => 100,
    };

    /// <summary>La part de la valeur, en dixièmes (2 = 20 %), selon le nombre de marchés ; 0 sans marché.</summary>
    public static int RateTenths(int markets) => markets <= 0 ? 0 : Math.Min(6, 1 + markets);

    /// <summary>Le nombre de marchés du joueur (un par ville qui en a un).</summary>
    public static int Count(Player player) => player.Towns.Count(t => t.BuiltBuildings.Any(b => b.Market));

    /// <summary>Ce que rapporte une unité vendue, en or (l'or lui-même : 1).</summary>
    private static decimal SellValue(ResourceType resource, int tenths) =>
        resource == ResourceType.Gold ? 1 : Value(resource) * tenths / 10m;

    /// <summary>Ce que coûte une unité achetée, en or (l'or lui-même : 1).</summary>
    private static decimal BuyPrice(ResourceType resource, int tenths) =>
        resource == ResourceType.Gold ? 1 : Value(resource) * 10m / tenths;

    /// <summary>
    /// Combien de <paramref name="give"/> il faut donner pour recevoir <paramref name="count"/>
    /// <paramref name="receive"/>, avec ce nombre de marchés (arrondi au-dessus : rien n'est donné en trop).
    /// </summary>
    public static int Cost(ResourceType give, ResourceType receive, int count, int markets)
    {
        int tenths = RateTenths(markets);
        if (tenths == 0 || give == receive || count <= 0)
            return 0;
        return (int)Math.Ceiling(count * BuyPrice(receive, tenths) / SellValue(give, tenths));
    }

    /// <summary>Le plus de <paramref name="receive"/> qu'on peut recevoir en donnant au plus <paramref name="have"/>.</summary>
    public static int MaxReceivable(ResourceType give, ResourceType receive, int have, int markets)
    {
        int tenths = RateTenths(markets);
        if (tenths == 0 || give == receive || have <= 0)
            return 0;
        int count = (int)Math.Floor(have * SellValue(give, tenths) / BuyPrice(receive, tenths));
        // l'arrondi de Cost peut dépasser d'une unité : on vérifie
        while (count > 0 && Cost(give, receive, count, markets) > have)
            count--;
        return count;
    }
}

/// <summary>Un joueur a échangé des ressources au marché : il a donné <c>Given</c> et reçu <c>Received</c>.</summary>
public sealed record ResourcesTraded(Player Player, ResourceType Give, int Given, ResourceType Receive, int Received) : GameEvent;
