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
    public static string BattleNextRound => "Round suivant :";

    /// <summary>« 12 Archers tirent sur 30 Paysans : 18 dégâts, 18 tués. », ou null pour ce qui ne se raconte pas.</summary>
    public static string? BattleMessage(BattleAction action) => action switch
    {
        UnitStruck s => $"{Capitalize(Stack(s.Attacker.Stack))} {Verb(s)} {StackAfter(s)} : {s.Damage} dégâts"
                        + (s.Killed > 0 ? $", {s.Killed} {(s.Killed == 1 ? "tué" : "tués")}." : "."),
        UnitWaited w => $"{Capitalize(Stack(w.Unit.Stack))} {(w.Unit.Count == 1 ? "attend" : "attendent")}.",
        UnitRegenerated r => $"{Capitalize(Stack(r.Unit.Stack))} {(r.Unit.Count == 1 ? "se régénère" : "se régénèrent")}.",
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

    /// <summary>L'info-bulle d'un artefact : son nom et ses bonus.</summary>
    public static string ArtifactTooltip(ArtifactType artifact) => $"{artifact.Name} : {Bonus(artifact)}";

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

    /// <summary>« 2 → 3 » : la caractéristique avant et après ce choix de niveau.</summary>
    public static string StatChange(Hero hero, HeroStat stat) => $"{StatValue(hero, stat)} → {StatValue(hero, stat) + 1}";

    /// <summary>Ce que le niveau donne dans tous les cas : un point de charisme, et le commandement qui en découle.</summary>
    public static string CharismaGain(Hero hero) =>
        $"Charisme : {hero.Charisma} → {hero.Charisma + 1} (commande {Command.Limit(hero.Charisma + 1)} points par troupe).";

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

    /// <summary>Combien on peut en recruter (place et ressources comprises).</summary>
    public static string Affordable(int max) => $"Vous pouvez en recruter : {max}";

    public static string RecruitDestination => "Les recrues vont dans la garnison ; si elle est pleine, chez le héros présent.";

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


    public static string GameOverTitle => "Fin de la partie";

    public static string GameOverHeadline(GameOutcome outcome) => outcome == GameOutcome.Victory ? "Victoire !" : "Défaite…";

    /// <summary>« Partie finie le jour 84 (Mois 3, semaine 4, Dimanche) ».</summary>
    public static string GameOverDay(int day) => $"Partie finie le jour {day + 1} ({Date(day)}).";

    /// <summary>Les chiffres de la partie du joueur, sur deux lignes.</summary>
    public static string GameOverStatistics(Player player)
    {
        var s = player.Statistics;
        string heroes = player.Heroes.Count == 0 ? "" : $" · meilleur héros : niveau {player.Heroes.Max(h => h.Level)}";
        return $"Combats gagnés : {s.BattlesWon} · villes prises : {s.TownsCaptured}{heroes}\n"
            + $"Créatures ennemies tuées : {s.CreaturesKilled} · créatures perdues : {s.CreaturesLost}";
    }

    public static string NewGameButton => "Nouvelle partie";
    public static string ReviewMapButton => "Revoir la carte";

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

    // ------------------------------------------------------------------ panneau de la carte (étape 7)

    /// <summary>« 2500 (+250) » : le stock et le revenu du jour.</summary>
    public static string ResourceAmount(int stock, int income) => income > 0 ? $"{stock} (+{income})" : $"{stock}";

    /// <summary>L'info-bulle d'une ressource : son revenu par jour, et d'où il vient.</summary>
    public static string ResourceTooltip(Player player, ResourceType resource)
    {
        var lines = new List<string>();
        int towns = resource == ResourceType.Gold ? player.Towns.Sum(t => t.DailyGold) : 0;
        var mines = player.Mines.Where(m => m.Resource == resource).ToList();
        int total = towns + mines.Sum(m => m.DailyProduction);
        lines.Add($"{Capitalize(Resource(resource))} : {(total > 0 ? $"+{total}" : "rien")} par jour");
        if (towns > 0)
            lines.Add($"Villes ({player.Towns.Count}) : +{towns}");
        if (mines.Count > 0)
            lines.Add($"Mines ({mines.Count}) : +{mines.Sum(m => m.DailyProduction)}");
        if (total == 0)
            lines.Add(resource == ResourceType.Gold ? "Prenez une ville ou une mine d'or." : "Prenez une mine pour en produire.");
        return string.Join("\n", lines);
    }

    /// <summary>L'info-bulle de la date : l'objectif de la partie et où l'on en est.</summary>
    public static string GoalTooltip(GameState state, Player viewer)
    {
        var lines = new List<string> { $"Objectif : {Objective(state.Rules)}." };
        if (state.Rules.Has(VictoryCondition.ClearGuards))
            lines.Add($"Monstres restants : {state.Map.Objects.OfType<MonsterGuard>().Count()}");
        if (state.Rules.Has(VictoryCondition.CaptureTowns))
            lines.Add($"Villes à vous : {viewer.Towns.Count} / {state.Towns.Count()}");
        if (state.Rules.Has(VictoryCondition.FindTreasure))
        {
            var pieces = state.TreasureMapPieces(viewer);
            lines.Add($"Carte au trésor : {pieces.Count(p => p)} morceaux sur {pieces.Count}");
        }
        if (state.Rules.DayLimit is { } limit)
            lines.Add($"Jours restants : {Math.Max(0, limit - state.Day + 1)}");
        return string.Join("\n", lines);
    }

    public static string HeroesHeader => "Héros";
    public static string TownsHeader => "Villes";
    public static string ArmyHeader => "Armée";
    public static string NoHero => "Aucun héros";
    public static string NoTown => "aucune";

    /// <summary>Sous le nom d'une ville de la liste : son revenu.</summary>
    public static string TownIncome(Town town) => $"+{town.DailyGold} or";

    /// <summary>« Niveau 2 · 1200 / 3000 » (expérience).</summary>
    public static string HeroLevelLine(Hero hero) =>
        $"Niveau {hero.Level} · {hero.Experience} / {Hero.ExperienceForLevel(hero.Level + 1)}";

    /// <summary>L'info-bulle d'un héros de la liste.</summary>
    public static string HeroTooltip(Hero hero) =>
        $"{hero.Name} · niveau {hero.Level}\nMouvement {hero.Movement} / {hero.MaxMovement}\n"
        + string.Join(", ", hero.Army.Stacks.Select(Stack));

    /// <summary>Abréviation d'une caractéristique, pour la fiche du héros (« Att »).</summary>
    public static string StatShort(HeroStat? stat) => stat switch
    {
        HeroStat.Attack => "Att",
        HeroStat.Defense => "Déf",
        HeroStat.Power => "Pui",
        HeroStat.Knowledge => "Sav",
        _ => "Cha",
    };

    /// <summary>Ce que fait une caractéristique (null : le charisme).</summary>
    public static string StatTooltip(HeroStat? stat, Hero hero) => stat switch
    {
        HeroStat.Attack => $"Attaque {hero.Attack} : s'ajoute à l'attaque de chaque troupe du héros.",
        HeroStat.Defense => $"Défense {hero.Defense} : s'ajoute à la défense de chaque troupe du héros.",
        HeroStat.Power => $"Puissance {hero.Power} : la force des sorts (la magie viendra plus tard).",
        HeroStat.Knowledge => $"Savoir {hero.Knowledge} : les points de sort (la magie viendra plus tard).",
        _ => $"Charisme {hero.Charisma} : le héros commande au plus {Command.Limit(hero.Charisma)} points par troupe "
             + "(nombre × coût de la créature). Au-delà, la troupe risque de se révolter au combat.",
    };

    /// <summary>L'info-bulle d'une troupe : ses caractéristiques, ses capacités et son commandement.</summary>
    public static string StackTooltip(CreatureStack stack, int? charisma)
    {
        var t = stack.Type;
        var lines = new List<string>
        {
            Stack(stack),
            $"Attaque {t.Attack} · défense {t.Defense} · dégâts {t.MinDamage}–{t.MaxDamage}",
            $"Points de vie {t.HitPoints} · vitesse {t.Speed}",
        };
        if (t.Abilities.Count > 0)
            lines.Add(string.Join(" · ", t.Abilities.Select(AbilityName)));
        if (charisma is { } c)
        {
            lines.Add($"Commandement : {Command.Cost(t, stack.Count)} / {Command.Limit(c)} points");
            if (Command.IsOverLimit(t, stack.Count, c))
                lines.Add("Trop nombreuse pour le charisme du héros : elle risque de se révolter au combat.");
        }
        return string.Join("\n", lines);
    }

    /// <summary>Le nom d'une capacité (liste « abilities » de creatures.json).</summary>
    public static string AbilityName(string ability) => ability switch
    {
        Ability.Shoots => "tire",
        Ability.NoRangePenalty => "tir sans pénalité de distance",
        Ability.Flies => "vole",
        Ability.DoubleRetaliation => "riposte deux fois",
        Ability.NoRetaliation => "frappe sans riposte",
        Ability.Breath => "souffle",
        Ability.RangedResistance => "résiste aux tirs",
        Ability.Charge => "charge",
        Ability.QuickStart => "rapide au premier round",
        Ability.DoubleAttack => "frappe deux fois",
        Ability.ShortRange => "tir court",
        Ability.Crushing => "coup écrasant",
        Ability.Regenerates => "se régénère",
        Ability.TwoHex => "grande créature",
        _ => ability,
    };

    public static string NextHeroButton => "Héros suivant";
    public static string NextHeroShort => "Suivant";
    public static string TreasureShort => "Trésor";
    public static string WalkTooltip => "Avancer sur le chemin tracé";
    public static string CenterTooltip => "Centrer la carte sur le héros";
    public static string DigTooltip => "Creuser ici (il faut une journée entière de mouvement)";
    public static string OptionsTooltip => "Sauvegarder, charger";
    public static string WalkButton => "Avancer";
    public static string CenterButton => "Centrer";
    public static string DigAction => "Creuser";
    public static string TreasureMapAction => "Carte au trésor";
    public static string OptionsButton => "Options";
    public static string EndTurnButton => "Fin du tour";

    public static string LogButton => "Journal";
    public static string LogTitle => "Journal des messages";
    public static string LogEmpty => "Aucun message pour l'instant.";

    /// <summary>La ligne du terrain survolé : terrain, route, coût, et les jours de marche s'il est accessible.</summary>
    public static string CellLine(TerrainType terrain, bool road, int? cost, int? days)
    {
        string text = Terrain(terrain) + (road ? $", {WithRoad}" : "");
        text += cost is { } c ? $" · déplacement {c}" : " · infranchissable";
        if (days is { } d)
            text += d <= 1 ? " · atteint aujourd'hui" : $" · {d} jours de marche";
        return text;
    }

    /// <summary>Le danger estimé d'un combat, d'après la part de l'armée perdue (null : défaite).</summary>
    public static string Danger(double? lost) => lost switch
    {
        null => "danger : mortel !",
        < 0.05 => "danger : faible",
        < 0.25 => $"danger : modéré (pertes ≈ {lost:P0})",
        < 0.6 => $"danger : élevé (pertes ≈ {lost:P0})",
        _ => $"danger : très élevé (pertes ≈ {lost:P0})",
    };

    public static string Owned => "à vous";
    public static string EnemyOwned => "ennemie";
    public static string Free => "libre";
    public static string Neutral => "neutre";
    public static string Guarded => "gardé par un monstre";
    public static string GarrisonOf(string danger) => $"garnison : {danger}";

    // ------------------------------------------------------------------ écran du héros et royaume (étape 7)

    public static string KingdomButton => "Royaume";
    public static string HeroScreenHint => "Clic : l'écran du héros";
    public static string ExperienceWord => "Expérience";
    public static string MovementWord => "Mouvement";
    public static string LevelLine(Hero hero) => $"Niveau {hero.Level}";
    public static string StatFullName(HeroStat? stat) => stat is { } s ? Capitalize(StatName(s)) : "Charisme";

    /// <summary>Sous chaque caractéristique de l'écran du héros : ce qu'elle fait, en quelques mots.</summary>
    public static string StatEffect(HeroStat? stat, Hero hero) => stat switch
    {
        HeroStat.Attack => "+ à l'attaque de chaque troupe",
        HeroStat.Defense => "+ à la défense de chaque troupe",
        HeroStat.Power => "force des sorts (plus tard)",
        HeroStat.Knowledge => "points de sort (plus tard)",
        _ => $"commande {Command.Limit(hero.Charisma)} points par troupe",
    };

    public static string ArtifactsHeader => "Artefacts";
    public static string NoArtifact => "Aucun artefact pour l'instant.";
    public static string MagicHeader => "Magie";
    public static string MagicLater =>
        "La magie viendra plus tard : les points de sort, le livre de sorts et les compétences du héros prendront place ici.";

    /// <summary>« 30 / 100 points » : le commandement d'une troupe.</summary>
    public static string CommandPoints(CreatureStack stack, int charisma) =>
        $"{Command.Cost(stack.Type, stack.Count)} / {Command.Limit(charisma)} pts";

    public static string KingdomTitle => "Le royaume";
    public static string KingdomHeroes(int count) =>
        $"Héros ({count} / {GameState.MaxHeroesPerPlayer}) · clic : le choisir · double clic : son écran";
    public static string KingdomTowns => "Villes · clic : ouvrir la ville · ● : on peut y construire aujourd'hui";
    public static string KingdomMines => "Mines et revenus · clic : centrer la carte sur une mine";
    public static string KingdomNoTown => "Aucune ville : prenez-en une pour recruter et construire.";

    /// <summary>Le détail d'un héros au survol, dans le royaume.</summary>
    public static string HeroDetails(Hero hero) =>
        $"{hero.Name} · niveau {hero.Level}\n"
        + $"Attaque {hero.Attack} · défense {hero.Defense} · puissance {hero.Power} · savoir {hero.Knowledge} · charisme {hero.Charisma}\n"
        + $"Mouvement {hero.Movement} / {hero.MaxMovement}\n"
        + $"Armée : {(hero.Army.IsEmpty ? "aucune" : string.Join(", ", hero.Army.Stacks.Select(Stack)))}";

    /// <summary>Le détail d'une ville au survol, dans le royaume.</summary>
    public static string TownDetails(Town town)
    {
        var available = town.Available.Where(a => a.Value > 0).Select(a => $"{a.Value} {Creature(a.Key, a.Value)}").ToList();
        return $"{town.Name} · +{town.DailyGold} or par jour\n"
            + $"Construction : {(town.BuiltToday ? "déjà faite aujourd'hui" : "possible aujourd'hui")}\n"
            + $"À recruter : {(available.Count == 0 ? "rien" : string.Join(", ", available))}\n"
            + $"Garnison : {(town.Garrison.IsEmpty ? "aucune" : string.Join(", ", town.Garrison.Stacks.Select(Stack)))}";
    }

    /// <summary>Dans la bande des mines : « mines 2 · villes 1 » pour l'or, « mines 1 » sinon.</summary>
    public static string MineCounts(Player player, ResourceType resource)
    {
        int mines = player.Mines.Count(m => m.Resource == resource);
        return resource == ResourceType.Gold && player.Towns.Count > 0 ? $"mines {mines} · villes {player.Towns.Count}" : $"mines {mines}";
    }

    public static string PerDay(int amount) => amount > 0 ? $"+{amount} par jour" : "rien";

    /// <summary>Où sont les mines d'une ressource (au survol).</summary>
    public static string MinesWhere(Player player, ResourceType resource)
    {
        var mines = player.Mines.Where(m => m.Resource == resource).ToList();
        return mines.Count == 0 ? $"Aucune mine {Of(Resource(resource))}."
            : string.Join("\n", mines.Select(m => $"{MineName(m)} en ({m.Position.X}, {m.Position.Y}) : +{m.DailyProduction} par jour"));
    }

    // ------------------------------------------------------------------ écran de ville (étape 7)

    public static string CancelButton => "Annuler";
    public static string QuitTown => "Quitter (Échap)";

    /// <summary>Sous le nom de la ville : son revenu et si l'on peut encore construire aujourd'hui.</summary>
    public static string TownHeader(Town town) =>
        $"revenu +{town.DailyGold} or par jour · construction : {(town.BuiltToday ? "déjà faite aujourd'hui" : "possible aujourd'hui")}";

    public static string BuildButton => "Construire";
    public static string RecruitButton => "Recruter";
    public static string TavernButton => "Taverne";
    public static string GarrisonHeader => "Garnison";
    public static string VisitingHero(Hero hero) => $"{hero.Name} (en visite)";
    public static string NoVisitor => "Aucun héros dans la ville";
    public static string DragHint =>
        "Glisser une troupe : la déplacer, l'échanger ou la fusionner.\nMaj en lâchant, ou clic droit : la partager.\nUn clic : la faire passer de l'autre côté.";
    public static string GiveAllToHero => "Tout donner au héros";
    public static string LeaveAllInGarrison => "Tout laisser en garnison";

    /// <summary>Au survol d'un bâtiment de la vue de la ville : son nom, ce qu'il fait, et ce qu'un clic y fait.</summary>
    public static string BuildingTooltip(Town town, BuildingType building)
    {
        string effect = BuildingEffect(town, building);
        string click = building.Dwelling != null ? "Clic : recruter" : building.Tavern ? "Clic : la taverne" : building.DailyGold > 0 ? "Clic : construire" : "";
        return string.Join("\n", new[] { building.Name, effect, click }.Where(s => s.Length > 0));
    }

    public static string BuildTitle(Town town) => $"Construire à {town.Name}";
    public static string ToBuild => "À construire (un bâtiment par jour)";
    public static string BuiltHeader => "Construits";
    public static string NothingToBuild => "Tous les bâtiments sont construits.";
    public static string RecruitWindowTitle(Town town) => $"Recruter à {town.Name}";
    public static string RecruitMore => "Recruter…";
    public static string RecruitAllButton(int count) => $"Tout ({count})";
    public static string ToBuildFirst(BuildingType dwelling) => $"{dwelling.Name}\nà construire";
    public static string GoBuild => "Construire…";

    /// <summary>« niveau 3 · vole » : le niveau d'une créature et ses capacités.</summary>
    public static string CreatureLevel(CreatureType type) =>
        string.Join(" · ", new[] { $"niveau {type.Tier}" }.Concat(type.Abilities.Select(AbilityName)));

    /// <summary>Les caractéristiques d'une créature, sur deux lignes.</summary>
    public static string CreatureStats(CreatureType type) =>
        $"Att {type.Attack} · Déf {type.Defense} · dégâts {type.MinDamage}–{type.MaxDamage}\nPV {type.HitPoints} · vitesse {type.Speed}";

    /// <summary>« 28 disponibles (+28 par semaine) ».</summary>
    public static string AvailableLine(CreatureType type, int available) =>
        $"{available} disponible{(available > 1 ? "s" : "")} (+{type.Growth} par semaine)";

    public static string PriceLine(CreatureType type) => $"Prix : {Cost(type.Cost)}";

    public static string TavernTitle => "Taverne";
    public static string TavernText => $"Deux héros se proposent. Chacun coûte {GameState.HeroCost} or et arrive avec quelques paysans.";
    public static string HireButton => "Recruter";
    public static string NoTavern => "Cette ville n'a pas encore de taverne.";

    public static string SplitTitle(CreatureType type) => $"Partager les {type.Plural}";
    public static string SplitText(CreatureType type, int moved, int total) =>
        $"{moved} {Creature(type, moved)} d'un côté, {total - moved} de l'autre.";

    // ------------------------------------------------------------------ écran de combat et bilan (étape 7)

    /// <summary>Le texte d'un bouton du combat avec sa touche : « Attendre (W) ».</summary>
    public static string WithKey(string text, string key) => $"{text} ({key})";

    public static string HeroBonus(Hero hero) => $"Attaque +{hero.Attack} · défense +{hero.Defense}";
    public static string SpellBookLater => "La magie viendra plus tard.";
    public static string BattleLogButton => "Journal";
    public static string BattleLogTitle => "Journal du combat";

    /// <summary>La fiche d'une troupe au combat, sur quelques lignes (ses caractéristiques, bonus du héros compris).</summary>
    public static string UnitStats(BattleUnit unit) =>
        $"Attaque {unit.Attack} · défense {unit.Defense}\n"
        + $"Dégâts {unit.Type.MinDamage}–{unit.Type.MaxDamage} · vitesse {unit.Type.Speed}\n"
        + $"PV de tête {unit.TopHitPoints} / {unit.Type.HitPoints}";

    public static string UnitAbilities(BattleUnit unit) =>
        unit.Type.Abilities.Count == 0 ? "Pas de capacité" : Capitalize(string.Join(", ", unit.Type.Abilities.Select(AbilityName)));

    public static string UnitState(BattleUnit unit) =>
        $"Ripostes : {unit.RetaliationsLeft}" + (unit.IsDefending ? " · se défend" : "");


    /// <summary>Le titre du bilan d'un combat.</summary>
    public static string ReportTitle(BattleReport report) =>
        report.Fled ? "Fuite" : report.AttackerWon ? "Victoire !" : "Défaite…";

    /// <summary>« contre 12 Loups · 4 rounds ».</summary>
    public static string ReportSubtitle(BattleFought battle) =>
        $"{BattleTitle(battle)} · {battle.Report.Rounds} round{(battle.Report.Rounds > 1 ? "s" : "")}";

    public static string YourLosses => "Vos pertes";
    public static string EnemyLosses => "Ennemis tués";
    public static string NoLoss => "aucune";
    public static string ReportExperience(BattleReport report) =>
        report.Fled ? "Fuite : pas d'expérience." : report.AttackerWon ? $"Expérience : +{report.Experience}" : "";
    public static string ContinueButton => "Continuer";

    public static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    public static string LowerFirst(string s) => s.Length == 0 ? s : char.ToLower(s[0]) + s[1..];

    private static string Of(string word) =>
        "AEIOUYÉÈÊaeiouyéèê".Contains(word[0]) ? $"d'{word}" : $"de {word}";
}
