using System;
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

    /// <summary>Ajouté au nom du terrain quand une route y passe (« Sable, route »).</summary>
    public static string WithRoad => "route";

    public static string Terrain(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => "Herbe",
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
        Obelisk => "Obélisque",
        Mine mine => MineName(mine),
        MonsterGuard guard => MonsterGroup(guard.Type, guard.Count),
        Town town => town.Name,
        _ => obj.GetType().Name,
    };

    /// <summary>L'adversaire d'un combat, tel qu'il était au début : « une bande de Loups », « la garnison de Bellerive ».</summary>
    public static string Enemy(BattleFought battle) => battle.Defender switch
    {
        MonsterGuard guard => LowerFirst(MonsterGroup(guard.Type, battle.DefenderArmy.Sum(s => s.Count))),
        Town town => $"la garnison de {town.Name}",
        var other => LowerFirst(ObjectName(other)),
    };

    /// <summary>L'adversaire d'un combat qui commence : « une bande de Loups », « la garnison de Bellerive ».</summary>
    public static string Opponent(MapObject defender, Army army) => defender switch
    {
        MonsterGuard guard => LowerFirst(MonsterGroup(guard.Type, army.TotalCount)),
        Town town => $"la garnison de {town.Name}",
        var other => LowerFirst(ObjectName(other)),
    };

    // ------------------------------------------------------------- combat tactique

    public static string BattleChoiceTitle => "Combat !";
    public static string BattleChoiceText(string opponent) =>
        $"Vous affrontez {opponent}. Voulez-vous diriger le combat, ou le laisser se dérouler tout seul ?";
    public static string TacticalButton => "Combat tactique";
    public static string QuickButton => "Combat rapide";

    public static string BattleWait => "Attendre";
    public static string BattleDefend => "Se défendre";
    public static string BattleAuto => "Auto";
    public static string BattleFlee => "Fuir";

    public static string BattleTitle(string opponent) => $"Combat contre {opponent}";
    public static string BattleRound(int round) => $"Round {round}";

    /// <summary>« 12 Archers tirent sur 30 Paysans : 18 dégâts, 18 tués. », ou null pour ce qui ne se raconte pas.</summary>
    public static string? BattleMessage(BattleAction action) => action switch
    {
        UnitStruck s => $"{Capitalize(Stack(s.Attacker.Stack))} {Verb(s)} {StackAfter(s)} : {s.Damage} dégâts"
                        + (s.Killed > 0 ? $", {s.Killed} {(s.Killed == 1 ? "tué" : "tués")}." : "."),
        UnitWaited w => $"{Capitalize(Stack(w.Unit.Stack))} {(w.Unit.Count == 1 ? "attend" : "attendent")}.",
        UnitDefended d => $"{Capitalize(Stack(d.Unit.Stack))} {(d.Unit.Count == 1 ? "se défend" : "se défendent")}.",
        AttackerFled fled => $"Le héros s'enfuit ! Pertes : {Losses(fled.Losses)}.",
        BattleEnded { Outcome: BattleOutcome.AttackerWon } => "Victoire !",
        BattleEnded { Outcome: BattleOutcome.DefenderWon } => "Défaite...",
        _ => null,
    };

    private static string Verb(UnitStruck s) => (s.Ranged, s.Retaliation, s.Attacker.Count == 1) switch
    {
        (true, _, true) => "tire sur",
        (true, _, false) => "tirent sur",
        (_, true, true) => "riposte contre",
        (_, true, false) => "ripostent contre",
        (_, _, true) => "attaque",
        _ => "attaquent",
    };

    /// <summary>La cible, avec ce qu'elle comptait avant le coup.</summary>
    private static string StackAfter(UnitStruck s) => Stack(new CreatureStack(s.Target.Type, s.Target.Count + s.Killed));

    /// <summary>« Révolte : 40 Paysans refusent d'obéir… », ou null s'il n'y a pas eu de révolte.</summary>
    public static string? Revolt(BattleReport report) => report.Rebels.Count == 0 ? null
        : $"Révolte : {string.Join(", ", report.Rebels.Select(Stack))} refusent d'obéir et combattent l'ennemi"
          + (report.RebelSurvivors.Count > 0 ? ", les survivants quittent l'armée." : ".");

    public static string BattleTitle(BattleFought battle) => battle.Defender is Town town
        ? $"Assaut de {town.Name}"
        : $"Combat contre {Enemy(battle)}";

    // ------------------------------------------------------------ héros

    public static string StatName(HeroStat stat) => stat switch
    {
        HeroStat.Attack => "attaque",
        HeroStat.Defense => "défense",
        HeroStat.Power => "puissance",
        HeroStat.Knowledge => "savoir",
        _ => throw new ArgumentOutOfRangeException(nameof(stat)),
    };

    /// <summary>Les caractéristiques du héros, sur quatre lignes.</summary>
    public static string HeroStats(Hero hero) =>
        $"Niveau {hero.Level} · expérience {hero.Experience} / {Hero.ExperienceForLevel(hero.Level + 1)}\n"
        + $"Attaque {hero.Attack} · défense {hero.Defense}\n"
        + $"Puissance {hero.Power} · savoir {hero.Knowledge} · charisme {hero.Charisma}\n"
        + $"Commandement max. par troupe : {Command.Limit(hero.Charisma)}";

    /// <summary>L'info-bulle d'un artefact : son nom et ses bonus.</summary>
    public static string ArtifactTooltip(ArtifactType artifact) => $"{artifact.Name} : {Bonus(artifact)}";

    /// <summary>« 12 Archers », avec une marque si la troupe risque de se révolter.</summary>
    public static string ArmyLine(CreatureStack stack, int charisma) =>
        Stack(stack) + (Command.IsOverLimit(stack.Type, stack.Count, charisma) ? "  (révolte ?)" : "");

    /// <summary>Avertissement avant de dépasser la limite de commandement d'une troupe.</summary>
    public static string CommandWarning(int limit) =>
        $"Attention : au-delà de {limit} points de commandement, cette troupe risque de se révolter au combat.";

    public static string LevelUp(HeroLeveledUp levelUp) =>
        $"{levelUp.Hero.Name} passe au niveau {levelUp.After.Level} ! +1 {StatName(levelUp.Stat)}, +1 charisme.";

    /// <summary>Le titre de la fenêtre de choix : « Niveau 2 : choisissez une caractéristique ».</summary>
    public static string LevelChoiceTitle(HeroLevelChoice choice) =>
        $"Niveau {choice.Level} : choisissez une caractéristique";

    public static string LevelChoiceText(HeroLevelChoice choice) =>
        $"{choice.Hero.Name} progresse. Le charisme augmente de 1 dans tous les cas.";

    /// <summary>Le bouton d'un choix : « Attaque (actuelle : 2) ».</summary>
    public static string StatChoice(Hero hero, HeroStat stat) =>
        $"{Capitalize(StatName(stat))} (actuelle : {StatValue(hero, stat)})";

    private static int StatValue(Hero hero, HeroStat stat) => stat switch
    {
        HeroStat.Attack => hero.Attack,
        HeroStat.Defense => hero.Defense,
        HeroStat.Power => hero.Power,
        HeroStat.Knowledge => hero.Knowledge,
        _ => throw new ArgumentOutOfRangeException(nameof(stat)),
    };

    /// <summary>Le titre de la fenêtre de niveau : « Niveau 2 atteint ! ».</summary>
    public static string LevelUpTitle(HeroLeveledUp levelUp) => $"Niveau {levelUp.After.Level} atteint !";

    /// <summary>
    /// Le contenu de la fenêtre de niveau : chaque caractéristique avec sa valeur d'avant et d'après,
    /// et le commandement maximal par troupe qui en découle.
    /// </summary>
    public static string LevelUpSummary(HeroLeveledUp levelUp)
    {
        var before = levelUp.Before;
        var after = levelUp.After;
        return string.Join("\n", new[]
        {
            $"{levelUp.Hero.Name} passe au niveau {after.Level}.",
            "",
            Change("Attaque", before.Attack, after.Attack),
            Change("Défense", before.Defense, after.Defense),
            Change("Puissance", before.Power, after.Power),
            Change("Savoir", before.Knowledge, after.Knowledge),
            Change("Charisme", before.Charisma, after.Charisma),
            Change("Commandement max. par troupe", Command.Limit(before.Charisma), Command.Limit(after.Charisma)),
        });
    }

    /// <summary>« Attaque : 1 → 2 (+1) », ou « Défense : 1 (inchangée) ».</summary>
    private static string Change(string label, int before, int after) => before == after
        ? $"{label} : {after} (inchangée)"
        : $"{label} : {before} → {after} (+{after - before})";

    // -------------------------------------------------------------- villes

    /// <summary>Un coût : « 2500 or, 10 bois », ou « Gratuit ».</summary>
    public static string Cost(IReadOnlyDictionary<ResourceType, int> cost) =>
        cost.Count == 0 ? "Gratuit" : string.Join(", ", cost.Select(c => $"{c.Value} {Resource(c.Key)}"));

    /// <summary>Le texte du bouton de construction, selon ce que disent les règles.</summary>
    public static string BuildStatus(BuildResult result, Town town, BuildingType building) => result switch
    {
        BuildResult.Ok => "Construire",
        BuildResult.AlreadyBuilt => "Construit",
        BuildResult.AlreadyBuiltToday => "Un bâtiment par jour",
        BuildResult.MissingRequirement => "Requiert : " + string.Join(", ",
            town.Buildings.Where(b => building.Requires.Contains(b.Id) && !town.Has(b.Id)).Select(b => b.Name)),
        BuildResult.NotEnoughResources => "Ressources insuffisantes",
        BuildResult.NotOwner => "Ville ennemie",
        _ => result.ToString(),
    };

    /// <summary>Ce qu'apporte un bâtiment, en une ligne.</summary>
    public static string BuildingEffect(Town town, BuildingType building) => building switch
    {
        { Dwelling: not null } when town.DwellingCreature(building) is { } creature => town.Has(building.Id)
            ? Dwelling(creature, town.Available.GetValueOrDefault(creature))
            : $"Produit des {creature.Plural} (+{creature.Growth} par semaine)",
        { DailyGold: > 0 } => $"+{building.DailyGold} or par jour",
        { Tavern: true } => "Permet de recruter des héros",
        _ => "",
    };

    /// <summary>Ce que produit une habitation : « 14 Paysans disponibles (+14 par semaine) ».</summary>
    public static string Dwelling(CreatureType creature, int available) =>
        $"{available} {Creature(creature, available)} disponible{(available > 1 ? "s" : "")} (+{creature.Growth} par semaine)";

    public static string RecruitTitle(CreatureType type) => $"Recruter des {type.Plural}";

    /// <summary>« 14 disponibles · 60 or l'unité ».</summary>
    public static string RecruitInfo(CreatureType type, int available) =>
        $"{available} disponible{(available > 1 ? "s" : "")} · {Cost(type.Cost)} l'unité";

    /// <summary>« 10 Paysans pour 600 or ».</summary>
    public static string RecruitTotal(CreatureType type, int count) =>
        $"{count} {Creature(type, count)} pour {Cost(type.Cost.ToDictionary(c => c.Key, c => c.Value * count))}";

    /// <summary>Pourquoi on ne peut recruter aucune créature, ou null si on peut.</summary>
    public static string? CannotRecruit(int available, bool hasRoom, int affordable) =>
        available == 0 ? "Aucune créature disponible cette semaine."
        : !hasRoom ? "Pas de place dans la garnison ni chez le héros."
        : affordable == 0 ? "Ressources insuffisantes."
        : null;

    /// <summary>Le texte du bouton de la taverne, selon ce que disent les règles.</summary>
    public static string HireStatus(HireResult result) => result switch
    {
        HireResult.Ok => $"Recruter un héros ({GameState.HeroCost} or)",
        HireResult.TownOccupied => "Un héros est déjà dans la ville",
        HireResult.NotEnoughGold => $"Il faut {GameState.HeroCost} or",
        HireResult.TooManyHeroes => $"{GameState.MaxHeroesPerPlayer} héros au maximum",
        HireResult.NoHeroLeft => "Plus aucun héros à recruter",
        HireResult.NoTavern => "Il faut une taverne",
        HireResult.NotOwner => "Ville ennemie",
        _ => result.ToString(),
    };

    public const string TransferHint = "Cliquez sur une troupe pour la faire passer entre la garnison et le héros.";

    public static string GameOverTitle(GameOutcome outcome) => outcome == GameOutcome.Victory ? "Victoire" : "Défaite";

    public static string GameOverText(GameEnded end) => end.Reason switch
    {
        GameEndReason.GuardsCleared => "Tous les monstres ont été vaincus. La région est à vous !",
        GameEndReason.TownsCaptured => "Toutes les villes sont à vous. La région est à vous !",
        GameEndReason.HeroesLost => "Votre dernier héros est tombé. La partie est perdue.",
        GameEndReason.TreasureFound => "Vous avez déterré le Plastron des Rois : la région vous reconnaît pour son roi !",
        _ => "Le temps est écoulé avant la victoire. La partie est perdue.",
    };

    /// <summary>Ce qu'il faut faire pour gagner, avec la date limite s'il y en a une.</summary>
    public static string Objective(GameRules rules)
    {
        // plusieurs façons de gagner : la première accomplie l'emporte
        string goal = Capitalize(string.Join(", ou ", GameRules.Choices.Where(rules.Has).Select(c => Goal(c).ToLowerInvariant())));
        return rules.DayLimit is { } limit ? $"{goal}, avant la fin de {Date(limit)}" : goal;
    }

    /// <summary>Une façon de gagner, telle qu'on la coche au début de la partie.</summary>
    public static string Goal(VictoryCondition condition) => condition switch
    {
        VictoryCondition.ClearGuards => "Vaincre tous les monstres",
        VictoryCondition.CaptureTowns => "Prendre toutes les villes",
        _ => "Trouver le trésor enfoui",
    };

    public static string DayLimitChoice => $"Limite : gagner avant la fin de {Date(GameRules.StandardDayLimit)}";
    public static string StartButton => "Commencer";

    // ------------------------------------------------------------------ trésor

    public static string TreasureMapTitle => "Carte au trésor";
    public static string DigButton => "Creuser (F)";
    public static string TreasureMapButton => "Carte au trésor (M)";
    public static string DigNeedsFullDay => "Pour creuser, il faut une journée entière de mouvement (au début du tour).";
    public static string TreasureGone => "Le trésor a déjà été déterré.";

    public static string TreasureMapText(int found, int total) => total == 0
        ? "Il n'y a pas d'obélisque sur cette carte."
        : $"Morceaux trouvés : {found} sur {total}. Chaque obélisque en dévoile un. Creusez (touche F) là où est la croix.";

    /// <summary>« +5 attaque, +5 défense, +5 charisme »</summary>
    public static string Bonus(ArtifactType artifact) => string.Join(", ", new[]
    {
        (artifact.Attack, "attaque"), (artifact.Defense, "défense"), (artifact.Power, "puissance"),
        (artifact.Knowledge, "savoir"), (artifact.Charisma, "charisme"),
    }.Where(b => b.Item1 != 0).Select(b => $"+{b.Item1} {b.Item2}"));

    public static string TreasureDugTitle => "Le trésor !";

    public static string TreasureDugText(TreasureDug dug) =>
        $"{dug.Hero.Name} déterre le {dug.Artifact.Name}, l'armure légendaire des anciens rois.\n{Bonus(dug.Artifact)}.";

    public static string RulesTitle => "Conditions de la partie";

    /// <summary>« au nord-est », « à l'ouest », « au centre »…</summary>
    public static string Where(Compass direction) => direction switch
    {
        Compass.North => "au nord",
        Compass.NorthEast => "au nord-est",
        Compass.East => "à l'est",
        Compass.SouthEast => "au sud-est",
        Compass.South => "au sud",
        Compass.SouthWest => "au sud-ouest",
        Compass.West => "à l'ouest",
        Compass.NorthWest => "au nord-ouest",
        _ => "au centre",
    };

    // ------------------------------------------------------------- sauvegarde

    public static string GameSaved => "Partie sauvegardée.";
    public static string SaveFailed => "Impossible d'enregistrer la partie.";
    public static string GameLoaded => "Partie chargée.";

    public static string OptionsTitle => "Options";
    public static string OptionsText => "Une seule sauvegarde : sauvegarder remplace la précédente.";
    public static string SaveButton => "Sauvegarder (F5)";
    public static string LoadButton => "Charger (F9)";
    public static string CloseButton => "Fermer";
    public static string NoSave => "Aucune partie sauvegardée.";
    public static string LoadFailed => "Cette sauvegarde ne peut pas être chargée (fichier abîmé ou d'une autre version du jeu).";

    public static string RulesText => "Comment gagner cette partie ? Cochez une ou plusieurs façons : la première accomplie l'emporte. Perdre tous ses héros reste une défaite.";

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

        TownCaptured captured => $"Vous prenez {captured.Town.Name} !",
        TownEntered entered => $"{entered.Hero.Name} entre dans {entered.Town.Name}.",
        BuildingBuilt built when built.Town.Owner != viewer => null,
        BuildingBuilt built => $"{built.Building.Name} : construit à {built.Town.Name}.",
        HeroHired hired when hired.Hero.Owner != viewer => null,
        HeroHired hired => $"{hired.Hero.Name} rejoint vos rangs à {hired.Town.Name} !",
        CreaturesRecruited recruited when recruited.Town.Owner != viewer => null,
        CreaturesRecruited recruited =>
            $"{recruited.Count} {Creature(recruited.Type, recruited.Count)} recruté{(recruited.Count > 1 ? "s" : "")} à {recruited.Town.Name}"
            + (recruited.ToHero ? " (garnison pleine : ils rejoignent le héros)." : "."),

        BattleFought battle when battle.Hero.Owner != viewer => null,
        BattleFought { Report.AttackerWon: true } battle => Join(
            Revolt(battle.Report),
            $"Victoire contre {Enemy(battle)} ! Pertes : {Losses(battle.Report.AttackerLosses)}. +{battle.Report.Experience} expérience."),
        BattleFought battle => Join(Revolt(battle.Report), $"{battle.Hero.Name} a été vaincu par {Enemy(battle)}."),
        HeroLeveledUp levelUp when levelUp.Hero.Owner != viewer => null,
        HeroLeveledUp levelUp => LevelUp(levelUp),

        DayStarted { NewWeek: true } day => $"Une nouvelle semaine commence, les monstres sont plus nombreux ! {Date(day.Day)}.",
        DayStarted day => $"Nouveau jour : {Date(day.Day)}.",
        IncomeReceived income when income.Player == viewer => $"Revenus : {Income(income.Income)}.",
        TurnStarted turn => $"Au tour du joueur {turn.Player.Name}.",
        RulesChosen { Treasure: { } where } chosen =>
            $"Objectif : {Objective(chosen.Rules)}. Le trésor est enfoui {Where(where)} de la carte : les obélisques en dévoilent la carte (touche M).",
        RulesChosen chosen => $"Objectif : {Objective(chosen.Rules)}.",
        GameEnded { Reason: GameEndReason.TreasureFound } => "Le trésor est à vous. Victoire !",
        ObeliskVisited { FirstVisit: true } => "Un obélisque ! Un morceau de la carte au trésor apparaît.",
        ObeliskVisited => "Vous avez déjà lu cet obélisque.",
        NothingDug dug => $"{dug.Hero.Name} creuse… il n'y a rien ici.",
        TreasureDug dug => $"{dug.Hero.Name} déterre le {dug.Artifact.Name} ! {Bonus(dug.Artifact)}.",
        GameEnded { Reason: GameEndReason.GuardsCleared } => "La région est libérée de ses monstres. Victoire !",
        GameEnded { Reason: GameEndReason.TownsCaptured } => "Toutes les villes sont à vous. Victoire !",
        GameEnded { Reason: GameEndReason.HeroesLost } => "Vous n'avez plus de héros. Défaite...",
        GameEnded => "Il est trop tard pour gagner. Défaite...",
        _ => null,
    };

    // ------------------------------------------------------------- outils

    /// <summary>Assemble des phrases qui ne sont pas vides, séparées par un espace.</summary>
    private static string Join(params string?[] parts) => string.Join(" ", parts.Where(p => p != null));

    public static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    public static string LowerFirst(string s) => s.Length == 0 ? s : char.ToLower(s[0]) + s[1..];

    private static string Of(string word) =>
        "AEIOUYÉÈÊaeiouyéèê".Contains(word[0]) ? $"d'{word}" : $"de {word}";
}
