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
        ResourceType.Mercury => "mercure",
        ResourceType.Crystal => "cristal",
        ResourceType.Sulfur => "soufre",
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
        ResourceType.Mercury => "Laboratoire d'alchimiste",
        ResourceType.Crystal => "Caverne de cristal",
        ResourceType.Sulfur => "Mine de soufre",
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
        Shrine shrine => $"Autel {OfStat(shrine.Stat)}",
        SageHut hut => $"Cabane du sage ({hut.Spell.Name})",
        WeeklyProducer { IsWaterWheel: true } => "Roue à eau",
        WeeklyProducer producer => $"Moulin ({Resource(producer.Resource)})",
        ObservationTower => "Tour d'observation",
        BlessingSite { Blessing: Blessing.Morale } => "Temple",
        BlessingSite => "Cercle des fées",
        Mine mine => MineName(mine),
        MonsterGuard guard => MonsterGroup(guard.Type, guard.Count),
        Town town => town.Name,
        HeroDefender defender => HeroWithColor(defender.Hero),
        _ => obj.GetType().Name,
    };

    /// <summary>L'adversaire d'un combat, tel qu'il était au début : « une bande de Loups », « la garnison de Bellerive ».</summary>
    public static string Enemy(BattleFought battle) => battle.Defender switch
    {
        MonsterGuard guard => LowerFirst(MonsterGroup(guard.Type, battle.DefenderArmy.Sum(s => s.Count))),
        Town town => $"la garnison de {town.Name}",
        HeroDefender defender => HeroWithColor(defender.Hero),
        var other => LowerFirst(ObjectName(other)),
    };

    /// <summary>L'adversaire d'un combat qui commence : « une bande de Loups », « la garnison de Bellerive ».</summary>
    public static string Opponent(MapObject defender, Army army) => defender switch
    {
        MonsterGuard guard => LowerFirst(MonsterGroup(guard.Type, army.TotalCount)),
        Town town => $"la garnison de {town.Name}",
        HeroDefender attacked => HeroWithColor(attacked.Hero),
        var other => LowerFirst(ObjectName(other)),
    };

    // ------------------------------------------------------------- combat tactique

    public static string BattleChoiceTitle => "Combat !";
    public static string BattleChoiceText(string opponent) =>
        $"Vous affrontez {opponent}. Voulez-vous diriger le combat, ou le laisser se dérouler tout seul ?";
    public static string TacticalButton => "Combat tactique";
    public static string QuickButton => "Combat rapide";

    public static string BattleWait => "Attendre";
    public static string BattleSummon => "Invoquer des diablotins";
    public static string BattleDefend => "Se défendre";
    public static string BattleStay => "Rester";
    public static string BattleAuto => "Auto";
    public static string BattleFlee => "Fuir";

    public static string BattleTitle(string opponent) => $"Combat contre {opponent}";
    public static string BattleRound(int round) => $"Round {round}";
    public static string BattleNextRound => "Round suivant :";

    /// <summary>« 12 Archers tirent sur 30 Paysans : 18 dégâts, 18 tués. », ou null pour ce qui ne se raconte pas.</summary>
    public static string? BattleMessage(BattleAction action) => action switch
    {
        UnitStruck s => (s.Luck > 0 ? "Coup de chance ! " : s.Luck < 0 ? "Malchance… " : "")
                        + $"{Capitalize(Stack(s.Attacker.Stack))} {Verb(s)} {StackAfter(s)} : {s.Damage} dégâts"
                        + (s.Killed > 0 ? $", {s.Killed} {(s.Killed == 1 ? "tué" : "tués")}." : "."),
        UnitWaited w => $"{Capitalize(Stack(w.Unit.Stack))} {(w.Unit.Count == 1 ? "attend" : "attendent")}.",
        UnitCheered m => $"Bon moral : {Stack(m.Unit.Stack)} {(m.Unit.Count == 1 ? "rejoue" : "rejouent")} !",
        UnitFroze m => $"Mauvais moral : {Stack(m.Unit.Stack)} {(m.Unit.Count == 1 ? "hésite et passe son tour" : "hésitent et passent leur tour")}.",
        UnitRegenerated r => $"{Capitalize(Stack(r.Unit.Stack))} {(r.Unit.Count == 1 ? "se régénère" : "se régénèrent")}.",
        UnitPoisoned p => $"Le poison ronge {Stack(new CreatureStack(p.Unit.Type, p.Unit.Count + p.Killed))} : {p.Damage} dégâts"
                          + (p.Killed > 0 ? $", {p.Killed} {(p.Killed == 1 ? "tué" : "tués")}." : "."),
        UnitGrew g => $"{g.Added} {Creature(g.Unit.Type, g.Added)} de plus rejoigne{(g.Added == 1 ? "" : "nt")} la troupe !",
        UnitDrained d => d.Revived > 0
            ? $"{Capitalize(Creature(d.Unit.Type, 2))} : {d.Revived} {(d.Revived == 1 ? "revient" : "reviennent")} à la vie en buvant le sang."
            : $"{Capitalize(Creature(d.Unit.Type, 2))} : {d.HitPoints} PV repris en buvant le sang.",
        UnitDefended d => $"{Capitalize(Stack(d.Unit.Stack))} {(d.Unit.Count == 1 ? "se défend" : "se défendent")}.",
        UnitSoothed h => $"{Capitalize(Stack(h.Healer.Stack))} {(h.Healer.Count == 1 ? "soigne" : "soignent")} {Stack(h.Unit.Stack)} ({h.HitPoints} PV).",
        UnitRooted r => $"{Capitalize(Stack(r.Unit.Stack))} {(r.Unit.Count == 1 ? "est enraciné" : "sont enracinés")} : pas de déplacement au prochain tour.",
        UnitTerrified t => $"Terreur : le moral {(t.Unit.Count == 1 ? "du" : "des")} {Stack(t.Unit.Stack)} tombe à {t.Morale:+0;-0;0}.",
        UnitSwallowed w => $"{Capitalize(Stack(w.Worm.Stack))} {(w.Worm.Count == 1 ? "avale" : "avalent")} un {w.Unit.Type.Name.ToLowerInvariant()} tout entier !",
        BloodPaid b => $"Le prix du sang : {b.Points} {(b.Points == 1 ? "point" : "points")} de sort pour le héros.",
        SpellPointsStolen s => $"{Capitalize(Creature(s.Thief.Type, 2))} : {s.Points} {(s.Points == 1 ? "point" : "points")} de sort {(s.Points == 1 ? "volé" : "volés")} au héros ennemi.",
        UnitSacrificed s => $"{s.Count} {Creature(s.Unit.Type, s.Count)} {(s.Count == 1 ? "se sacrifie" : "se sacrifient")} : la troupe frappe plus fort.",
        UnitSplit s => $"{Capitalize(Creature(s.From.Type, 2))} : {s.Count} {(s.Count == 1 ? "se détache" : "se détachent")} en une nouvelle troupe.",
        UnitSeduced s => $"{Capitalize(Stack(s.Unit.Stack))} {(s.Unit.Count == 1 ? "est séduit" : "sont séduits")} : {(s.Unit.Count == 1 ? "il passera son" : "ils passeront leur")} prochain tour.",
        UnitEntranced e => $"{Capitalize(Stack(e.Unit.Stack))}, sous le charme, {(e.Unit.Count == 1 ? "passe son" : "passent leur")} tour.",
        UnitHooked h => $"{Capitalize(Stack(h.Hooker.Stack))} {(h.Hooker.Count == 1 ? "tire" : "tirent")} {Stack(h.Target.Stack)} au bout de {(h.Hooker.Count == 1 ? "son" : "leurs")} crochet{(h.Hooker.Count == 1 ? "" : "s")} !",
        UnitBurned b => $"La chaleur brûle {Stack(new CreatureStack(b.Unit.Type, b.Unit.Count + b.Killed))} : {b.Damage} dégâts"
                        + (b.Killed > 0 ? $", {b.Killed} {(b.Killed == 1 ? "tué" : "tués")}." : "."),
        UnitSummoned s => $"{Capitalize(Stack(s.Summoner.Stack))} {(s.Summoner.Count == 1 ? "ouvre" : "ouvrent")} un portail : {s.Count} {Creature(s.Summoned.Type, s.Count)} {(s.Count == 1 ? "apparaît" : "apparaissent")} !",
        UnitRevolted v => $"{Capitalize(Stack(v.Unit.Stack))} {(v.Unit.Count == 1 ? "se révolte et change" : "se révoltent et changent")} de camp !",
        UnitMayRetreat r => $"{Capitalize(Stack(r.Unit.Stack))} {(r.Unit.Count == 1 ? "peut" : "peuvent")} repartir ({r.Steps} {(r.Steps == 1 ? "case" : "cases")}).",
        UnitEnraged e => $"{Capitalize(Stack(e.Unit.Stack))} {(e.Unit.Count == 1 ? "enrage" : "enragent")} : +{e.Rage} en attaque.",
        LightningStruck l => $"La foudre frappe {Stack(new CreatureStack(l.Target.Type, l.Target.Count + l.Killed))} : {l.Damage} dégâts"
                             + (l.Killed > 0 ? $", {l.Killed} {(l.Killed == 1 ? "tué" : "tués")}." : "."),
        SpellCast c => $"{c.Caster.Name} lance {c.Spell.Name}.",
        SpellDamaged d => $"{d.Spell.Name} : {d.Damage} dégâts à {Stack(new CreatureStack(d.Target.Type, d.Target.Count + d.Killed))}"
                          + (d.Killed > 0 ? $", {d.Killed} {(d.Killed == 1 ? "tué" : "tués")}." : "."),
        UnitHealed { Revived: > 0 } h => $"{h.Spell.Name} : {h.Revived} {Creature(h.Unit.Type, h.Revived)} {(h.Revived == 1 ? "revient" : "reviennent")} au combat.",
        UnitHealed h => $"{h.Spell.Name} : {Stack(h.Unit.Stack)} {(h.Unit.Count == 1 ? "est soigné" : "sont soignés")} ({h.HitPoints} PV).",
        SpellApplied a => $"{a.Spell.Name} sur {Stack(a.Unit.Stack)}.",
        SpellsDispelled d => $"{d.Spell.Name} : {Stack(d.Unit.Stack)} {(d.Unit.Count == 1 ? "n'a" : "n'ont")} plus aucun sort.",
        UnitSkipped s => $"{Capitalize(Stack(s.Unit.Stack))} {(s.Unit.Count == 1 ? "est aveuglé et ne joue pas" : "sont aveuglés et ne jouent pas")}.",
        UnitTeleported t => $"{Capitalize(Stack(t.Unit.Stack))} {(t.Unit.Count == 1 ? "est téléporté" : "sont téléportés")}.",
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

    public static string DefenseChoiceTitle => "Vous êtes attaqué !";

    public static string DefenseChoiceText(string attacker, string? town) => town == null
        ? $"{attacker} attaque votre héros. Dirigez-vous la défense, ou laissez-vous le combat se jouer seul ?"
        : $"{attacker} attaque {town}. Dirigez-vous la défense de la garnison, ou laissez-vous le combat se jouer seul ?";

    /// <summary>« Sire Yvain (Bleu) » : un héros et la couleur de son joueur.</summary>
    public static string HeroWithColor(Hero hero) => $"{hero.Name} ({hero.Owner.Name})";

    /// <summary>Le bilan vu par celui qui se défendait : « Sire Yvain (Bleu) attaque Sire Roland · 3 rounds ».</summary>
    public static string DefenseSubtitle(BattleFought battle) =>
        $"{HeroWithColor(battle.Hero)} attaque {(battle.Defender is Town town ? town.Name : battle.Defender is HeroDefender d ? d.Hero.Name : ObjectName(battle.Defender))}"
        + $" · {battle.Report.Rounds} round{(battle.Report.Rounds > 1 ? "s" : "")}";

    /// <summary>Le titre du bilan pour celui qui se défendait.</summary>
    public static string DefenseTitle(BattleReport report) => report.AttackerWon ? "Défaite…" : "Victoire !";

    /// <summary>L'expérience du héros qui s'est défendu et a gagné (la valeur des troupes tuées).</summary>
    public static string DefenseExperience(BattleFought battle) =>
        battle.Defender is HeroDefender && !battle.Report.AttackerWon && !battle.Report.Fled
            ? $"Expérience : +{Battle.ExperienceFor(battle.Report.AttackerLosses)}" : "";

    /// <summary>« Révolte : 40 Paysans refusent d'obéir… », ou null s'il n'y a pas eu de révolte.</summary>
    public static string? Revolt(BattleReport report)
    {
        var lines = new List<string>();
        if (report.Rebels.Count > 0)
            lines.Add($"Révolte : {string.Join(", ", report.Rebels.Select(Stack))} refusent d'obéir et combattent l'ennemi"
                      + (report.RebelSurvivors.Count > 0 ? ", les survivants quittent l'armée." : "."));
        if (report.DefenderRebels.Count > 0)
            lines.Add($"Révolte chez le défenseur : {string.Join(", ", report.DefenderRebels.Select(Stack))} refusent d'obéir et combattent avec l'attaquant"
                      + (report.DefenderRebelSurvivors.Count > 0 ? ", les survivants quittent son armée." : "."));
        return lines.Count == 0 ? null : string.Join(" ", lines);
    }

    public static string BattleTitle(BattleFought battle) => battle.Defender is Town town
        ? $"Assaut de {town.Name}"
        : $"Combat contre {Enemy(battle)}";

    // ------------------------------------------------------------ héros

    /// <summary>« de l'attaque », « de la défense », « de la puissance », « du savoir ».</summary>
    public static string OfStat(HeroStat stat) => stat switch
    {
        HeroStat.Attack => "de l'attaque",
        HeroStat.Defense => "de la défense",
        HeroStat.Power => "de la puissance",
        _ => "du savoir",
    };

    /// <summary>Ce que le lieu survolé donne encore : « déjà visité », « vide cette semaine »… (vide s'il donne).</summary>
    public static string SiteState(MapObject obj, Hero? hero, Player viewer) => obj switch
    {
        Shrine shrine when hero != null && shrine.Visitors.Contains(hero.Id) => $" (déjà visité par {hero.Name})",
        SageHut hut when hero != null && hero.Knows(hut.Spell) => $" ({hero.Name} connaît ce sort)",
        WeeklyProducer { Stocked: false } => " (vide cette semaine)",
        ObservationTower tower when tower.Visitors.Contains(viewer.Id) => " (déjà visitée)",
        BlessingSite site when hero != null && site.AlreadyGiven(hero) => $" ({hero.Name} en a déjà la bénédiction)",
        _ => "",
    };

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

    public const string ChestTitle = "Un coffre au trésor !";

    // ------------------------------------------------------------- Diplomatie et fuite des gardiens

    public static string EncounterTitle(GuardEncounter e) => e.Offer != null ? "Diplomatie" : "Ils s'enfuient !";

    /// <summary>Ce que propose le gardien (ou qu'il s'enfuit), et ce qui empêche d'accepter.</summary>
    public static string EncounterText(GuardEncounter e)
    {
        var group = Stack(new CreatureStack(e.Guard.Type, e.Guard.Count)); // le nombre exact : on les paie, ils rejoignent l'armée
        var lines = new List<string>();
        if (e.Offer is { } offer)
        {
            lines.Add($"{Capitalize(group)} {(e.Guard.Count == 1 ? "propose" : "proposent")} de rejoindre {e.Hero.Name}"
                      + (offer.IsFree ? ", sans rien demander." : $" pour {Cost(offer.Price)}."));
            lines.Add(offer.Refusal switch
            {
                JoinRefusal.Command => $"Mais {e.Hero.Name} n'a pas assez de charisme pour les commander : ils ne lui obéiraient pas.",
                JoinRefusal.Price => "Mais vous n'avez pas de quoi payer.",
                JoinRefusal.Slot => "Mais il n'y a plus de place dans l'armée.",
                _ => "",
            });
        }
        if (e.Flees)
            lines.Add(e.Offer == null
                ? $"{Capitalize(group)} {(e.Guard.Count == 1 ? "a peur" : "ont peur")} de votre armée et {(e.Guard.Count == 1 ? "s'enfuit" : "s'enfuient")}. Les laisser partir (sans combat ni expérience), ou les poursuivre ?"
                : "Sinon, ils s'enfuiront : vous pouvez les laisser partir.");
        return string.Join("\n", lines.Where(l => l.Length > 0));
    }

    public static string EncounterJoin(GuardEncounter e) => e.Offer is { IsFree: false } offer ? $"Accepter ({Cost(offer.Price)})" : "Accepter";
    public const string EncounterLetFlee = "Les laisser partir";
    public static string EncounterFight(GuardEncounter e) => e.Flees ? "Les poursuivre" : "Combattre";

    public static string ChestText(ChestOpened chest) =>
        $"{chest.Hero.Name} ouvre un coffre plein d'or. Garder l'or pour le royaume, ou le distribuer aux paysans "
        + "et gagner de l'expérience ?";

    public static string ChestGold(TreasureChest chest) => $"{chest.Gold} or";

    public static string ChestExperience(TreasureChest chest) => $"{chest.Experience} points d'expérience";

    /// <summary>Le titre de la fenêtre de choix : « Niveau 2 : choisissez une caractéristique ».</summary>
    public static string LevelChoiceTitle(HeroLevelChoice choice) =>
        $"Niveau {choice.Level} : choisissez une caractéristique";

    public static string LevelChoiceText(HeroLevelChoice choice) =>
        $"{choice.Hero.Name} progresse. Le charisme augmente de 1 dans tous les cas, et le héros gagne un point de compétence "
        + "(à dépenser dans l'écran du héros).";

    /// <summary>« 2 → 3 » : la caractéristique avant et après ce choix de niveau.</summary>
    public static string StatChange(Hero hero, HeroStat stat) => $"{StatValue(hero, stat)} → {StatValue(hero, stat) + 1}";

    /// <summary>Ce que le niveau donne dans tous les cas : un point de charisme, et le commandement qui en découle.</summary>
    public static string CharismaGain(Hero hero) =>
        $"Charisme : {hero.Charisma} → {hero.Charisma + 1} (commande "
        + $"{Command.Limit(hero.Charisma + 1) * (100 + hero.SkillValue(Skills.Leadership)) / 100} points par troupe).";

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
    public static string Cost(IReadOnlyDictionary<ResourceType, int> cost)
    {
        if (cost.Count == 0)
            return "Gratuit";
        // la même quantité des quatre ressources rares (la guilde des mages) : « 3 de chaque rare »
        var rare = ResourceInfo.Rare.Select(r => cost.GetValueOrDefault(r)).Distinct().ToList();
        if (rare.Count == 1 && rare[0] > 0)
            return string.Join(", ", cost.Where(c => !ResourceInfo.IsRare(c.Key)).Select(c => $"{c.Value} {Resource(c.Key)}")
                .Append($"{rare[0]} de chaque rare"));
        return string.Join(", ", cost.Select(c => $"{c.Value} {Resource(c.Key)}"));
    }

    /// <summary>Le texte du bouton de construction, selon ce que disent les règles.</summary>
    public static string BuildStatus(BuildResult result, Town town, BuildingType building) => result switch
    {
        BuildResult.Ok => "Construire",
        BuildResult.AlreadyBuilt => "Construit",
        BuildResult.AlreadyBuiltToday => "Un bâtiment par jour",
        BuildResult.MissingRequirement => "Requiert : " + string.Join(", ",
            building.RequirementChoices.Where(choices => !choices.Any(town.Has))
                .SelectMany(choices => town.Buildings.Where(b => choices.Contains(b.Id)).Take(1)).Select(b => b.Name)),
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
        { MageGuild: > 0 } => $"Enseigne {town.GuildSpellsAt(building.MageGuild)} sorts de niveau {building.MageGuild} aux héros"
                              + " et recharge leurs points de sort",
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
        GameEndReason.OpponentsDefeated => "Vos adversaires sont vaincus. La région est à vous !",
        GameEndReason.TownsCaptured => "Toutes les villes sont à vous. La région est à vous !",
        GameEndReason.HeroesLost => "Votre dernier héros est tombé. La partie est perdue.",
        GameEndReason.Eliminated => "Votre dernière ville est tombée, et vous n'aviez plus de héros pour la défendre. La partie est perdue.",
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
        VictoryCondition.DefeatOpponents => "Vaincre tous les adversaires",
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
    public static string OptionsText => "F5 fait une sauvegarde rapide, F9 la reprend. La partie est aussi sauvegardée au début de chaque tour.";
    public static string SaveButton => "Sauvegarder";
    public static string LoadButton => "Charger";
    public static string FullscreenButton => "Plein écran";
    public static string QuitGameButton => "Quitter le jeu";
    public static string SaveTooltip => "Sauvegarder la partie sous un nom";
    public static string LoadTooltip => "Choisir une partie sauvegardée";
    public static string FullscreenTooltip => "Passer du plein écran à une fenêtre, ou l'inverse";
    public static string CloseOptionsTooltip => "Revenir à la partie";
    public static string QuitGameTitle => "Quitter le jeu";
    public static string QuitGameText => "Quitter le jeu ? Ce qui n'a pas été sauvegardé sera perdu.";
    public static string QuitGameConfirm => "Quitter";
    public static string CloseButton => "Fermer";
    public static string NoSave => "Aucune partie sauvegardée.";
    public static string NoQuickSave => "Aucune sauvegarde rapide (F5 pour en faire une).";
    public static string QuickSaved => "Sauvegarde rapide faite (F9 pour la reprendre).";

    // ------------------------------------------------------------- menu principal et sauvegardes (étape 7)

    public static string GameTitle => "HoMM";
    public static string NewGameTooltip => "Choisir la taille de la carte, la graine et les façons de gagner";
    public static string LoadGameButton => "Charger";
    public static string SettingsButton => "Réglages";
    public static string SettingsTooltip => "Plein écran";
    public static string SettingsText => "D'autres réglages viendront plus tard.";
    public static string MainMenuButton => "Menu principal";
    public static string MainMenuTooltip => "Revenir au menu principal";
    public static string MainMenuTitle => "Menu principal";
    public static string MainMenuText => "Revenir au menu principal ? Ce qui n'a pas été sauvegardé sera perdu.";

    public static string NewGameTitle => "Nouvelle partie";
    public static string MapSizeLabel => "Taille de la carte";
    public static string MapSizeName(MapSize size) => size switch
    {
        MapSize.Tiny => "Très petite",
        MapSize.Small => "Petite",
        MapSize.Normal => "Normale",
        MapSize.Large => "Grande",
        _ => "Très grande",
    };
    public static string MapSizeDetails(MapSize size, int width, int height) =>
        $"{width} × {height} cases, {width / MapGenerator.ZoneWidth * (height / MapGenerator.ZoneHeight)} régions"
        + (size >= MapSize.Large ? " : une longue partie" : "");
    public static string SeedLabel => "Graine";
    public static string SeedPlaceholder => "au hasard";
    public static string RandomSeed => "Au hasard";
    public static string SeedHelp => "La même graine donne la même carte.";
    public static string GoalsLabel => "Comment gagner";
    public static string OpponentsLabel => "Adversaires";
    public static string DifficultyLabel => "Difficulté";

    public static string DifficultyName(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "Facile",
        Difficulty.Hard => "Difficile",
        _ => "Normale",
    };

    public static string DifficultyTooltip(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "Des adversaires prudents, moins riches, qui vous laissent tranquille les trois premières semaines",
        Difficulty.Hard => "Des adversaires audacieux, plus riches, avec plus de héros",
        _ => "Des adversaires ni trop prudents ni trop audacieux",
    };

    /// <summary>Les déplacements d'un adversaire que l'on voit sont rejoués : « Bleu se déplace… ».</summary>
    public static string RivalMoving(Player player) => $"{player.Name} se déplace…";
    public static string OpponentsTooltip(int count) => count switch
    {
        0 => "Seul contre les monstres",
        1 => "Un adversaire joué par l'ordinateur (dans le coin opposé)",
        _ => $"{count} adversaires joués par l'ordinateur, chacun dans un coin (sur les plus grandes cartes)",
    };
    public static string GoalsNote => "La première façon accomplie l'emporte. Perdre tous ses héros reste une défaite.";

    public static string SaveTitle => "Sauvegarder la partie";
    public static string LoadTitle => "Charger une partie";
    public static string SaveNameLabel => "Nom :";
    public static string SaveAction => "Sauvegarder";
    public static string LoadAction => "Charger";
    public static string DeleteSave => "Supprimer";
    public static string OverwriteTitle => "Remplacer la sauvegarde";
    public static string OverwriteText(string name) => $"Une sauvegarde « {name} » existe déjà. La remplacer ?";
    public static string DeleteTitle => "Supprimer la sauvegarde";
    public static string DeleteText(string name) => $"Supprimer « {name} » ? Elle sera perdue.";

    /// <summary>Le nom proposé pour une nouvelle sauvegarde : « Sire Roland, jour 12 ».</summary>
    public static string SuggestedSaveName(string? hero, int day) => $"{hero ?? "Partie"}, jour {day + 1}";

    public static string SaveDisplayName(SaveEntry entry) =>
        entry.IsQuick ? "Sauvegarde rapide" : entry.IsAuto ? "Sauvegarde automatique" : entry.Name;

    /// <summary>« Jour 12 (Mois 1, semaine 2, Vendredi) · Sire Roland, niveau 3 · carte normale · le 04/11 à 18:32 ».</summary>
    public static string SaveDetails(SaveEntry entry)
    {
        if (entry.Summary is not { } s)
            return "Illisible (fichier abîmé ou d'une autre version du jeu)";
        string hero = s.HeroName == null ? "aucun héros" : $"{s.HeroName}, niveau {s.HeroLevel}";
        string ended = s.Outcome == GameOutcome.Playing ? "" : " · partie finie";
        return $"Jour {s.Day + 1} ({Date(s.Day)}) · {hero} · carte {MapSizeName(s.Size).ToLowerInvariant()}{ended}"
            + $" · le {entry.Written:dd'/'MM} à {entry.Written:HH:mm}";
    }
    public static string LoadFailed => "Cette sauvegarde ne peut pas être chargée (fichier abîmé ou d'une autre version du jeu).";

    public static string RulesText => "Comment gagner cette partie ? Cochez une ou plusieurs façons : la première accomplie l'emporte. Perdre tous ses héros reste une défaite.";

    // ------------------------------------------------------------- journal

    /// <summary>
    /// La ligne du journal pour un événement, vu par le joueur <paramref name="viewer"/>,
    /// ou null si l'événement ne s'affiche pas dans le journal.
    /// </summary>
    public static string? Message(GameEvent e, Player viewer) => e switch
    {
        TownCaptured { PreviousOwner: { } loser } lost when loser == viewer && lost.Hero.Owner != viewer =>
            $"{HeroWithColor(lost.Hero)} vous prend {lost.Town.Name} !",
        ObjectVisited visit when visit.Hero.Owner != viewer => null,
        ChestOpened => null, // la fenêtre du choix
        GuardEncounter => null, // la fenêtre du choix
        GuardJoined joined when joined.Hero.Owner != viewer => null,
        GuardJoined joined => $"{Capitalize(Stack(new CreatureStack(joined.Type, joined.Count)))} {(joined.Count == 1 ? "rejoint" : "rejoignent")} {joined.Hero.Name}"
                              + (joined.Price.Count > 0 ? $" pour {Cost(joined.Price)}." : " sans rien demander."),
        GuardFled fled when fled.Hero.Owner != viewer => null,
        GuardFled fled => $"{Capitalize(Stack(new CreatureStack(fled.Type, fled.Count)))} {(fled.Count == 1 ? "s'enfuit" : "s'enfuient")} devant {fled.Hero.Name}.",
        ChestChosen chosen when chosen.Hero.Owner != viewer => null,
        ChestChosen { Gold: true } chosen => $"Un coffre au trésor ! +{chosen.Amount} or.",
        ChestChosen chosen => $"Un coffre au trésor ! {chosen.Hero.Name} gagne {chosen.Amount} points d'expérience.",
        ResourcesFound found => $"Vous trouvez {found.Amount} {Resource(found.Resource)}.",
        MineCaptured captured =>
            $"{MineName(captured.Mine)} capturée ! +{captured.Mine.DailyProduction} {Resource(captured.Mine.Resource)} par jour.",
        MineAlreadyOwned owned => $"{MineName(owned.Mine)} vous appartient déjà.",

        TownCaptured captured => $"Vous prenez {captured.Town.Name} !",
        TownEntered entered => $"{entered.Hero.Name} entre dans {entered.Town.Name}.",
        BuildingBuilt built when built.Town.Owner != viewer => null,
        BuildingBuilt built => $"{built.Building.Name} : construit à {built.Town.Name}.",
        DeadRaised raised when raised.Hero.Owner != viewer => null,
        DeadRaised raised => $"Nécromancie : {raised.Count} {Creature(raised.Type, raised.Count)} se relève{(raised.Count == 1 ? "" : "nt")} dans l'armée de {raised.Hero.Name}.",
        SpellsLearned learned when learned.Hero.Owner != viewer => null,
        SpellsLearned learned => $"{learned.Hero.Name} apprend {List(learned.Spells.Select(s => s.Name).ToList())} à la guilde des mages de {learned.Town.Name}.",
        ResourcesTraded traded when traded.Player != viewer => null,
        ResourcesTraded traded => $"Marché : {traded.Given} {Resource(traded.Give)} contre {traded.Received} {Resource(traded.Receive)}.",
        HeroHired hired when hired.Hero.Owner != viewer => null,
        HeroHired hired => $"{hired.Hero.Name} rejoint vos rangs à {hired.Town.Name} !",
        CreaturesRecruited recruited when recruited.Town.Owner != viewer => null,
        CreaturesRecruited recruited =>
            $"{recruited.Count} {Creature(recruited.Type, recruited.Count)} recruté{(recruited.Count > 1 ? "s" : "")} à {recruited.Town.Name}"
            + (recruited.ToHero ? " (garnison pleine : ils rejoignent le héros)." : "."),

        BattleFought { DefenderOwner: { } defender } battle when defender == viewer && battle.Hero.Owner != viewer =>
            battle.Report.AttackerWon
                ? $"{HeroWithColor(battle.Hero)} vous attaque et l'emporte."
                : $"{HeroWithColor(battle.Hero)} vous attaque et est repoussé !",
        BattleFought battle when battle.Hero.Owner != viewer => null,
        PlayerDefeated defeated when defeated.Player != viewer => $"Le joueur {defeated.Player.Name} est éliminé !",
        BattleFought { Report.AttackerWon: true } battle => Join(
            Revolt(battle.Report),
            $"Victoire contre {Enemy(battle)} ! Pertes : {Losses(battle.Report.AttackerLosses)}. +{battle.Report.Experience} expérience."),
        BattleFought battle => Join(Revolt(battle.Report), $"{battle.Hero.Name} a été vaincu par {Enemy(battle)}."),
        HeroLeveledUp levelUp when levelUp.Hero.Owner != viewer => null,
        HeroLeveledUp levelUp => LevelUp(levelUp),

        DayStarted { NewWeek: true } day => $"Une nouvelle semaine commence, les monstres sont plus nombreux ! {Date(day.Day)}.",
        DayStarted day => $"Nouveau jour : {Date(day.Day)}.",
        IncomeReceived income when income.Player == viewer => $"Revenus : {Income(income.Income)}.",
        TurnStarted { Player.IsComputer: true } => null, // les adversaires jouent aussitôt : pas de message
        TurnStarted turn => $"Au tour du joueur {turn.Player.Name}.",
        RulesChosen { Treasure: { } where } chosen =>
            $"Objectif : {Objective(chosen.Rules)}. Le trésor est enfoui {Where(where)} de la carte : les obélisques en dévoilent la carte (touche M).",
        RulesChosen chosen => $"Objectif : {Objective(chosen.Rules)}.",
        GameEnded { Reason: GameEndReason.TreasureFound } => "Le trésor est à vous. Victoire !",
        ShrineVisited { Gained: true } shrine => $"{shrine.Hero.Name} prie à l'autel {OfStat(shrine.Shrine.Stat)} : +1 {StatName(shrine.Shrine.Stat)}.",
        ShrineVisited shrine => $"{shrine.Hero.Name} a déjà prié à cet autel.",
        SageVisited { Learned: true } sage => $"Le sage enseigne {sage.Hut.Spell.Name} à {sage.Hero.Name}.",
        SageVisited sage => $"{sage.Hero.Name} connaît déjà le sort du sage ({sage.Hut.Spell.Name}).",
        BlessingVisited { Gained: true, Site.Blessing: Blessing.Morale } b => $"{b.Hero.Name} se recueille au temple : +1 moral jusqu'au prochain combat.",
        BlessingVisited { Gained: true } b => $"{b.Hero.Name} danse avec les fées : +1 chance jusqu'au prochain combat.",
        BlessingVisited b => $"{b.Hero.Name} a déjà cette bénédiction (elle dure jusqu'au prochain combat).",
        ProducerVisited { Amount: > 0 } producer => $"{ObjectName(producer.Producer)} : +{producer.Amount} {Resource(producer.Producer.Resource)}.",
        ProducerVisited producer => $"{ObjectName(producer.Producer)} : rien à prendre avant la semaine prochaine.",
        TowerVisited tower => $"Du haut de la tour, {tower.Hero.Name} découvre les environs.",
        ObeliskVisited { FirstVisit: true } => "Un obélisque ! Un morceau de la carte au trésor apparaît.",
        ObeliskVisited => "Vous avez déjà lu cet obélisque.",
        NothingDug dug => $"{dug.Hero.Name} creuse… il n'y a rien ici.",
        TreasureDug dug => $"{dug.Hero.Name} déterre le {dug.Artifact.Name} ! {Bonus(dug.Artifact)}.",
        GameEnded { Reason: GameEndReason.GuardsCleared } => "La région est libérée de ses monstres. Victoire !",
        GameEnded { Reason: GameEndReason.OpponentsDefeated } => "Tous vos adversaires sont vaincus. Victoire !",
        GameEnded { Reason: GameEndReason.TownsCaptured } => "Toutes les villes sont à vous. Victoire !",
        GameEnded { Reason: GameEndReason.HeroesLost } => "Vous n'avez plus de héros. Défaite...",
        GameEnded { Reason: GameEndReason.Eliminated } => "Vous n'avez plus ni héros ni ville. Défaite...",
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
        HeroStat.Power => $"Puissance {hero.Power} : la force des sorts (dégâts, soins) et leur durée ({hero.Power + 1} round{(hero.Power + 1 > 1 ? "s" : "")}).",
        HeroStat.Knowledge => $"Savoir {hero.Knowledge} : {Hero.SpellPointsPerKnowledge} points de sort par point (points de sort : {hero.SpellPoints} / {hero.MaxSpellPoints}, +1 par jour).",
        _ => $"Charisme {hero.Charisma} : le héros commande au plus {hero.CommandLimit} points par troupe "
             + "(nombre × coût de la créature). Au-delà, la troupe risque de se révolter au combat.",
    };

    /// <summary>« +2 », « 0 », « −1 ».</summary>
    public static string Signed(int value) => value > 0 ? $"+{value}" : value < 0 ? $"−{-value}" : "0";

    public static string MoraleWord => "Moral";
    public static string LuckWord => "Chance";

    /// <summary>« Moral +1 · chance 0 ».</summary>
    public static string MoraleAndLuck(int morale, int luck) => $"Moral {Signed(morale)} · chance {Signed(luck)}";

    /// <summary>Ce que fait le moral, et d'où vient celui-ci (les parts qui comptent).</summary>
    public static string MoraleTooltip(MoraleParts morale)
    {
        int total = morale.Total;
        var lines = new List<string>
        {
            $"Moral {Signed(total)}"
            + (total > 0 ? $" : après avoir agi, la troupe rejoue aussitôt une fois sur {Percent(total * Morale.GoodChancePerPoint)}."
                : total < 0 ? $" : au début de son tour, la troupe se fige une fois sur {Percent(-total * Morale.BadChancePerPoint)}."
                : " : ni bon ni mauvais."),
        };
        void Part(int value, string what)
        {
            if (value != 0)
                lines.Add($"{what} : {Signed(value)}");
        }
        Part(morale.Aura, "Charisme du héros (20, 30, 40)");
        Part(morale.Factions, morale.Factions > 0 ? "Une seule faction dans l'armée" : morale.Factions == -1 ? "Trois factions dans l'armée" : "Quatre factions ou plus");
        Part(morale.Overload, "Troupe trop nombreuse pour le charisme");
        Part(morale.Skill, "Compétence Moral");
        Part(morale.Site, "Temple (jusqu'au prochain combat)");
        lines.Add("Une seule faction : +1 ; trois : −1 ; quatre ou plus : −2.");
        return string.Join("\n", lines);
    }

    /// <summary>Ce que fait la chance, et d'où vient celle-ci.</summary>
    public static string LuckTooltip(LuckParts luck)
    {
        int total = luck.Total;
        var lines = new List<string>
        {
            $"Chance {Signed(total)}"
            + (total > 0 ? $" : un coup sur {Percent(total * Morale.LuckChancePerPoint)} fait deux fois plus de dégâts."
                : total < 0 ? $" : un coup sur {Percent(-total * Morale.LuckChancePerPoint)} fait moitié moins de dégâts."
                : " : ni bonne ni mauvaise."),
        };
        if (luck.Skill != 0)
            lines.Add($"Compétence Chance : {Signed(luck.Skill)}");
        if (luck.Site != 0)
            lines.Add($"Cercle des fées (jusqu'au prochain combat) : {Signed(luck.Site)}");
        return string.Join("\n", lines);
    }

    /// <summary>« 5 % » d'une part entre 0 et 1.</summary>
    private static string Percent(double share) => $"{Math.Round(share * 100)} %";

    /// <summary>L'info-bulle d'une troupe : ses caractéristiques, ses capacités et son commandement.</summary>
    public static string StackTooltip(CreatureStack stack, int? commandLimit, MoraleParts? morale = null, int? luck = null)
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
        if (commandLimit is { } c)
        {
            lines.Add($"Commandement : {Command.Cost(t, stack.Count)} / {c} points");
            if (Command.Cost(t, stack.Count) > c)
                lines.Add("Trop nombreuse pour le charisme du héros : elle risque de se révolter au combat.");
        }
        if (morale is { } m)
            lines.Add(MoraleAndLuck(m.Total, luck ?? 0) + (m.Overload < 0 ? $" (trop nombreuse : {Signed(m.Overload)} moral)" : ""));
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
        Ability.Undead => "mort-vivant (insensible aux sorts de l'esprit et au poison)",
        Ability.GhostGrowth => "grossit en tuant",
        Ability.Poison => "morsure empoisonnée",
        Ability.LifeDrain => "draine la vie",
        Ability.DeathCloud => "nuage de mort",
        Ability.Sweep => "balayage",
        Ability.Rage => "rage (chaque coup reçu donne de l'attaque)",
        Ability.HitAndRun => "frappe et repart",
        Ability.Lightning => "appelle la foudre",
        Ability.Rending => "griffes (ignorent 60 % de la défense)",
        Ability.NatureRegeneration => "régénération de la nature (un quart de ses PV à chaque tour)",
        Ability.DoubleShot => "tire deux fois",
        Ability.Leaps => "bondit par-dessus troupes et obstacles",
        Ability.HealingAura => "aura de soin (soigne les troupes amies voisines)",
        Ability.Roots => "enracine (la cible ne peut plus bouger à son tour suivant)",
        Ability.Made => "façonnée : résiste à la magie (moitié des dégâts des sorts, insensible aux sorts néfastes)",
        Ability.Repairs => "répare les créatures façonnées voisines",
        Ability.Blessing => "bénit une troupe amie au début de son tour",
        Ability.NoMeleePenalty => "sans pénalité au corps à corps",
        Ability.Terror => "terreur (chaque coup fait perdre 1 de moral à la cible)",
        Ability.Horror => "horreur (sa terreur fait perdre 2 de moral)",
        Ability.Brave => "brave (moral jamais sous +1, insensible à la terreur)",
        Ability.EndlessRetaliation => "riposte à chaque attaque",
        Ability.Burrows => "se déplace sous terre",
        Ability.Swallow => "peut avaler une créature entière",
        Ability.Rays => "rayons (chaque tir lance un sort néfaste au hasard)",
        Ability.Infernal => "infernal (insensible au feu ; sa mort rend des points de sort à son héros)",
        Ability.ManaThief => "vole des points de sort au héros ennemi",
        Ability.Sacrifice => "sacrifice (un sur dix meurt avant d'attaquer, +50 % de dégâts)",
        Ability.Splits => "se divise quand il est frappé",
        Ability.Seduce => "séduit (un tir sur quatre fait passer son tour à la cible)",
        Ability.Hook => "crochet (tire un ennemi à 3 cases contre lui)",
        Ability.HeatAura => "aura de chaleur (brûle les ennemis voisins)",
        Ability.Feeds => "se nourrit des morts de l'Enfer (+1 attaque)",
        Ability.Summons => "invoque des diablotins (une fois par combat)",
        _ => ability,
    };

    public static string NextHeroButton => "Héros suivant";
    public static string NextHeroShort => "Suivant";
    public static string TreasureShort => "Trésor";
    public static string WalkTooltip => "Avancer sur le chemin tracé";
    public static string CenterTooltip => "Centrer la carte sur le héros";
    public static string DigTooltip => "Creuser ici (il faut une journée entière de mouvement)";
    public static string OptionsTooltip => "Options : sauvegarder, charger, plein écran, quitter le jeu";
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
        HeroStat.Power => $"force et durée des sorts ({hero.Power + 1} round{(hero.Power + 1 > 1 ? "s" : "")})",
        HeroStat.Knowledge => $"{hero.MaxSpellPoints} points de sort au plus",
        _ => $"commande {hero.CommandLimit} points par troupe",
    };

    // ------------------------------------------------------------------ l'écran du héros : onglets et équipement

    public static string EquipmentTab => "Équipement";
    public static string SkillsTab => "Compétences";
    public static string MagicTab => "Magie";
    public static string ChestHeader => "Coffre";
    public static string ChestTooltip => "Le coffre : les artefacts que le héros porte sans les mettre.";

    /// <summary>Le nom d'un emplacement sur le héros (court : il s'écrit dans l'emplacement vide).</summary>
    public static string SlotName(ArtifactSlot slot) => slot switch
    {
        ArtifactSlot.Head => "Tête",
        ArtifactSlot.Neck => "Cou",
        ArtifactSlot.Shoulders => "Cape",
        ArtifactSlot.Torso => "Torse",
        ArtifactSlot.Weapon => "Arme",
        ArtifactSlot.Shield => "Bouclier",
        ArtifactSlot.Ring => "Anneau",
        ArtifactSlot.Belt => "Ceinture",
        ArtifactSlot.Feet => "Pieds",
        _ => "Divers",
    };

    /// <summary>L'info-bulle d'un emplacement : ce qu'il porte, ou qu'il est vide.</summary>
    public static string SlotTooltip(ArtifactSlot slot, ArtifactType? artifact) =>
        artifact != null ? $"{SlotName(slot)} : {ArtifactTooltip(artifact)}" : $"{SlotName(slot)} : rien pour l'instant.";

    /// <summary>Les deux premières lettres du nom d'une compétence (son icône, en attendant les images).</summary>
    public static string SkillInitials(SkillType skill) => skill.Name.Length <= 2 ? skill.Name : skill.Name[..2];

    /// <summary>L'info-bulle d'une compétence dans son arbre : son rang, ce que fait le suivant, et comment l'apprendre.</summary>
    public static string SkillIconTooltip(SkillType skill, int rank, LearnSkillResult result) =>
        $"{SkillButton(skill, rank)}\n{SkillNextLine(skill, rank)}\n"
        + (result == LearnSkillResult.Ok ? "Clic : dépenser un point de compétence." : LearnSkillReason(result));

    // ------------------------------------------------------------------ compétences (étape 9)


    /// <summary>« Points de compétence : 2 · compétences 3 / 6 ».</summary>
    public static string SkillPointsLine(int points, int skills) =>
        $"Points de compétence : {points} · compétences {skills} / {HoMM.Core.Skills.MaxSkills}";

    public static string SkillTreeName(SkillTree tree) => tree switch
    {
        SkillTree.Aventure => "Aventure",
        SkillTree.Commandement => "Commandement",
        _ => "Magie",
    };


    public static string SkillRowLine(int row, int needed, bool open) =>
        open ? $"Rangée {row}" : $"Rangée {row} : ouverte après {needed} points dans cet arbre";

    public static string RankName(int rank) => rank switch { 1 => "base", 2 => "avancé", _ => "expert" };

    /// <summary>« Logistique (avancé) », ou le nom seul si le héros ne l'a pas.</summary>
    public static string SkillButton(SkillType skill, int rank) => rank == 0 ? skill.Name : $"{skill.Name} ({RankName(rank)})";

    /// <summary>Ce que fait la compétence à ce rang.</summary>
    public static string SkillEffect(SkillType skill, int rank)
    {
        int v = skill.ValueAt(rank), v2 = skill.Value2At(rank);
        if (skill.School is { } school)
            return $"sorts {SchoolName(school)} : {v} % moins chers, +{v2} % de force"
                   + (rank >= HoMM.Core.Skills.MaxRank ? " ; plus tard, un sort spécial" : "");
        return skill.Id switch
        {
            HoMM.Core.Skills.Logistics => $"+{v} % de mouvement sur la carte",
            HoMM.Core.Skills.Scouting => $"voit {v} cases plus loin",
            HoMM.Core.Skills.Pathfinding => $"le surcoût des terrains difficiles baisse de {v} %",
            HoMM.Core.Skills.Estates => $"+{v} or par jour",
            HoMM.Core.Skills.Leadership => $"+{v} % de points de commandement par troupe",
            HoMM.Core.Skills.Discipline => $"révoltes {v} % moins probables",
            HoMM.Core.Skills.Recruiter => $"+{v} % de créatures chaque semaine dans la ville où le héros commence la semaine",
            HoMM.Core.Skills.Inspiration => $"+{v} de vitesse au premier round" + (v2 > 0 ? $", puis +{v2} pour tout le combat" : ""),
            HoMM.Core.Skills.Mysticism => $"+{v} point{(v > 1 ? "s" : "")} de sort par jour",
            HoMM.Core.Skills.Morale => $"+{v} de moral aux troupes",
            HoMM.Core.Skills.Luck => $"+{v} de chance aux troupes",
            HoMM.Core.Skills.Tactics => $"placer ses troupes avant le combat, dans les {v} premières colonnes (moins si l'ennemi a aussi la Tactique)",
            HoMM.Core.Skills.Diplomacy => rank switch
            {
                1 => "un gardien que votre armée vaut 3 fois propose de vous rejoindre, à son prix",
                2 => "un gardien que votre armée vaut 2 fois propose de vous rejoindre, à moitié prix",
                _ => "un gardien que votre armée vaut 1,5 fois propose de vous rejoindre, gratuitement",
            } + " (plus facile pour un gardien de votre faction et avec beaucoup de charisme)",
            _ => "",
        };
    }

    /// <summary>« de l'eau », « du feu »… : l'école de magie d'un sort.</summary>
    public static string SchoolName(string school) => school switch
    {
        "eau" => "de l'eau",
        "feu" => "du feu",
        "terre" => "de la terre",
        "air" => "de l'air",
        _ => school,
    };

    /// <summary>« Avancé : +20 % de mouvement sur la carte » : le rang suivant, ou le rang expert atteint.</summary>
    public static string SkillNextLine(SkillType skill, int rank) =>
        rank >= HoMM.Core.Skills.MaxRank
            ? $"Expert : {SkillEffect(skill, rank)}"
            : $"{Capitalize(RankName(rank + 1))} : {SkillEffect(skill, rank + 1)}";

    public static string LearnSkillReason(LearnSkillResult result) => result switch
    {
        LearnSkillResult.NoPoint => "Aucun point de compétence (un par niveau)",
        LearnSkillResult.MaxRank => "Déjà expert",
        LearnSkillResult.TooManySkills => $"Six compétences au plus",
        LearnSkillResult.RowLocked => "Rangée pas encore ouverte : dépenser plus de points dans cet arbre",
        _ => "",
    };

    /// <summary>Un sort du livre (écran du héros) : « Flèche magique · niveau 1 · 5 points ».</summary>
    public static string SpellLine(SpellType spell, Hero? hero = null) => $"{spell.Name} · niveau {spell.Level} · {SpellCost(spell, hero)}";

    /// <summary>« 30 / 100 points » : le commandement d'une troupe.</summary>
    public static string CommandPoints(CreatureStack stack, int commandLimit) =>
        $"{Command.Cost(stack.Type, stack.Count)} / {commandLimit} pts";

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
    public static string QuitTown => "Quitter";
    public static string QuitTownTooltip => "Revenir à la carte";

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
        string click = building.Dwelling != null ? "Clic : recruter" : building.Tavern ? "Clic : la taverne"
            : building.MageGuild > 0 ? "Clic : la guilde des mages" : building.DailyGold > 0 ? "Clic : construire" : "";
        return string.Join("\n", new[] { building.Name, effect, click }.Where(s => s.Length > 0));
    }

    public static string BuildTitle(Town town) => $"Construire à {town.Name}";
    public static string ToBuild => "Un bâtiment par jour";
    public static string BuiltMark => "✓ Construit";
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

    // ----------------------------------------------------------- magie (étape 9)

    /// <summary>« a, b et c ».</summary>
    public static string List(IReadOnlyList<string> items) =>
        items.Count <= 1 ? string.Join("", items) : string.Join(", ", items.Take(items.Count - 1)) + " et " + items[^1];

    // ----------------------------------------------------------- héros et classes (étape 9)

    public static string FactionLabel => "Faction";
    public static string YourHeroLabel => "Votre héros";
    public static string GameLabel => "La partie";
    public static string ClassNote => "La classe vient de la faction : chaque héros de cette faction la reçoit.";

    /// <summary>« Normale (54 × 40) » dans la liste des tailles de carte.</summary>
    public static string MapSizeChoice(MapSize size, (int Width, int Height) cells) => $"{MapSizeName(size)} ({cells.Width} × {cells.Height})";

    /// <summary>« Aucun », « 1 », « 2 »… dans la liste des adversaires.</summary>
    public static string OpponentsChoice(int count) => count == 0 ? "Aucun" : count.ToString();
    public static string HeroLabel => "Héros";
    public static string FactionName(string faction) => faction switch
    {
        "chateau" => "Château",
        "morts-vivants" => "Morts-vivants",
        "foret" => "Forêt",
        "tour" => "Tour de magie",
        _ => Capitalize(faction),
    };

    /// <summary>Ce que donne une classe : « Paladin : attaque 2, défense 2, puissance 1, savoir 2, charisme 14 ; sort : Soins ».</summary>
    public static string ClassSummary(HeroClass heroClass, SpellCatalog spells) =>
        $"{heroClass.Name} : attaque {heroClass.Attack}, défense {heroClass.Defense}, puissance {heroClass.Power}, "
        + $"savoir {heroClass.Knowledge}, charisme {heroClass.Charisma}"
        + (heroClass.Spells.Count > 0 ? $" ; sort{(heroClass.Spells.Count > 1 ? "s" : "")} : {List(heroClass.Spells.Select(id => spells.Get(id).Name).ToList())}" : "");

    /// <summary>« Talent : … » : le talent de la classe et ce qu'il fait (il grandit avec le niveau du héros), ou null.</summary>
    public static string? TalentText(HeroClass heroClass) => heroClass.Talent switch
    {
        Talents.Necromancy => $"Talent : nécromancie (après une victoire, {Talents.NecromancyBase:P0} des ennemis tués + {Talents.NecromancyPerLevel:P0} par niveau se relèvent en squelettes)",
        Talents.Crusader => $"Talent : croisé (+{Talents.CrusaderPerLevel:P0} de dégâts par niveau contre les morts-vivants)",
        Talents.Warlord => $"Talent : chef de guerre (la Rage des troupes monte de 1 de plus tous les {Talents.WarlordLevels} niveaux)",
        Talents.Hunter => $"Talent : chasseur (+{Talents.HunterPerLevel:P0} de dégâts par niveau pour les tireurs)",
        Talents.Scholar => $"Talent : érudit (+{Talents.ScholarPercentPerLevel} % de force des sorts par niveau)",
        Talents.Pact => $"Talent : pacte (le prix du sang peut rendre {Talents.PactPerLevel} point de sort de plus par niveau dans un combat)",
        Talents.Dread => $"Talent : effroi (l'armée ennemie commence le combat à −1 de moral, −1 de plus tous les {Talents.DreadLevels} niveaux)",
        _ => null,
    };

    /// <summary>
    /// Ce que donne une classe, sur quelques lignes : les caractéristiques, le sort, le talent, l'armée de départ.
    /// </summary>
    public static string ClassDetails(HeroClass heroClass, GameData data)
    {
        var lines = new List<string> { ClassSummary(heroClass, data.Spells).Split(" : ", 2)[1] };
        if (TalentText(heroClass) is { } talent)
            lines.Add(talent);
        if (heroClass.Army.Count > 0)
            lines.Add("Armée : " + string.Join(", ", heroClass.Army.Select(s => $"{s.Count} {Creature(data.Creatures.Get(s.Creature), s.Count)}")));
        return string.Join("\n", lines);
    }

    /// <summary>« SR » pour Sire Roland (un héros sans portrait).</summary>
    public static string Initials(string name) =>
        string.Concat(name.Split(' ').Where(w => w.Length > 2).Take(2).Select(w => char.ToUpper(w[0])));

    public static string GuildButton => "Guilde";
    public static string GuildTitle(Town town) => $"Guilde des mages de {town.Name}";
    public static string GuildText =>
        "Un héros qui entre dans la ville apprend tous les sorts de la guilde et retrouve tous ses points de sort.";
    public static string GuildLevel(int level) => $"Niveau {level}";
    public static string GuildLevelMissing => "pas encore construit";
    public static string SpellKnown => "connu";
    public static string NoGuild => "Cette ville n'a pas encore de guilde des mages.";
    /// <summary>« 5 points » : ce que coûte le sort, à ce héros s'il est donné (son école de magie le rend moins cher).</summary>
    public static string SpellCost(SpellType spell, Hero? hero = null) => $"{hero?.SpellCost(spell) ?? spell.Cost} points";

    /// <summary>
    /// Ce que fait un sort, en une phrase ; avec un héros, la force calculée d'après sa puissance (« 30 dégâts »), sinon
    /// la formule (« 10 + 10 × puissance dégâts »).
    /// </summary>
    public static string SpellEffectText(SpellType spell, Hero? hero = null)
    {
        string strength = hero != null ? $"{spell.Base + spell.PerPower * hero.Power}"
            : spell.PerPower > 0 ? $"{spell.Base} + {spell.PerPower} × puissance" : $"{spell.Base}";
        return spell.Effect switch
        {
            SpellEffect.Damage when spell.Target == SpellTarget.Area => $"{strength} dégâts sur une case et ses 6 voisines (amis compris)",
            SpellEffect.Damage when spell.Target == SpellTarget.Chain =>
                $"{strength} dégâts à une troupe ennemie, puis moitié moins à chaque saut vers la troupe la plus proche ({SpellType.ChainTargets} au plus, amis compris)",
            SpellEffect.Damage => $"{strength} dégâts à une troupe ennemie",
            SpellEffect.Bless => $"+{spell.Amount} % de dégâts à une troupe amie",
            SpellEffect.Curse => $"−{spell.Amount} % de dégâts à une troupe ennemie",
            SpellEffect.Bloodlust => $"+{spell.Amount} attaque à une troupe amie",
            SpellEffect.ArrowShield => $"Les tirs font {spell.Amount} % de dégâts en moins à une troupe amie",
            SpellEffect.Blind => "Une troupe ennemie ne joue plus jusqu'à ce qu'on la frappe (elle ne riposte pas à ce coup)",
            SpellEffect.Teleport => "Une troupe amie va sur n'importe quelle case libre",
            SpellEffect.Dispel => "Retire tous les sorts d'une troupe, amie ou ennemie",
            SpellEffect.StoneSkin => $"+{spell.Amount} défense à une troupe amie",
            SpellEffect.Haste => $"+{spell.Amount} vitesse à une troupe amie",
            SpellEffect.Slow => "Vitesse divisée par deux pour une troupe ennemie",
            SpellEffect.Weakness => $"−{spell.Amount} attaque à une troupe ennemie",
            SpellEffect.Cure => $"Soigne {strength} PV d'une troupe amie et retire les sorts néfastes",
            SpellEffect.Resurrection => $"Ramène {strength} PV de créatures tuées dans ce combat (troupe amie)",
            _ => "",
        };
    }

    public static string TavernTitle => "Taverne";
    public static string TavernText => $"Deux héros se proposent. Chacun coûte {GameState.HeroCost} or et arrive avec quelques créatures du premier niveau de la ville.";
    public static string HireButton => "Recruter";
    public static string NoTavern => "Cette ville n'a pas encore de taverne.";

    public static string SplitTitle(CreatureType type) => $"Partager les {type.Plural}";
    public static string SplitText(CreatureType type, int moved, int total) =>
        $"{moved} {Creature(type, moved)} d'un côté, {total - moved} de l'autre.";

    // ------------------------------------------------------------------ écran de combat et bilan (étape 7)

    /// <summary>L'info-bulle d'un bouton avec sa touche (la touche n'est plus écrite sur le bouton).</summary>
    public static string KeyTooltip(string tooltip, string key) => $"{tooltip} ({key})";

    public static string HeroBonus(Hero hero) => $"Attaque +{hero.Attack} · défense +{hero.Defense}";
    public static string SpellBookButton => "Sorts";
    public static string BattleOptionsButton => "Options";
    public static string BattleOptionsTitle => "Options du combat";
    public static string ProjectileSpeedLabel => "Vitesse des projectiles (flèches, sorts lancés…)";
    public static readonly string[] ProjectileSpeedNames = { "Lente", "Normale", "Rapide" };

    // ------------------------------------------------------------------ marché

    public static string MarketButton => "Marché";
    public static string MarketTitle => "Marché";
    public static string MarketGive => "Donner";
    public static string MarketReceive => "Recevoir";
    public static string TradeButton => "Échanger";
    public static string MaxButton => "Max";

    /// <summary>Le taux du joueur : « 1 marché : vous vendez à 20 % de la valeur (plus de marchés, meilleurs taux). »</summary>
    public static string MarketRates(int markets) =>
        $"{markets} marché{(markets > 1 ? "s" : "")} : vous vendez à {Market.RateTenths(markets) * 10} % de la valeur"
        + (markets < 5 ? " (chaque marché de plus améliore les taux)." : " (le meilleur taux).");

    /// <summary>
    /// Le taux entre deux ressources : le prix d'une unité (« 63 bois ») ou, si une unité donnée en rapporte plusieurs,
    /// combien elle rapporte (« 20 / bois ») ; « — » pour la même ressource.
    /// </summary>
    public static string MarketUnitPrice(ResourceType give, ResourceType receive, int markets)
    {
        if (give == receive)
            return "—";
        int perOne = Market.MaxReceivable(give, receive, 1, markets);
        return perOne > 1 ? $"{perOne} / {Resource(give)}" : $"{Market.Cost(give, receive, 1, markets)} {Resource(give)}";
    }

    public static string MarketDeal(ResourceType give, int cost, ResourceType receive, int count) =>
        count == 0 ? "Choisissez combien recevoir." : $"Vous donnez {cost} {Resource(give)} et recevez {count} {Resource(receive)}.";

    public static string MarketNothing(ResourceType give, ResourceType receive) =>
        give == receive ? "Choisissez deux ressources différentes." : $"Pas assez de {Resource(give)} pour en recevoir.";

    public static string SpellBookTitle(Hero hero) => $"Livre de sorts de {hero.Name}";
    public static string SpellPointsLine(int points, int max) => $"Points de sort : {points} / {max}";
    public static string SpellPointsDaily => "+1 par jour, tous dans une ville qui a une guilde des mages";
    public static string OneSpellPerRound => "Un seul sort par round : le livre se lit seulement.";
    public static string NotYourTurnToCast => "Ce n'est pas votre tour : le livre se lit seulement.";
    public static string SpellBookShort => "Sorts";
    public static string SpellBookOnMap => "Les sorts se lancent au combat.";
    public static string SpellBookTooltip => "Livre de sorts du héros";
    public static string NotEnoughSpellPoints => "pas assez de points";
    public static string NoSpellsKnown => "Aucun sort : un héros en apprend dans une ville qui a une guilde des mages.";
    public static string SpellTargetPrompt(SpellType spell) => spell.Target switch
    {
        SpellTarget.Any => $"{spell.Name} : choisissez une troupe (clic droit ou Échap : annuler).",
        SpellTarget.Area => $"{spell.Name} : choisissez une case (clic droit ou Échap : annuler).",
        SpellTarget.Ally => $"{spell.Name} : choisissez une de vos troupes (clic droit ou Échap : annuler).",
        _ => $"{spell.Name} : choisissez une troupe ennemie (clic droit ou Échap : annuler).",
    };

    public static string TeleportDestinationPrompt(SpellType spell) =>
        $"{spell.Name} : choisissez où envoyer la troupe (clic droit ou Échap : annuler).";

    /// <summary>Le héros au combat (clic sur lui) : son bonus, ses points de sort et ses sorts.</summary>
    public static string BattleHeroText(Hero hero, int spellPoints) =>
        $"{HeroBonus(hero)}\n{SpellPointsLine(spellPoints, hero.MaxSpellPoints)}"
        + (hero.Spells.Count > 0 ? $"\nSorts : {List(hero.Spells.Select(s => s.Name).ToList())}" : "");
    public static string BattleLogButton => "Journal";
    public static string BattleStart => "Commencer";
    public static string BattlePlacementTitle => "Placement";

    /// <summary>La Tactique : ce que le joueur peut faire avant le premier round.</summary>
    public static string BattlePlacementPrompt(int columns) =>
        $"Tactique : placez vos troupes dans vos {columns} premières colonnes (cliquez une troupe, puis sa case), puis « Commencer ».";
    public static string BattleLogTitle => "Journal du combat";

    /// <summary>La fiche d'une troupe au combat, sur quelques lignes (ses caractéristiques, bonus du héros compris).</summary>
    public static string UnitStats(BattleUnit unit) =>
        $"Attaque {unit.Attack} · défense {unit.Defense}\n"
        + $"Dégâts {unit.Type.MinDamage}–{unit.Type.MaxDamage} · vitesse {unit.Type.Speed}\n"
        + $"PV de tête {unit.TopHitPoints} / {unit.Type.HitPoints}\n"
        + MoraleAndLuck(unit.Morale, unit.Luck);

    public static string UnitAbilities(BattleUnit unit) =>
        unit.Type.Abilities.Count == 0 ? "Pas de capacité" : Capitalize(string.Join(", ", unit.Type.Abilities.Select(AbilityName)));

    public static string UnitState(BattleUnit unit, int round = 0) =>
        $"Ripostes : {unit.RetaliationsLeft}" + (unit.IsDefending ? " · se défend" : "")
        + (unit.Spells.Count > 0
            ? "\nSorts : " + string.Join(", ", unit.Spells.Select(s => round > 0 ? $"{s.Spell.Name} ({Rounds(s.LastRound - round + 1)})" : s.Spell.Name))
            : "");

    private static string Rounds(int n) => $"{n} round{(n > 1 ? "s" : "")}";


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
