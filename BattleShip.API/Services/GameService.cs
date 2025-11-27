using System.Collections.Concurrent;
using BattleShip.API.Hubs;
using BattleShip.Models.DTOs;
using BattleShip.Models.Entities;
using BattleShip.Models.Enums;
using BattleShip.Models.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BattleShip.API.Services;

public class GameService : IGameService
{
    // In-memory storage for simplicity
    private static readonly ConcurrentDictionary<string, Game> _games = new();
    private const string AI_PLAYER_ID = "AI";
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILeaderboardService _leaderboardService;

    public GameService(IHubContext<GameHub> hubContext, ILeaderboardService leaderboardService)
    {
        _hubContext = hubContext;
        _leaderboardService = leaderboardService;
    }

    public Task<Game> CreateGameAsync(string playerId, bool isSinglePlayer = true, GameDifficulty difficulty = GameDifficulty.Medium)
    {
        var game = new Game
        {
            Player1Id = playerId,
            CurrentTurnPlayerId = playerId, // Player 1 starts
            IsSinglePlayer = isSinglePlayer,
            Difficulty = difficulty
        };
        
        int boardSize = difficulty switch
        {
            GameDifficulty.Easy => 8,
            GameDifficulty.Medium => 10,
            GameDifficulty.Hard => 12,
            _ => 10
        };

        game.Player1Board.Size = boardSize;
        game.Player2Board.Size = boardSize;

        if (isSinglePlayer)
        {
            game.Player2Id = AI_PLAYER_ID;
            game.Status = GameStatus.PlacingShips;
            PlaceRandomShips(game.Player2Board);
        }
        
        _games.TryAdd(game.Id, game);
        return Task.FromResult(game);
    }

    private async Task NotifyGameUpdate(string gameId)
    {
        await _hubContext.Clients.Group(gameId).SendAsync("GameStateUpdated", gameId);
    }

    public async Task<Game?> JoinGameAsync(string gameId, string playerId)
    {
        if (_games.TryGetValue(gameId, out var game))
        {
            if (game.Player2Id == null && game.Player1Id != playerId && !game.IsSinglePlayer)
            {
                game.Player2Id = playerId;
                game.Status = GameStatus.PlacingShips; // Start placing ships when P2 joins
                await NotifyGameUpdate(gameId);
                return game;
            }
        }
        return null;
    }

    public async Task<Game?> PlaceShipsAsync(string gameId, string playerId, List<ShipPlacementDto> ships)
    {
        if (!_games.TryGetValue(gameId, out var game)) return null;

        var board = playerId == game.Player1Id ? game.Player1Board : 
                    playerId == game.Player2Id ? game.Player2Board : null;

        if (board == null) return null;

        board.Ships.Clear();

        foreach (var s in ships)
        {
            var ship = new Ship
            {
                Name = s.Name,
                Size = s.Size,
                Orientation = s.Orientation,
                StartPosition = new Coordinate(s.Row, s.Col),
                OccupiedCoordinates = new List<Coordinate>()
            };

            for (int i = 0; i < s.Size; i++)
            {
                if (s.Orientation == Orientation.Horizontal)
                    ship.OccupiedCoordinates.Add(new Coordinate(s.Row, s.Col + i));
                else
                    ship.OccupiedCoordinates.Add(new Coordinate(s.Row + i, s.Col));
            }
            
            if (ship.OccupiedCoordinates.Any(c => !IsValidCoordinate(c, board))) continue;
            
            // Check overlap
            if (board.Ships.Any(existing => existing.OccupiedCoordinates.Any(c => ship.OccupiedCoordinates.Contains(c)))) continue;

            board.AddShip(ship);
        }

        if (game.Player1Board.Ships.Any() && game.Player2Board.Ships.Any())
        {
            game.Status = GameStatus.InProgress;
        }

        await NotifyGameUpdate(gameId);
        return game;
    }

    public async Task<GameStateDto?> ShootAsync(string gameId, string playerId, int row, int col)
    {
        if (!_games.TryGetValue(gameId, out var game)) return null;

        if (game.Status != GameStatus.InProgress) return null;
        if (game.CurrentTurnPlayerId != playerId) return null;

        var opponentBoard = playerId == game.Player1Id ? game.Player2Board : game.Player1Board;
        var coordinate = new Coordinate(row, col);

        // Player Shot
        var result = opponentBoard.ReceiveShot(coordinate);
        string? sunkShipName = null;
        if (result == CellState.Sunk)
        {
             var ship = opponentBoard.Ships.FirstOrDefault(s => s.OccupiedCoordinates.Contains(coordinate));
             sunkShipName = ship?.Name;
        }
        game.MoveHistory.Add(new Move { PlayerId = playerId, Row = row, Col = col, Result = result, SunkShipName = sunkShipName });

        // Check win condition for Player
        if (opponentBoard.Ships.All(s => s.IsSunk))
        {
            game.Status = GameStatus.Finished;
            game.WinnerId = playerId;
            await _leaderboardService.AddWinAsync(playerId);
        }
        else
        {
            // Switch turn
            game.CurrentTurnPlayerId = playerId == game.Player1Id ? game.Player2Id! : game.Player1Id;

            // AI Turn if Single Player
            if (game.IsSinglePlayer && game.CurrentTurnPlayerId == AI_PLAYER_ID)
            {
                PerformAiShot(game);
            }
        }

        await NotifyGameUpdate(gameId);
        return MapToDto(game, playerId);
    }

