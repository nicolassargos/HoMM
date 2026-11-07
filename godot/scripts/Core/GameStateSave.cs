using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoMM.Core;

/// <summary>
/// Sauvegarde et chargement d'une partie, en JSON. Tout est enregistré (terrain compris) : une partie
/// chargée ne dépend pas du générateur de cartes, qui peut changer d'une version du jeu à l'autre.
/// Les objets, héros et joueurs se retrouvent par leur identifiant.
/// </summary>
/// <summary>Le résumé d'une sauvegarde (voir <see cref="GameState.ReadSummary"/>).</summary>
public sealed record SaveSummary(int Day, string? HeroName, int HeroLevel, MapSize Size, GameOutcome Outcome);

public sealed partial class GameState
{
    /// <summary>Version du format. À augmenter si le format change (une ancienne sauvegarde est alors refusée).</summary>
    /// <summary>
    /// Version 2 : les routes sont une couche à part (<see cref="MapData.Roads"/>). Une sauvegarde de la version 1
    /// (où la route était un terrain, le numéro 1) est encore lue : ses routes deviennent de l'herbe avec une route.
    /// </summary>
    public const int SaveVersion = 3; // 3 : les mines sur 3 × 2 cases

    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    // ------------------------------------------------------------- le fichier

    private sealed record SaveFile(
        int Version, int Seed, int Day, int CurrentPlayer, int? CurrentHero, GameOutcome Outcome, GameRules Rules,
        ulong Random, int NextPlayerId, int NextHeroId,
        MapData Map, List<PlayerData> Players, List<HeroData> Heroes, List<ChoiceData> Choices);

    /// <summary>
    /// Le terrain : une ligne de chiffres par rangée de la carte (le numéro du terrain). Les routes : une ligne
    /// par rangée, « 1 » pour une case avec une route, « 0 » sinon (absentes dans la version 1).
    /// </summary>
    private sealed record MapData(int Width, int Height, Cell Start, List<string> Terrain, List<ObjectData> Objects,
        List<string>? Roads = null, Cell? Treasure = null);

    /// <summary>Un objet de la carte. <paramref name="Kind"/> dit lequel ; seuls ses champs sont remplis.</summary>
    private sealed record ObjectData(
        string Kind, int Id, Cell Position,
        ResourceType? Resource = null, int? Amount = null, int? Owner = null,
        string? Creature = null, int? InitialCount = null,
        string? Name = null, string? Faction = null, List<string>? Buildings = null, bool BuiltToday = false,
        Dictionary<string, int>? Available = null, List<StackData>? Garrison = null, List<string>? GuildSpells = null);

    private sealed record StackData(string Creature, int Count);

    private sealed record PlayerData(int Id, string Name, Dictionary<ResourceType, int> Resources, string Fog,
        List<int> Mines, List<int> Towns, List<int>? Obelisks = null, StatisticsData? Statistics = null, bool IsComputer = false);

    /// <summary>Les chiffres de la partie d'un joueur (absents des anciennes sauvegardes : zéro).</summary>
    private sealed record StatisticsData(int BattlesWon, int CreaturesKilled, int CreaturesLost, int TownsCaptured);

    private sealed record HeroData(
        int Id, string Name, int Owner, Cell Position, int Movement, int MaxMovement, int VisionRadius,
        int Attack, int Defense, int Power, int Knowledge, int Charisma, int Level, int Experience,
        List<StackData> Army, List<Cell>? Path, List<string>? Artifacts = null, int? SpellPoints = null, List<string>? Spells = null,
        string? Class = null);

    /// <summary>Un niveau gagné dont la caractéristique n'a pas encore été choisie.</summary>
    private sealed record ChoiceData(int Hero, int Level, List<HeroStat> Options);

    // ------------------------------------------------------------- sauvegarde

