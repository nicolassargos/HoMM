namespace HoMM.Core;

/// <summary>
/// Le type de la carte (une option de la nouvelle partie, choix de l'utilisateur, 2026-11-14, comme Heroes 3) : la part de
/// la mer, et s'il faut un bateau pour aller partout.
/// </summary>
public enum MapType
{
    /// <summary>Continent : la terre partout, des lacs (comme avant l'option).</summary>
    Continent,

    /// <summary>Quelques mers : des zones entières sont de la mer, mais tout s'atteint aussi à pied.</summary>
    Seas,

    /// <summary>Îles : chaque joueur sur son île (quelques zones), et des îles neutres ; on va de l'une à l'autre en bateau.</summary>
    Islands,

    /// <summary>Archipel : beaucoup de petites îles (une zone chacune), tout en bateau.</summary>
    Archipelago,
}