    private void PerformAiShot(Game game)
    {
        var playerBoard = game.Player1Board;
        var random = new Random();
        Coordinate target;

        if (game.Difficulty == GameDifficulty.Easy)
        {
            // Easy: Pure Random
            int row, col;
            do
            {
                row = random.Next(0, playerBoard.Size);
                col = random.Next(0, playerBoard.Size);
                target = new Coordinate(row, col);
            } while (playerBoard.ShotsFired.ContainsKey(target));
        }
        else if (game.Difficulty == GameDifficulty.Medium)
        {
            // Medium: Hunt/Target (Standard)
            var potentialTargets = GetPotentialTargets(playerBoard);
            if (potentialTargets.Any())
            {
                target = potentialTargets[random.Next(potentialTargets.Count)];
            }
            else
            {
                int row, col;
                do
                {
                    row = random.Next(0, playerBoard.Size);
                    col = random.Next(0, playerBoard.Size);
                    target = new Coordinate(row, col);
                } while (playerBoard.ShotsFired.ContainsKey(target));
            }
        }
        else // Hard
        {
            // Hard: Hunt/Target + Parity (Checkerboard) + Probability (Cheat a little? No, let's just be smart)
            // For now, let's use Hunt/Target but prioritize targets better
            var potentialTargets = GetPotentialTargets(playerBoard);
            if (potentialTargets.Any())
            {
                target = potentialTargets[random.Next(potentialTargets.Count)];
            }
            else
            {
                // Checkerboard strategy for hunting
                int row, col;
                int attempts = 0;
                do
                {
                    row = random.Next(0, playerBoard.Size);
                    col = random.Next(0, playerBoard.Size);
                    target = new Coordinate(row, col);
                    attempts++;
                } while ((playerBoard.ShotsFired.ContainsKey(target) || (row + col) % 2 != 0) && attempts < 100);
                
                if (attempts >= 100) // Fallback if checkerboard is full
                {
                     do
                    {
                        row = random.Next(0, playerBoard.Size);
                        col = random.Next(0, playerBoard.Size);
                        target = new Coordinate(row, col);
                    } while (playerBoard.ShotsFired.ContainsKey(target));
                }
            }
        }

        var result = playerBoard.ReceiveShot(target);
        string? sunkShipName = null;
        if (result == CellState.Sunk)
        {
             var ship = playerBoard.Ships.FirstOrDefault(s => s.OccupiedCoordinates.Contains(target));
             sunkShipName = ship?.Name;
        }
        game.MoveHistory.Add(new Move { PlayerId = AI_PLAYER_ID, Row = target.Row, Col = target.Column, Result = result, SunkShipName = sunkShipName });

        if (playerBoard.Ships.All(s => s.IsSunk))
        {
            game.Status = GameStatus.Finished;
            game.WinnerId = AI_PLAYER_ID;
        }
        else
        {
            game.CurrentTurnPlayerId = game.Player1Id;
        }
    }

    private List<Coordinate> GetPotentialTargets(Board board)
    {
        var targets = new List<Coordinate>();
        var hits = board.ShotsFired.Where(s => s.Value == CellState.Hit).Select(s => s.Key).ToList();

        foreach (var hit in hits)
        {
            // Check neighbors (Up, Down, Left, Right)
            var neighbors = new[]
            {
                new Coordinate(hit.Row - 1, hit.Column),
                new Coordinate(hit.Row + 1, hit.Column),
                new Coordinate(hit.Row, hit.Column - 1),
                new Coordinate(hit.Row, hit.Column + 1)
            };

            foreach (var n in neighbors)
            {
                if (IsValidCoordinate(n, board) && !board.ShotsFired.ContainsKey(n))
                {
                    targets.Add(n);
                }
            }
        }
        return targets;
    }

    private bool IsValidCoordinate(Coordinate c, Board board)
    {
        return c.Row >= 0 && c.Row < board.Size && c.Column >= 0 && c.Column < board.Size;
    }

