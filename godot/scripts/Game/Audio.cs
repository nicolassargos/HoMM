using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Les sons et la musique du jeu. Un seul nœud, posé sur la racine (il survit quand la scène est relancée : la musique
/// ne s'arrête pas entre le menu et la partie). Les fichiers sont décrits dans <c>data/sounds.json</c> :
///   « music »    : les musiques (« menu », « map », « town », « town-chateau », « battle »…), qui bouclent ;
///   « sounds »   : les sons courts (« click », « build », « victory »…) ;
///   « ambience » : les ambiances, de longues boucles discrètes ;
///   « lengths »  : la durée où couper un son court trop long (en secondes, il s'efface à la fin) ;
///   « gaits »    : les façons de marcher au combat (pas par case, créatures), pour les pas (<see cref="PlayStep"/>).
/// Chaque nom donne un ou plusieurs fichiers de <c>audio/</c> (.ogg, .wav ou .mp3), tirés au hasard pour ne pas répéter.
/// Au combat, une créature a ses sons : « archer-shoot », « griffon-attack », « ange-impact », « ange-death »… (son id
/// et son action, <see cref="PlayCreature"/>) ; les sorts, « spell-boule-de-feu »….
/// Les fichiers sont lus directement (pas besoin de les importer avec l'éditeur Godot), comme les images (<see cref="Art"/>).
/// Trois bus : Musique, Effets, Ambiance, dont les volumes viennent des réglages (<see cref="Settings"/>).
/// </summary>
public partial class Audio : Node
{
    public const string Root = "res://audio/";
    public const string MusicBus = "Musique";
    public const string EffectsBus = "Effets";
    public const string AmbienceBus = "Ambiance";

    /// <summary>Le temps du fondu entre deux musiques, en secondes.</summary>
    private const double FadeTime = 1.2;

    /// <summary>Le volume d'une musique qui s'efface (en décibels : on ne l'entend plus).</summary>
    private const float Silent = -60;

    private static Audio? _instance;

    private readonly Dictionary<string, string[]> _music = new();
    private readonly Dictionary<string, string[]> _sounds = new();
    private readonly Dictionary<string, string[]> _ambiences = new();
    private readonly Dictionary<string, double> _lengths = new();
    private readonly Dictionary<string, AudioStream?> _cache = new();
    private readonly Random _rng = new();

    /// <summary>Deux lecteurs de musique : l'un s'efface pendant que l'autre arrive.</summary>
    private readonly AudioStreamPlayer[] _musicPlayers = new AudioStreamPlayer[2];
    private readonly Tween?[] _musicFades = new Tween?[2];
    private int _musicIndex;
    private string? _musicId;
    private string? _musicFile;

    /// <summary>Deux lecteurs d'ambiance, comme pour la musique : l'un s'efface pendant que l'autre arrive.</summary>
    private readonly AudioStreamPlayer[] _ambiencePlayers = new AudioStreamPlayer[2];
    private readonly Tween?[] _ambienceFades = new Tween?[2];
    private int _ambienceIndex;
    private string? _ambienceId;

    /// <summary>Les lecteurs des sons courts (plusieurs peuvent jouer en même temps).</summary>
    private readonly List<AudioStreamPlayer> _effects = new();

    /// <summary>L'effacement d'un son coupé, par lecteur (un lecteur repris ne doit pas être coupé par l'ancien).</summary>
    private readonly Dictionary<AudioStreamPlayer, Tween> _cuts = new();

    /// <summary>Le temps où un son coupé s'efface, en secondes.</summary>
    private const double CutFade = 0.3;

    private const int EffectPlayers = 8;

    /// <summary>La musique qui joue (son nom dans <c>sounds.json</c>), ou null.</summary>
    public static string? CurrentMusic => _instance?._musicId;

    /// <summary>
    /// Crée le nœud du son (une seule fois, au lancement) : les bus, le catalogue, le volume des réglages ; et un clic
    /// pour chaque bouton du jeu, où qu'il soit.
    /// </summary>
    public static void Start(SceneTree tree)
    {
        if (_instance != null)
            return;
        foreach (var bus in new[] { MusicBus, EffectsBus, AmbienceBus })
            BusIndex(bus);
        ApplyVolumes();
        var audio = new Audio { Name = "Audio", ProcessMode = ProcessModeEnum.Always };
        audio.ReadCatalog(Godot.FileAccess.GetFileAsString("res://data/sounds.json"));
        _instance = audio;
        // la racine est occupée pendant _Ready : le nœud y entre juste après
        var root = tree.Root;
        Callable.From(() => root.AddChild(audio)).CallDeferred();
        tree.NodeAdded += node =>
        {
            // un bouton déplacé d'un parent à un autre (le contenu d'une fenêtre) revient ici : un seul clic
            if (node is BaseButton button && !button.IsConnected(BaseButton.SignalName.Pressed, Click))
                button.Connect(BaseButton.SignalName.Pressed, Click);
        };
    }

    private static readonly Callable Click = Callable.From(() => Play("click"));

    public Audio()
    {
        for (int i = 0; i < _musicPlayers.Length; i++)
        {
            int index = i;
            _musicPlayers[i] = new AudioStreamPlayer { Bus = MusicBus };
            // une musique finie : la suivante du même nom (une autre si elle en a plusieurs)
            _musicPlayers[i].Finished += () =>
            {
                if (index == _musicIndex && _musicId != null)
                    StartMusic(index, NextFile(_music[_musicId], _musicFile), fade: false);
            };
            AddChild(_musicPlayers[i]);
        }
        for (int i = 0; i < _ambiencePlayers.Length; i++)
        {
            int index = i;
            var player = _ambiencePlayers[i] = new AudioStreamPlayer { Bus = AmbienceBus };
            // une ambiance finie recommence (un autre de ses fichiers, s'il y en a plusieurs)
            player.Finished += () =>
            {
                if (index == _ambienceIndex && _ambienceId != null && Pick(_ambiences, _ambienceId) is { } stream)
                {
                    player.Stream = stream;
                    player.Play();
                }
            };
            AddChild(player);
        }
        for (int i = 0; i < EffectPlayers; i++)
        {
            var player = new AudioStreamPlayer { Bus = EffectsBus };
            _effects.Add(player);
            AddChild(player);
        }
    }

    public override void _Ready()
    {
        // une musique demandée avant que le nœud soit dans l'arbre (le menu, au lancement)
        if (_musicId is { } id)
        {
            _musicId = null;
            PlayMusic(id);
        }
        if (_ambienceId is { } ambience)
        {
            _ambienceId = null;
            PlayAmbience(ambience);
        }
    }

    private void ReadCatalog(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;
        using var document = JsonDocument.Parse(json);
        foreach (var (section, table) in new[] { ("music", _music), ("sounds", _sounds), ("ambience", _ambiences) })
        {
            if (!document.RootElement.TryGetProperty(section, out var entries))
                continue;
            foreach (var entry in entries.EnumerateObject())
                table[entry.Name] = entry.Value.EnumerateArray().Select(f => f.GetString() ?? "").Where(f => f != "").ToArray();
        }
        if (document.RootElement.TryGetProperty("lengths", out var lengths))
            foreach (var entry in lengths.EnumerateObject())
                _lengths[entry.Name] = entry.Value.GetDouble();
        if (document.RootElement.TryGetProperty("gaits", out var gaits))
            foreach (var gait in gaits.EnumerateObject().Where(g => g.Value.ValueKind == JsonValueKind.Object))
            {
                _stepsPerCell[gait.Name] = gait.Value.GetProperty("perCell").GetInt32();
                if (gait.Value.TryGetProperty("creatures", out var creatures))
                    foreach (var creature in creatures.EnumerateArray())
                        _gaits[creature.GetString() ?? ""] = gait.Name;
            }
    }

    /// <summary>La façon de marcher de chaque créature (« heavy », « hooves »…) ; « feet » pour celles qui n'en ont pas.</summary>
    private readonly Dictionary<string, string> _gaits = new();

    /// <summary>Le nombre de pas par case traversée, par façon de marcher.</summary>
    private readonly Dictionary<string, int> _stepsPerCell = new();

    private static string GaitOf(CreatureType creature) =>
        _instance?._gaits.GetValueOrDefault(creature.Id) ?? "feet";

    /// <summary>Combien de pas fait cette créature pour traverser une case (2 à pied, 4 au trot…).</summary>
    public static int StepsPerCell(CreatureType creature) =>
        _instance?._stepsPerCell.GetValueOrDefault(GaitOf(creature), 2) ?? 2;

    /// <summary>
    /// Un pas d'une créature au combat, selon sa façon de marcher et le sol du champ : « step-heavy-snow », sinon
    /// « step-heavy » (un même son sur tous les sols : ramper), sinon le pas à pied sur ce sol.
    /// </summary>
    public static void PlayStep(CreatureType creature, TerrainType terrain)
    {
        if (_instance is not { } audio)
            return;
        string gait = GaitOf(creature), ground = GroundOf(terrain);
        Play(new[] { $"step-{gait}-{ground}", $"step-{gait}", $"step-feet-{ground}" }.FirstOrDefault(audio._sounds.ContainsKey) ?? "",
            StepVolume);
    }

    /// <summary>Les pas restent derrière les coups et les cris.</summary>
    private const float StepVolume = -8;

    /// <summary>Le sol des pas : la forêt et la mer (en attendant un pont de navire) sonnent comme l'herbe, la montagne comme la roche.</summary>
    private static string GroundOf(TerrainType terrain) => terrain switch
    {
        TerrainType.Sand => "sand",
        TerrainType.Swamp => "swamp",
        TerrainType.Rock or TerrainType.Mountain => "rock",
        TerrainType.Snow => "snow",
        TerrainType.Burnt => "burnt",
        _ => "grass",
    };

    /// <summary>
    /// Joue un son court (s'il existe : un son absent du catalogue ne fait rien). Sa hauteur varie un peu à chaque fois,
    /// pour qu'il ne paraisse pas répété. <paramref name="volumeDb"/> : plus bas pour un son de fond (les pas).
    /// </summary>
    public static void Play(string id, float volumeDb = 0)
    {
        if (_instance is not { } audio || !audio.IsInsideTree() || audio.Pick(audio._sounds, id) is not { } stream)
            return;
        var player = audio._effects.FirstOrDefault(p => !p.Playing) ?? audio._effects[0];
        audio._effects.Remove(player);
        audio._effects.Add(player); // le plus ancien sera repris en premier si tous jouent
        if (audio._cuts.Remove(player, out var cut))
            cut.Kill();
        player.Stream = stream;
        player.VolumeDb = volumeDb;
        player.PitchScale = 1 + (float)(audio._rng.NextDouble() - 0.5) * 0.04f;
        player.Play();
        if (audio._lengths.TryGetValue(id, out double length))
        {
            var fade = audio._cuts[player] = audio.CreateTween();
            fade.TweenInterval(Math.Max(0, length - CutFade));
            fade.TweenProperty(player, "volume_db", Silent, CutFade);
            fade.TweenCallback(Callable.From(() =>
            {
                player.Stop();
                audio._cuts.Remove(player);
            }));
        }
    }

    /// <summary>
    /// Le son d'une créature au combat : « attack » (au début de son geste), « shoot » (un tir), « impact » (quand son
    /// coup porte), « shot-impact » (quand son tir touche), « death » (des créatures de la troupe meurent). Sans son
    /// propre son, une créature prend celui de tout le combat (« battle-impact », aussi pour un tir, « battle-death ») ;
    /// il n'y en a pas pour le geste ni le tir.
    /// </summary>
    public static void PlayCreature(CreatureType creature, string action)
    {
        if (_instance is not { } audio)
            return;
        string id = $"{creature.Id}-{action}";
        Play(audio._sounds.ContainsKey(id) ? id : action == "shot-impact" ? "battle-impact" : $"battle-{action}");
    }

    /// <summary>
    /// La musique de la carte à cet instant : celle du terrain autour du héros choisi (« map-sand »…), tenue à jour par
    /// Main ; « map » si ce terrain n'a pas la sienne.
    /// </summary>
    public static string MapMusic { get; set; } = "map";

    /// <summary>Revient à la musique et à l'ambiance de la carte (en sortant d'une ville ou d'un combat).</summary>
    public static void PlayMapMusic()
    {
        PlayMusic(MapMusic, "map");
        PlayAmbience(MapAmbience);
    }

    /// <summary>
    /// Passe à cette musique, en fondu : le premier de ces noms qui existe (« town-chateau », sinon « town »). Rien ne
    /// change si elle joue déjà.
    /// </summary>
    public static void PlayMusic(params string[] ids)
    {
        if (_instance is not { } audio || ids.FirstOrDefault(audio._music.ContainsKey) is not { } id || id == audio._musicId)
            return;
        audio._musicId = id;
        if (!audio.IsInsideTree())
            return; // elle commencera dans _Ready
        int oldIndex = audio._musicIndex;
        var old = audio._musicPlayers[oldIndex];
        bool fade = old.Playing;
        if (fade)
        {
            audio._musicFades[oldIndex]?.Kill();
            var fadeOut = audio._musicFades[oldIndex] = audio.CreateTween();
            fadeOut.TweenProperty(old, "volume_db", Silent, FadeTime);
            fadeOut.TweenCallback(Callable.From(old.Stop));
        }
        audio._musicIndex = 1 - oldIndex;
        audio.StartMusic(audio._musicIndex, audio.NextFile(audio._music[id], null), fade);
    }

    private void StartMusic(int index, string? file, bool fade)
    {
        var player = _musicPlayers[index];
        _musicFades[index]?.Kill(); // le fondu d'avant de ce lecteur (qui l'aurait arrêté) ne compte plus
        _musicFades[index] = null;
        _musicFile = file;
        if (file == null || Load(file) is not { } stream)
        {
            player.Stop();
            return;
        }
        player.Stream = stream;
        player.VolumeDb = fade ? Silent : 0;
        player.Play();
        if (fade)
            (_musicFades[index] = CreateTween()).TweenProperty(player, "volume_db", 0f, FadeTime);
    }

    /// <summary>L'ambiance de la carte à cet instant (le sol autour du héros choisi, « water » en bateau), tenue à jour par Main.</summary>
    public static string? MapAmbience { get; set; }

    /// <summary>L'ambiance qui joue (son nom dans <c>sounds.json</c>), ou null.</summary>
    public static string? CurrentAmbience => _instance?._ambienceId;

    /// <summary>Le nom de l'ambiance d'un sol (comme les pas, mais la mer garde la sienne).</summary>
    public static string AmbienceOf(TerrainType terrain) => terrain == TerrainType.Water ? "water" : GroundOf(terrain);

    /// <summary>
    /// Passe à cette ambiance en boucle, en fondu (null, ou une ambiance absente du catalogue : plus d'ambiance). Rien ne
    /// change si elle joue déjà.
    /// </summary>
    public static void PlayAmbience(string? id)
    {
        if (_instance is not { } audio)
            return;
        if (id != null && !audio._ambiences.ContainsKey(id))
            id = null;
        if (id == audio._ambienceId)
            return;
        audio._ambienceId = id;
        if (!audio.IsInsideTree())
            return;
        int oldIndex = audio._ambienceIndex;
        var old = audio._ambiencePlayers[oldIndex];
        audio._ambienceFades[oldIndex]?.Kill();
        if (old.Playing)
        {
            var fadeOut = audio._ambienceFades[oldIndex] = audio.CreateTween();
            fadeOut.TweenProperty(old, "volume_db", Silent, FadeTime);
            fadeOut.TweenCallback(Callable.From(old.Stop));
        }
        audio._ambienceIndex = 1 - oldIndex;
        var player = audio._ambiencePlayers[audio._ambienceIndex];
        audio._ambienceFades[audio._ambienceIndex]?.Kill();
        audio._ambienceFades[audio._ambienceIndex] = null;
        if (id == null || audio.Pick(audio._ambiences, id) is not { } stream)
        {
            player.Stop();
            return;
        }
        // un seul fichier : il boucle sans le moindre blanc (préparé par tools/audio/boucler.py) ; plusieurs : ils
        // s'enchaînent à la fin de chacun
        if (stream is AudioStreamOggVorbis ogg)
            ogg.Loop = audio._ambiences[id].Length == 1;
        player.Stream = stream;
        player.VolumeDb = Silent;
        player.Play();
        (audio._ambienceFades[audio._ambienceIndex] = audio.CreateTween()).TweenProperty(player, "volume_db", 0f, FadeTime);
    }

    /// <summary>Le son de ce que les règles viennent de faire, s'il en a un (du point de vue de <paramref name="viewer"/>).</summary>
    public static void PlayFor(GameEvent e, Player viewer)
    {
        if (SoundFor(e, viewer) is { } id)
            Play(id);
    }

    private static string? SoundFor(GameEvent e, Player viewer) => e switch
    {
        ResourcesFound or FlotsamFound when ((ObjectVisited)e).Hero.Owner == viewer => "pickup",
        ChestOpened or SeaChestOpened or AbandonedShipLooted or ArtifactFound when ((ObjectVisited)e).Hero.Owner == viewer => "chest",
        MineCaptured or TownCaptured when ((ObjectVisited)e).Hero.Owner == viewer => "capture",
        BuildingBuilt built when built.Town.Owner == viewer => "build",
        HeroLevelChoice choice when choice.Hero.Owner == viewer => "level-up",
        DayStarted { NewWeek: true } => "new-week",
        // (ceux des adversaires : quand leurs déplacements sont rejoués)
        MapSpellCast cast when cast.Hero.Owner == viewer => $"spell-{cast.Spell.Id}",
        BattleFought battle when battle.Hero.Owner == viewer => battle.Report.AttackerWon ? "victory" : "defeat",
        BattleFought battle when battle.DefenderOwner == viewer => battle.Report.AttackerWon ? "defeat" : "victory",
        GameEnded end => end.Outcome == GameOutcome.Victory ? "victory" : "defeat",
        _ => null,
    };

    /// <summary>Un des fichiers de ce nom, au hasard, prêt à jouer ; null s'il n'y en a pas.</summary>
    private AudioStream? Pick(Dictionary<string, string[]> table, string id) =>
        table.TryGetValue(id, out var files) && files.Length > 0 ? Load(files[_rng.Next(files.Length)]) : null;

    /// <summary>Un fichier de la liste, autre que <paramref name="current"/> quand elle en a plusieurs.</summary>
    private string? NextFile(string[] files, string? current)
    {
        var choices = files.Length > 1 ? files.Where(f => f != current).ToArray() : files;
        return choices.Length == 0 ? null : choices[_rng.Next(choices.Length)];
    }

    private AudioStream? Load(string file)
    {
        if (_cache.TryGetValue(file, out var cached))
            return cached;
        string path = Root + file;
        AudioStream? stream = null;
        if (!Godot.FileAccess.FileExists(path))
        {
            // le jeu exporté n'a que les sons importés par Godot, pas les fichiers eux-mêmes
            if (ResourceLoader.Exists(path))
                stream = GD.Load<AudioStream>(path);
            else
                GD.PrintErr($"Son absent : {path}");
        }
        else if (file.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            stream = AudioStreamOggVorbis.LoadFromFile(path);
        else if (file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            stream = AudioStreamWav.LoadFromFile(path);
        else if (file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            stream = AudioStreamMP3.LoadFromFile(path);
        else
            GD.PrintErr($"Son illisible (ni .ogg, ni .wav, ni .mp3) : {path}");
        _cache[file] = stream;
        return stream;
    }

    /// <summary>Le numéro de ce bus (créé s'il n'existe pas encore : tout est fait par le code, sans l'éditeur).</summary>
    private static int BusIndex(string name)
    {
        int index = AudioServer.GetBusIndex(name);
        if (index >= 0)
            return index;
        AudioServer.AddBus();
        index = AudioServer.BusCount - 1;
        AudioServer.SetBusName(index, name);
        AudioServer.SetBusSend(index, "Master");
        return index;
    }

    /// <summary>Donne aux bus le volume des réglages (de 0 à 1 ; au carré, pour que le curseur suive l'oreille).</summary>
    public static void ApplyVolumes()
    {
        foreach (var (bus, volume) in new[]
                 {
                     (MusicBus, Settings.MusicVolume), (EffectsBus, Settings.EffectsVolume), (AmbienceBus, Settings.AmbienceVolume),
                 })
        {
            int index = BusIndex(bus);
            AudioServer.SetBusMute(index, volume <= 0.001);
            AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb((float)(volume * volume)));
        }
        // aucun son pendant une capture, ni avec « --mute » (2026-11-17, demandé par l'utilisateur : les captures à la
        // suite relançaient la musique à chaque fois)
        AudioServer.SetBusMute(0, Muted);
    }

    private static bool Muted => OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--capture=") || a == "--mute");
}
