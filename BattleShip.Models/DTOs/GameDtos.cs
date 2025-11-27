using BattleShip.Models.Enums;

namespace BattleShip.Models.DTOs;

public record CreateGameRequest(string PlayerId, GameDifficulty Difficulty = GameDifficulty.Medium, bool IsSinglePlayer = true);
public record JoinGameRequest(string GameId, string PlayerId);
public record PlaceShipsRequest(string GameId, string PlayerId, List<ShipPlacementDto> Ships);
public record ShipPlacementDto(string Name, int Size, int Row, int Col, Orientation Orientation);
public record ShootRequest(string GameId, string PlayerId, int Row, int Column);
public record LeaderboardEntry(string PlayerId, int Wins);
public record GameStateDto(
    string GameId, 
    string CurrentTurnPlayerId, 
    GameStatus Status, 
    string? WinnerId,
    List<ShotResultDto> MyShots,
    List<ShotResultDto> OpponentShots,
    List<MoveHistoryDto> History
);

public record ShotResultDto(int Row, int Column, CellState State);
public record MoveHistoryDto(string PlayerId, int Row, int Col, string Result, string? SunkShipName);
