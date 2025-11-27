using BattleShip.Models.DTOs;
using BattleShip.Models.Entities;
using BattleShip.Models.Enums;

namespace BattleShip.Models.Interfaces;

public interface IGameService
{
    Task<Game> CreateGameAsync(string playerId, bool isSinglePlayer = true, GameDifficulty difficulty = GameDifficulty.Medium);
    Task<Game?> JoinGameAsync(string gameId, string playerId);
    Task<Game?> PlaceShipsAsync(string gameId, string playerId, List<ShipPlacementDto> ships);
    Task<GameStateDto?> ShootAsync(string gameId, string playerId, int row, int col);
    Task<Game?> UndoLastMoveAsync(string gameId);
    Task<Game?> GetGameAsync(string gameId);
    Task<Game?> RestartGameAsync(string gameId, string playerId);
    Task<List<LeaderboardEntry>> GetLeaderboardAsync();
}
