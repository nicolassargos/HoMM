namespace HoMM.Core;

/// <summary>
/// Ce qui s'est passé dans la partie. Les règles (<see cref="GameState"/>) ajoutent les événements
/// dans une file ; l'affichage les lit avec <see cref="GameState.TakeEvents"/> et les montre
/// à son rythme (animations, fenêtres, sons…). Les règles ne dépendent jamais de l'affichage.
///
/// Chaque événement est défini dans le fichier de ce qu'il concerne (HeroMoved dans Hero.cs,
/// BattleFought dans Battle.cs…). Ici, seulement les événements de la partie dans son ensemble.
/// </summary>
public abstract record GameEvent;

/// <summary>Un nouveau jour commence (<paramref name="NewWeek"/> : c'est aussi une nouvelle semaine).</summary>
public sealed record DayStarted(int Day, bool NewWeek) : GameEvent;

/// <summary>C'est au tour d'un autre joueur.</summary>
public sealed record TurnStarted(Player Player) : GameEvent;

/// <summary>La partie est finie, pour cette raison.</summary>
public sealed record GameEnded(GameOutcome Outcome, GameEndReason Reason) : GameEvent;

/// <summary>Les conditions de la partie ont été choisies (au premier jour).</summary>
/// <summary>
/// Les conditions de la partie sont choisies. <paramref name="Treasure"/> : dans quelle partie de la carte le trésor
/// est enfoui, quand le trouver fait gagner (null sinon).
/// </summary>
public sealed record RulesChosen(GameRules Rules, Compass? Treasure = null) : GameEvent;
