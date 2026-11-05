namespace HoMM.Core;

/// <summary>
/// L'intelligence des adversaires (étape 8) : elle joue avec les mêmes méthodes de <see cref="GameState"/> que le joueur
/// humain, sans attendre l'affichage, puis l'appelant finit le tour. Point 1 : l'adversaire passe son tour ; il explorera
/// et ramassera (point 2), construira et recrutera (point 3), combattra (point 4).
/// </summary>
public static class ComputerPlayer
{
    /// <summary>Joue le tour du joueur courant, s'il est joué par l'ordinateur (sans finir le tour).</summary>
    public static void PlayTurn(GameState state)
    {
        if (!state.CurrentPlayer.IsComputer || state.Outcome != GameOutcome.Playing)
            return;
        // pour l'instant, rien : l'adversaire attend dans son coin
    }
}