    public async Task<Game?> UndoLastMoveAsync(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return null;
        if (game.Status != GameStatus.InProgress && game.Status != GameStatus.Finished) return null;
        if (!game.MoveHistory.Any()) return null;

        int movesToUndo = game.IsSinglePlayer ? 2 : 1;
        
        for (int i = 0; i < movesToUndo; i++)
        {
            if (!game.MoveHistory.Any()) break;
            
            var lastMove = game.MoveHistory.Last();
            game.MoveHistory.RemoveAt(game.MoveHistory.Count - 1);
            
            var board = lastMove.PlayerId == game.Player1Id ? game.Player2Board : game.Player1Board;
            var coord = new Coordinate(lastMove.Row, lastMove.Col);
            
            if (board.ShotsFired.ContainsKey(coord))
            {
                board.ShotsFired.Remove(coord);
                if (lastMove.Result == CellState.Hit || lastMove.Result == CellState.Sunk)
                {
                    var ship = board.Ships.FirstOrDefault(s => s.OccupiedCoordinates.Contains(coord));
                    if (ship != null)
                    {
                        ship.Hits--;
                    }
                }
            }
            
            game.CurrentTurnPlayerId = lastMove.PlayerId;
            
            if (game.Status == GameStatus.Finished)
            {
                game.Status = GameStatus.InProgress;
                game.WinnerId = null;
            }
        }
        
        await NotifyGameUpdate(gameId);
        return game;
    }

    public Task<Game?> GetGameAsync(string gameId)
    {
        _games.TryGetValue(gameId, out var game);
        return Task.FromResult(game);
    }

    public async Task<Game?> RestartGameAsync(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return null;
        
        // Basic validation
        if (game.Player1Id != playerId && game.Player2Id != playerId) return null;

        // Reset Game
        game.Status = GameStatus.PlacingShips;
        game.WinnerId = null;
        game.CurrentTurnPlayerId = game.Player1Id;
        
        // Reset Boards
        var size = game.Player1Board.Size;
        game.Player1Board = new Board { Size = size };
        game.Player2Board = new Board { Size = size };

        // Re-place ships
        if (game.IsSinglePlayer)
        {
            PlaceRandomShips(game.Player2Board);
        }

        await NotifyGameUpdate(gameId);
        return game;
    }

    public Task<List<LeaderboardEntry>> GetLeaderboardAsync()
    {
        return _leaderboardService.GetLeaderboardAsync();
    }

    private GameStateDto MapToDto(Game game, string playerId)
    {
        var isPlayer1 = playerId == game.Player1Id;
        var myBoard = isPlayer1 ? game.Player1Board : game.Player2Board;
        var opponentBoard = isPlayer1 ? game.Player2Board : game.Player1Board;

        var myShots = opponentBoard.ShotsFired.Select(kvp => new ShotResultDto(kvp.Key.Row, kvp.Key.Column, kvp.Value)).ToList();
        var opponentShots = myBoard.ShotsFired.Select(kvp => new ShotResultDto(kvp.Key.Row, kvp.Key.Column, kvp.Value)).ToList();

        var history = game.MoveHistory.Select(m => 
            new MoveHistoryDto(m.PlayerId, m.Row, m.Col, m.Result.ToString(), m.SunkShipName)
        ).ToList();

        return new GameStateDto(
            game.Id,
            game.CurrentTurnPlayerId,
            game.Status,
            game.WinnerId,
            myShots,
            opponentShots,
            history
        );
    }

    private void PlaceRandomShips(Board board)
    {
        var random = new Random();
        var shipsToPlace = new[]
        {
            new { Name = "Carrier", Size = 5 },
            new { Name = "Battleship", Size = 4 },
            new { Name = "Cruiser", Size = 3 },
            new { Name = "Submarine", Size = 3 },
            new { Name = "Destroyer", Size = 2 }
        };

        foreach (var shipInfo in shipsToPlace)
        {
            bool placed = false;
            int attempts = 0;
            while (!placed && attempts < 100) // Safety break
            {
                attempts++;
                var orientation = (Orientation)random.Next(2);
                var row = random.Next(board.Size);
                var col = random.Next(board.Size);
                
                // Check bounds
                if (orientation == Orientation.Horizontal && col + shipInfo.Size > board.Size) continue;
                if (orientation == Orientation.Vertical && row + shipInfo.Size > board.Size) continue;

                var newShip = new Ship
                {
                    Name = shipInfo.Name,
                    Size = shipInfo.Size,
                    Orientation = orientation,
                    StartPosition = new Coordinate(row, col),
                    OccupiedCoordinates = new List<Coordinate>()
                };

                // Generate coordinates
                for (int i = 0; i < shipInfo.Size; i++)
                {
                    if (orientation == Orientation.Horizontal)
                        newShip.OccupiedCoordinates.Add(new Coordinate(row, col + i));
                    else
                        newShip.OccupiedCoordinates.Add(new Coordinate(row + i, col));
                }

                // Check overlap
                if (board.Ships.Any(s => s.OccupiedCoordinates.Any(c => newShip.OccupiedCoordinates.Contains(c))))
                {
                    continue;
                }

                board.AddShip(newShip);
                placed = true;
            }
        }
    }
}
