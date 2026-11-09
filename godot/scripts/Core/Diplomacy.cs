using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Pourquoi un gardien qui propose de rejoindre le héros ne peut pas le faire (ou <see cref="None"/>).</summary>
public enum JoinRefusal
{
    None,

    /// <summary>La troupe dépasserait le commandement du héros : elle ne lui obéirait pas.</summary>
    Command,

    /// <summary>Le héros n'a pas de quoi payer.</summary>
    Price,

    /// <summary>Plus de place dans l'armée (ni place libre, ni troupe de la même créature).</summary>
    Slot,
}

/// <summary>
/// Un gardien propose de rejoindre le héros : son prix (vide : gratuit) et, s'il ne peut pas, pourquoi
/// (<see cref="JoinRefusal"/>).
/// </summary>
public sealed record JoinOffer(IReadOnlyDictionary<ResourceType, int> Price, JoinRefusal Refusal)
{
    public bool IsFree => Price.Values.All(v => v == 0);
    public bool CanAccept => Refusal == JoinRefusal.None;
}

/// <summary>Ce que fait le héros face à un gardien qui propose de le rejoindre, ou qui s'enfuit.</summary>
public enum EncounterChoice { Fight, Join, LetFlee }

/// <summary>
/// Avant un combat contre un gardien de la carte : il propose de rejoindre le héros (<paramref name="Offer"/>, avec la
/// Diplomatie) et / ou s'enfuit (<paramref name="Flees"/>, devant une armée bien plus forte). La partie attend le choix
/// (<see cref="GameState.ResolveEncounter"/>).
/// </summary>
public sealed record GuardEncounter(Hero Hero, MonsterGuard Guard, JoinOffer? Offer, bool Flees) : GameEvent;

/// <summary>Le gardien a rejoint l'armée du héros (contre ce prix).</summary>
public sealed record GuardJoined(Hero Hero, CreatureType Type, int Count, IReadOnlyDictionary<ResourceType, int> Price) : GameEvent;

/// <summary>Le héros a laissé fuir le gardien.</summary>
public sealed record GuardFled(Hero Hero, CreatureType Type, int Count) : GameEvent;

/// <summary>
/// La Diplomatie (compétence de l'arbre Aventure, rangée 3) et la fuite des gardiens trop faibles (décidées avec
/// l'utilisateur, 2026-11-09). Les forces se comparent en or (le prix des créatures : à prix égal, un combat est égal).
/// <para>
/// <b>Rejoindre</b> : il faut la Diplomatie. Chaque <b>degré</b> rend un gardien plus facile à convaincre : le rang de la
/// compétence (1 à 3), +1 pour un gardien de la faction du héros, +1 par palier de charisme (20, 30, 40 : comme le
/// moral). Il faut une armée qui vaut au moins <see cref="Ratios"/> fois la sienne ; il coûte <see cref="PricePercent"/>
/// de son prix. Il doit tenir dans le commandement du héros, avec la troupe de la même créature s'il y en a une.
/// Tout ou rien.
/// </para>
/// <para><b>Fuir</b> : devant une armée qui vaut <see cref="FleeRatio"/> fois la sienne, le gardien s'enfuit (pour tout
/// héros) ; le héros le laisse partir (ni combat ni expérience) ou le poursuit (un combat ordinaire).</para>
/// </summary>
public static class Diplomacy
{
    /// <summary>L'armée du héros doit valoir au moins tant de fois le gardien, au degré 1, 2, 3, 4, 5 et plus.</summary>
    public static readonly double[] Ratios = { 3, 2, 1.5, 1.25, 1 };

    /// <summary>La part du prix à payer, au degré 1, 2, 3 et plus.</summary>
    public static readonly int[] PricePercent = { 100, 50, 0 };

    /// <summary>Devant une armée qui vaut tant de fois la sienne, un gardien s'enfuit.</summary>
    public const double FleeRatio = 10;

    /// <summary>La valeur d'une armée en or (le prix de ses créatures).</summary>
    public static int Value(IEnumerable<CreatureStack> stacks) => Battle.ExperienceFor(stacks);

    /// <summary>
    /// Le degré du héros face à cette créature : 0 sans la Diplomatie, sinon son rang, +1 si la créature est de sa
    /// faction, +1 par palier de charisme.
    /// </summary>
    public static int Degree(Hero hero, CreatureType type)
    {
        int rank = hero.SkillRank(Skills.Diplomacy);
        if (rank == 0)
            return 0;
        return rank + (hero.Class?.Faction is { Length: > 0 } faction && faction == type.Faction ? 1 : 0) + Morale.Aura(hero.Charisma);
    }

    /// <summary>L'armée qu'il faut au moins, en fois la valeur du gardien, à ce degré (1 et plus).</summary>
    public static double RatioAt(int degree) => Ratios[Math.Clamp(degree, 1, Ratios.Length) - 1];

    public static int PricePercentAt(int degree) => PricePercent[Math.Clamp(degree, 1, PricePercent.Length) - 1];

    /// <summary>Ce qui se passe avant le combat contre ce gardien, ou null s'il n'y a qu'à combattre.</summary>
    public static GuardEncounter? Encounter(Hero hero, MonsterGuard guard)
    {
        int theirs = Math.Max(1, Value(guard.Army.Stacks));
        int ours = Value(hero.Army.Stacks);
        bool flees = ours >= FleeRatio * theirs;
        JoinOffer? offer = null;
        int degree = Degree(hero, guard.Type);
        if (degree > 0 && ours >= RatioAt(degree) * theirs)
            offer = Offer(hero, guard, PricePercentAt(degree));
        return offer == null && !flees ? null : new GuardEncounter(hero, guard, offer, flees);
    }

    /// <summary>Le prix demandé (arrondi au-dessus) et ce qui empêcherait d'accepter.</summary>
    private static JoinOffer Offer(Hero hero, MonsterGuard guard, int percent)
    {
        int count = guard.Count;
        var price = guard.Type.Cost.Where(c => c.Value > 0)
            .ToDictionary(c => c.Key, c => (int)Math.Ceiling(c.Value * (long)count * percent / 100.0));
        foreach (var key in price.Where(p => p.Value == 0).Select(p => p.Key).ToList())
            price.Remove(key);
        // avec la troupe de la même créature qu'il rejoindrait
        int together = count + hero.Army.Stacks.Where(s => s.Type == guard.Type).Sum(s => s.Count);
        var refusal = Command.Cost(guard.Type, together) > hero.CommandLimit ? JoinRefusal.Command
            : !hero.Army.CanReceive(guard.Type) ? JoinRefusal.Slot
            : price.Any(p => hero.Owner.Resources.GetValueOrDefault(p.Key) < p.Value) ? JoinRefusal.Price
            : JoinRefusal.None;
        return new JoinOffer(price, refusal);
    }
}
