namespace HoMM.Core;

/// <summary>
/// Les passages entre les zones (une option de la nouvelle partie, choix de l'utilisateur, 2026-11-14) : la force des
/// gardiens qui les tiennent, pour ne pas conquérir trop tôt la zone voisine.
/// </summary>
public enum ZoneAccess
{
    /// <summary>Ouverts : l'armée de départ bat le gardien de la sortie de sa zone (comme avant l'option).</summary>
    Open,

    /// <summary>Normaux : il faut environ une semaine d'armée (deux fois l'armée de départ) pour sortir de sa zone.</summary>
    Normal,

    /// <summary>Fermés : deux à trois semaines d'armée (quatre fois l'armée de départ) ; les autres passages sont deux fois plus gardés.</summary>
    Closed,
}