    /// <summary>La partie entière, en JSON (les événements pas encore affichés ne sont pas gardés).</summary>
    public string Save()
    {
        if (CurrentBattle != null)
            throw new InvalidOperationException("On ne peut pas sauvegarder pendant un combat.");
        var save = new SaveFile(
            SaveVersion, Seed, Day, _currentPlayer, CurrentHero?.Id, Outcome, Rules,
            _rng.State, _nextPlayerId, _nextHeroId,
            SaveMap(),
            Players.Select(p => new PlayerData(p.Id, p.Name, new Dictionary<ResourceType, int>(p.Resources),
                p.Fog.Export(), p.Mines.Select(m => m.Id).ToList(), p.Towns.Select(t => t.Id).ToList(), p.Obelisks.OrderBy(id => id).ToList(),
                new StatisticsData(p.Statistics.BattlesWon, p.Statistics.CreaturesKilled, p.Statistics.CreaturesLost, p.Statistics.TownsCaptured),
                p.IsComputer)).ToList(),
            AllHeroes.Select(h => new HeroData(
                h.Id, h.Name, h.Owner.Id, h.Position, h.Movement, h.MaxMovement, h.VisionRadius,
                h.Attack, h.Defense, h.Power, h.Knowledge, h.Charisma, h.Level, h.Experience,
                Stacks(h.Army), _paths.GetValueOrDefault(h)?.ToList(), h.Artifacts.Count > 0 ? h.Artifacts.Select(a => a.Id).ToList() : null,
                h.SpellPoints, h.Spells.Count > 0 ? h.Spells.Select(s => s.Id).ToList() : null, h.Class?.Id)).ToList(),
            _pendingChoices.Select(c => new ChoiceData(c.Hero.Id, c.Level, c.Options.ToList())).ToList());
        return JsonSerializer.Serialize(save, SaveOptions);
    }

    private MapData SaveMap()
    {
        var terrain = new List<string>();
        for (int y = 0; y < Map.Height; y++)
            terrain.Add(string.Concat(Enumerable.Range(0, Map.Width).Select(x => (char)('0' + (int)Map.GetTerrain(new Cell(x, y))))));
        var roads = new List<string>();
        for (int y = 0; y < Map.Height; y++)
            roads.Add(string.Concat(Enumerable.Range(0, Map.Width).Select(x => Map.HasRoad(new Cell(x, y)) ? '1' : '0')));
        var objects = Map.Objects.OrderBy(o => o.Id).Select(SaveObject).ToList();
        return new MapData(Map.Width, Map.Height, Map.Start, terrain, objects, roads, Map.Treasure);
    }

    private static ObjectData SaveObject(MapObject obj) => obj switch
    {
        ResourcePile pile => new ObjectData("pile", obj.Id, obj.Position, Resource: pile.Resource, Amount: pile.Amount),
        TreasureChest chest => new ObjectData("chest", obj.Id, obj.Position, Amount: chest.Gold),
        Obelisk => new ObjectData("obelisk", obj.Id, obj.Position),
        Mine mine => new ObjectData("mine", obj.Id, obj.Position, Resource: mine.Resource, Owner: mine.Owner?.Id),
        MonsterGuard guard => new ObjectData("guard", obj.Id, obj.Position, Creature: guard.Type.Id,
            InitialCount: guard.InitialCount, Amount: guard.Count),
        Town town => new ObjectData("town", obj.Id, obj.Position, Owner: town.Owner?.Id,
            Name: town.Name, Faction: town.Faction, Buildings: town.Built.ToList(), BuiltToday: town.BuiltToday,
            Available: town.Available.ToDictionary(a => a.Key.Id, a => a.Value), Garrison: Stacks(town.Garrison),
            GuildSpells: town.GuildSpells.Count > 0 ? town.GuildSpells.Select(s => s.Id).ToList() : null),
        _ => throw new InvalidOperationException($"Objet de carte non prévu dans la sauvegarde : {obj.GetType().Name}"),
    };

    private static List<StackData> Stacks(Army army) => army.Stacks.Select(s => new StackData(s.Type.Id, s.Count)).ToList();

    // ------------------------------------------------------------- chargement

