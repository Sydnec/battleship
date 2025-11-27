namespace BattleShip.App.Services;

/// <summary>
/// Interface commune pour les services de communication avec l'API
/// Permet de basculer entre REST et gRPC
/// </summary>
public interface IBattleShipService
{
    Task<bool> StartNewGameAsync(string difficulty = "Medium");
    Task<bool> AttackAsync(int row, int col);
    Task<bool> UndoMovesAsync(int count);
    Task<List<LeaderboardEntryDto>?> GetLeaderboardAsync(int count = 10);
    Task<bool> EndGameAsync(string playerName = "Joueur");
}
