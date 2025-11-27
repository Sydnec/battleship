using BattleShip.Models.DTOs;

namespace BattleShip.Models.Interfaces;

public interface ILeaderboardService
{
    Task AddWinAsync(string playerId);
    Task<List<LeaderboardEntry>> GetLeaderboardAsync();
}
