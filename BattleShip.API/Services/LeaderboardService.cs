using System.Collections.Concurrent;
using BattleShip.Models.DTOs;
using BattleShip.Models.Interfaces;

namespace BattleShip.API.Services;

public class LeaderboardService : ILeaderboardService
{
    private static readonly ConcurrentDictionary<string, int> _wins = new();

    public Task AddWinAsync(string playerId)
    {
        _wins.AddOrUpdate(playerId, 1, (key, oldValue) => oldValue + 1);
        return Task.CompletedTask;
    }

    public Task<List<LeaderboardEntry>> GetLeaderboardAsync()
    {
        var leaderboard = _wins.Select(kvp => new LeaderboardEntry(kvp.Key, kvp.Value))
                               .OrderByDescending(e => e.Wins)
                               .Take(10)
                               .ToList();
        return Task.FromResult(leaderboard);
    }
}