    /// <summary>
    /// Recrée une partie enregistrée par <see cref="Save"/>. Les choix de niveau en attente sont de nouveau
    /// proposés (événements <see cref="HeroLevelChoice"/>). Lève <see cref="FormatException"/> si le fichier
    /// est illisible, d'une autre version, ou ne correspond pas aux données du jeu.
    /// </summary>
    /// <summary>
    /// Ce qu'on montre d'une sauvegarde dans la liste (sans recréer la partie) : le jour, le héros du joueur humain
    /// (le plus avancé), son niveau, la taille de la carte. Null si le fichier est illisible ou d'une autre version.
    /// </summary>
    public static SaveSummary? ReadSummary(string json)
    {
        try
        {
            if (JsonSerializer.Deserialize<SaveFile>(json, SaveOptions) is not { Map: not null, Players.Count: > 0 } save
                || save.Version is not (1 or 2 or SaveVersion))
                return null;
            var hero = save.Heroes.Where(h => h.Owner == save.Players[0].Id)
                .OrderByDescending(h => h.Id == save.CurrentHero).ThenByDescending(h => h.Level).FirstOrDefault();
            return new SaveSummary(save.Day, hero?.Name, hero?.Level ?? 0,
                MapSizes.Closest(save.Map.Width, save.Map.Height), save.Outcome);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static GameState Load(string json, GameData data)
    {
        SaveFile? save;
        try
        {
            save = JsonSerializer.Deserialize<SaveFile>(json, SaveOptions);
        }
        catch (JsonException e)
        {
            throw new FormatException($"sauvegarde illisible : {e.Message}", e);
        }
        if (save == null)
            throw new FormatException("sauvegarde vide");
        if (save.Version is not (1 or 2 or SaveVersion))
            throw new FormatException($"version de sauvegarde inconnue : {save.Version} (attendue : {SaveVersion})");
        try
        {
            return Restore(save, data);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidCastException or ArgumentException or NullReferenceException)
        {
            throw new FormatException($"sauvegarde invalide : {e.Message}", e);
        }
    }

    private static GameState Restore(SaveFile save, GameData data)
    {
        var map = RestoreMap(save.Map);
        var state = new GameState(save.Seed, data, map)
        {
            Day = save.Day,
            Outcome = save.Outcome,
            Rules = save.Rules,
            _currentPlayer = save.CurrentPlayer,
            _nextPlayerId = save.NextPlayerId,
            _nextHeroId = save.NextHeroId,
        };
        state._rng.State = save.Random;

        foreach (var p in save.Players)
        {
            var player = new Player(p.Name, map) { Id = p.Id, IsComputer = p.IsComputer };
            foreach (var (resource, amount) in p.Resources)
                player.Resources[resource] = amount;
            if (p.Statistics is { } stats)
            {
                player.Statistics.BattlesWon = stats.BattlesWon;
                player.Statistics.CreaturesKilled = stats.CreaturesKilled;
                player.Statistics.CreaturesLost = stats.CreaturesLost;
                player.Statistics.TownsCaptured = stats.TownsCaptured;
            }
            state.Players.Add(player);
        }
        Player Owner(int id) => state.PlayerById(id) ?? throw new KeyNotFoundException($"joueur inconnu : {id}");

        foreach (var o in save.Map.Objects)
        {
            MapObject obj = o.Kind switch
            {
                "pile" => new ResourcePile(o.Position, o.Resource!.Value, o.Amount!.Value),
                "chest" => new TreasureChest(o.Position, o.Amount!.Value),
                "obelisk" => new Obelisk(o.Position),
                // avant la version 3, une mine n'occupait qu'une case : ses voisines peuvent être prises
                "mine" => new Mine(o.Position, o.Resource!.Value) { Owner = o.Owner is { } id ? Owner(id) : null, Large = save.Version >= 3 },
                "guard" => new MonsterGuard(o.Position, data.Creatures.Get(o.Creature!), o.InitialCount!.Value, o.Amount!.Value),
                "town" => RestoreTown(o, data, id => Owner(id)),
                _ => throw new KeyNotFoundException($"objet de carte inconnu : {o.Kind}"),
            };
            obj.Id = o.Id;
            map.AddObject(obj);
        }
        // Les listes du joueur, dans leur ordre d'origine
        foreach (var p in save.Players)
        {
            var player = Owner(p.Id);
            player.Mines.AddRange(p.Mines.Select(id => (Mine)ObjectOrThrow(map, id)));
            player.Towns.AddRange(p.Towns.Select(id => (Town)ObjectOrThrow(map, id)));
            if (p.Obelisks != null)
                player.Obelisks.UnionWith(p.Obelisks);
        }

        foreach (var h in save.Heroes)
        {
            var hero = new Hero(h.Name, Owner(h.Owner), h.Position)
            {
                Id = h.Id,
                MaxMovement = h.MaxMovement,
                Movement = h.Movement,
                VisionRadius = h.VisionRadius,
                Attack = h.Attack,
                Defense = h.Defense,
                Power = h.Power,
                Knowledge = h.Knowledge,
                Charisma = h.Charisma,
                Level = h.Level,
                Experience = h.Experience,
                // les anciennes sauvegardes n'ont pas de points de sort : le héros est plein
                SpellPoints = h.SpellPoints ?? Hero.SpellPointsPerKnowledge * h.Knowledge,
                // les anciennes sauvegardes n'ont pas de classe : celle de la faction de départ (les caractéristiques restent)
                Class = h.Class is { } classId ? data.Classes.Get(classId) : data.Classes.ForFaction(StartingFaction),
            };
            foreach (var id in h.Spells ?? new List<string>())
                hero.Learn(data.Spells.Get(id));
            foreach (var s in h.Army)
                hero.Army.AddStack(data.Creatures.Get(s.Creature), s.Count);
            // ses caractéristiques comptent déjà leurs bonus
            foreach (var id in h.Artifacts ?? new List<string>())
                hero.Artifacts.Add(data.Artifacts.Get(id));
            if (h.Path != null)
                state._paths[hero] = h.Path.ToList();
        }
        // Après les héros : leur création a déjà découvert un peu de brouillard
        foreach (var p in save.Players)
            Owner(p.Id).Fog.Import(p.Fog);

        state.CurrentHero = save.CurrentHero is { } heroId ? state.HeroById(heroId) : null;
        foreach (var c in save.Choices)
        {
            var hero = state.HeroById(c.Hero) ?? throw new KeyNotFoundException($"héros inconnu : {c.Hero}");
            var choice = new HeroLevelChoice(hero, c.Level, c.Options);
            state._pendingChoices.Add(choice);
            state.Emit(choice);
        }
        return state;
    }

    /// <summary>Le numéro de la route quand elle était un terrain (sauvegardes de la version 1).</summary>
    private const int OldRoadTerrain = 1;

    private static AdventureMap RestoreMap(MapData m)
    {
        var map = new AdventureMap(m.Width, m.Height) { Start = m.Start, Treasure = m.Treasure };
        if (m.Terrain.Count != m.Height || m.Terrain.Any(row => row.Length != m.Width))
            throw new FormatException("terrain de taille incorrecte");
        if (m.Roads != null && (m.Roads.Count != m.Height || m.Roads.Any(row => row.Length != m.Width)))
            throw new FormatException("routes de taille incorrecte");
        for (int y = 0; y < m.Height; y++)
        for (int x = 0; x < m.Width; x++)
        {
            var cell = new Cell(x, y);
            int terrain = m.Terrain[y][x] - '0';
            // version 1 : le terrain 1 était la route
            if (terrain == OldRoadTerrain && m.Roads == null)
            {
                map.SetTerrain(cell, TerrainType.Grass);
                map.SetRoad(cell, true);
                continue;
            }
            if (!Enum.IsDefined(typeof(TerrainType), terrain))
                throw new FormatException($"terrain inconnu en ({x}, {y})");
            map.SetTerrain(cell, (TerrainType)terrain);
            if (m.Roads != null)
                map.SetRoad(cell, m.Roads[y][x] switch
                {
                    '1' => true,
                    '0' => false,
                    _ => throw new FormatException($"route illisible en ({x}, {y})"),
                });
        }
        return map;
    }

    private static Town RestoreTown(ObjectData o, GameData data, Func<int, Player> owner)
    {
        var town = new Town(o.Position, o.Name!, o.Faction!, data)
        {
            Owner = o.Owner is { } id ? owner(id) : null,
            BuiltToday = o.BuiltToday,
        };
        foreach (var building in o.Buildings ?? new List<string>())
            town.RestoreBuilding(building);
        foreach (var (creature, count) in o.Available ?? new Dictionary<string, int>())
            town.Available[data.Creatures.Get(creature)] = count;
        foreach (var s in o.Garrison ?? new List<StackData>())
            town.Garrison.AddStack(data.Creatures.Get(s.Creature), s.Count);
        foreach (var spellId in o.GuildSpells ?? new List<string>())
            town.RestoreGuildSpell(data.Spells.Get(spellId));
        return town;
    }

    private static MapObject ObjectOrThrow(AdventureMap map, int id) =>
        map.ObjectById(id) ?? throw new KeyNotFoundException($"objet inconnu : {id}");
}
