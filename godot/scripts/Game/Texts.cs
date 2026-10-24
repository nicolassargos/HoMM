using System.Collections.Generic;
using System.Linq;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Tous les textes affichés au joueur. Les règles (Core) ne produisent aucun texte :
/// c'est ici qu'on met en mots les objets et les événements.
/// N'utilise pas Godot, pour pouvoir être testé avec les règles.
/// </summary>
public static class Texts
{
    private static readonly string[] DayNames = { "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche" };

    public const string NotEnoughMovement = "Plus assez de points de mouvement aujourd'hui.";

    public static string Welcome(Hero hero) => $"Bienvenue, {hero.Name} ! Chassez les monstres de la région.";

    // ------------------------------------------------------------------ noms

    public static string Resource(ResourceType resource) => resource switch
    {
        ResourceType.Gold => "or",
        ResourceType.Wood => "bois",
        ResourceType.Ore => "minerai",
        ResourceType.Gems => "gemmes",
        _ => resource.ToString(),
    };

    public static string Terrain(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => "Herbe",
        TerrainType.Road => "Route",
        TerrainType.Forest => "Forêt",
        TerrainType.Sand => "Sable",
        TerrainType.Swamp => "Marais",
        TerrainType.Water => "Eau",
        TerrainType.Mountain => "Montagne",
        _ => terrain.ToString(),
    };

    /// <summary>« Mois 1, semaine 2, Mercredi » (jour 0 = premier lundi).</summary>
    public static string Date(int day) => $"Mois {day / 28 + 1}, semaine {day / 7 % 4 + 1}, {DayNames[day % 7]}";

    public static string Creature(CreatureType type, int count) => count == 1 ? type.Name : type.Plural;

    /// <summary>« 12 Archers ».</summary>
    public static string Stack(CreatureStack stack) => $"{stack.Count} {Creature(stack.Type, stack.Count)}";

    public static string Losses(IReadOnlyList<CreatureStack> losses) =>
        losses.Count == 0 ? "aucune" : string.Join(", ", losses.Select(Stack));

    /// <summary>Les revenus « +1000 or, +2 bois ».</summary>
    public static string Income(IReadOnlyDictionary<ResourceType, int> income) =>
        string.Join(", ", income.Select(i => $"+{i.Value} {Resource(i.Key)}"));

    public static string MineName(Mine mine) => mine.Resource switch
    {
        ResourceType.Gold => "Mine d'or",
        ResourceType.Wood => "Scierie",
        ResourceType.Ore => "Mine de minerai",
        ResourceType.Gems => "Mine de gemmes",
        _ => "Mine",
    };

    /// <summary>« Quelques Loups », « Une bande d'Archers »… Le nombre exact reste caché, comme dans Heroes.</summary>
    public static string MonsterGroup(CreatureType type, int count) => count switch
    {
        < 5 => $"Quelques {type.Plural}",
        < 10 => $"Plusieurs {type.Plural}",
        < 20 => $"Une bande {Of(type.Plural)}",
        < 50 => $"Beaucoup {Of(type.Plural)}",
        < 100 => $"Une horde {Of(type.Plural)}",
        < 250 => $"Une multitude {Of(type.Plural)}",
        < 500 => $"Une nuée {Of(type.Plural)}",
        < 1000 => $"Des milliers {Of(type.Plural)}",
        _ => $"Une légion {Of(type.Plural)}",
    };

    public static string ObjectName(MapObject obj) => obj switch
    {
        ResourcePile pile => $"Tas de {Resource(pile.Resource)}",
        TreasureChest => "Coffre au trésor",
        Mine mine => MineName(mine),
        MonsterGuard guard => MonsterGroup(guard.Type, guard.Count),
        _ => obj.GetType().Name,
    };

    public static string BattleTitle(BattleFought battle) =>
        $"Combat contre {LowerFirst(MonsterGroup(battle.Guard.Type, battle.EnemyCount))}";

    public static string GameOverTitle(GameOutcome outcome) => outcome == GameOutcome.Victory ? "Victoire" : "Défaite";

    public static string GameOverText(GameOutcome outcome) => outcome == GameOutcome.Victory
        ? "Tous les monstres ont été vaincus. La région est à vous !"
        : "Votre dernier héros est tombé. La partie est perdue.";

    // ------------------------------------------------------------- journal

    /// <summary>
    /// La ligne du journal pour un événement, vu par le joueur <paramref name="viewer"/>,
    /// ou null si l'événement ne s'affiche pas dans le journal.
    /// </summary>
    public static string? Message(GameEvent e, Player viewer) => e switch
    {
        ObjectVisited visit when visit.Hero.Owner != viewer => null,
        ResourcesFound { Object: TreasureChest } found => $"Un coffre au trésor ! +{found.Amount} {Resource(found.Resource)}.",
        ResourcesFound found => $"Vous trouvez {found.Amount} {Resource(found.Resource)}.",
        MineCaptured captured =>
            $"{MineName(captured.Mine)} capturée ! +{captured.Mine.DailyProduction} {Resource(captured.Mine.Resource)} par jour.",
        MineAlreadyOwned owned => $"{MineName(owned.Mine)} vous appartient déjà.",

        BattleFought battle when battle.Hero.Owner != viewer => null,
        BattleFought { Report.AttackerWon: true } battle =>
            $"Victoire contre {LowerFirst(MonsterGroup(battle.Guard.Type, battle.EnemyCount))} ! Pertes : {Losses(battle.Report.AttackerLosses)}.",
        BattleFought battle =>
            $"{battle.Hero.Name} a été vaincu par {LowerFirst(MonsterGroup(battle.Guard.Type, battle.EnemyCount))}.",

        DayStarted { NewWeek: true } day => $"Une nouvelle semaine commence, les monstres sont plus nombreux ! {Date(day.Day)}.",
        DayStarted day => $"Nouveau jour : {Date(day.Day)}.",
        IncomeReceived income when income.Player == viewer => $"Revenus : {Income(income.Income)}.",
        TurnStarted turn => $"Au tour du joueur {turn.Player.Name}.",
        GameEnded { Outcome: GameOutcome.Victory } => "La région est libérée de ses monstres. Victoire !",
        GameEnded => "Vous n'avez plus de héros. Défaite...",
        _ => null,
    };

    // ------------------------------------------------------------- outils

    public static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    public static string LowerFirst(string s) => s.Length == 0 ? s : char.ToLower(s[0]) + s[1..];

    private static string Of(string word) =>
        "AEIOUYÉÈÊaeiouyéèê".Contains(word[0]) ? $"d'{word}" : $"de {word}";
}
