using Grpc.Core;
using BattleShip.Grpc;
using BattleShip.API.Services;
using BattleShip.API.Helpers;
using Battleship.Core.Enums;

namespace BattleShip.API.GrpcServices;

/// <summary>
/// Implémentation du service gRPC BattleShip
/// </summary>
public class BattleShipGrpcService : BattleShipService.BattleShipServiceBase
{
    private readonly GameService _gameService;
    private readonly ILogger<BattleShipGrpcService> _logger;

    public BattleShipGrpcService(GameService gameService, ILogger<BattleShipGrpcService> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    /// <summary>
    /// Démarre une nouvelle partie
    /// </summary>
    public override Task<StartGameResponse> StartGame(StartGameRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC StartGame appelé avec difficulté: {Difficulty}", request.Difficulty);

        // Parser la difficulté
        if (!Enum.TryParse<AILevel>(request.Difficulty, out var level))
        {
            level = AILevel.Medium;
        }

        // Créer la partie
        var game = _gameService.CreateGame(level);

        // Construire la réponse
        var response = new StartGameResponse
        {
            GameId = game.Id.ToString(),
            GridSize = game.Settings.GridSize
        };

        // Convertir la grille en tableau de strings
        var grid = game.PlayerBoard.Grid;
        for (int r = 0; r < game.Settings.GridSize; r++)
        {
            var rowString = "";
            for (int c = 0; c < game.Settings.GridSize; c++)
            {
                rowString += grid[r, c];
            }
            response.PlayerGrid.Add(rowString);
        }

        // Ajouter les bateaux
        foreach (var ship in game.PlayerBoard.Ships)
        {
            response.Ships.Add(new ShipInfo
            {
                Letter = ship.Letter.ToString(),
                Size = ship.Size
            });
        }

        return Task.FromResult(response);
    }

    /// <summary>
    /// Effectue une attaque
    /// </summary>
    public override Task<AttackResponse> Attack(AttackRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC Attack appelé pour le jeu {GameId} à ({Row}, {Column})", 
            request.GameId, request.Row, request.Column);

        if (!Guid.TryParse(request.GameId, out var gameId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "GameId invalide"));
        }

        var game = _gameService.GetGame(gameId);
        if (game == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Partie introuvable"));
        }

        if (game.IsOver)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "La partie est terminée"));
        }

        // Valider les coordonnées
        if (request.Row < 0 || request.Row >= game.Settings.GridSize ||
            request.Column < 0 || request.Column >= game.Settings.GridSize)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, 
                $"Les coordonnées doivent être entre 0 et {game.Settings.GridSize - 1}"));
        }

        // Attaque du joueur
        var playerResult = _gameService.PlayerAttack(gameId, request.Row, request.Column);

        char? hitLetter = null;
        if (playerResult != ShotResult.Miss)
        {
            hitLetter = game.ComputerBoard.Grid[request.Row, request.Column];
        }

        // Construire la réponse
        var response = new AttackResponse
        {
            PlayerAttackResult = playerResult.ToString(),
            HitShipLetter = hitLetter?.ToString() ?? "",
            GameState = game.State.ToString()
        };

        // Si la partie n'est pas terminée, l'IA attaque
        if (!game.IsOver)
        {
            var (aiRow, aiCol, aiResult) = _gameService.AIAttack(gameId);

            char? aiHitLetter = null;
            if (aiResult != ShotResult.Miss)
            {
                aiHitLetter = game.PlayerBoard.Grid[aiRow, aiCol];
            }

            response.AiAttack = new AttackInfo
            {
                Row = aiRow,
                Column = aiCol,
                Result = aiResult.ToString(),
                HitShipLetter = aiHitLetter?.ToString() ?? ""
            };

            response.GameState = game.State.ToString();
        }

        // Mettre à jour les grilles
        ConvertGridToStrings(game.PlayerBoard.GetFullView(), response.PlayerGrid);
        ConvertGridToStrings(game.ComputerBoard.GetPlayerView(), response.OpponentView);

        // Déterminer le gagnant
        if (game.IsOver)
        {
            if (game.ComputerBoard.AreAllShipsSunk())
            {
                response.Winner = "Player";
            }
            else if (game.PlayerBoard.AreAllShipsSunk())
            {
                response.Winner = "AI";
            }
        }

        return Task.FromResult(response);
    }

    /// <summary>
    /// Récupère l'état d'une partie
    /// </summary>
    public override Task<GetGameStateResponse> GetGameState(GetGameStateRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetGameState appelé pour le jeu {GameId}", request.GameId);

        if (!Guid.TryParse(request.GameId, out var gameId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "GameId invalide"));
        }

        var game = _gameService.GetGame(gameId);
        if (game == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Partie introuvable"));
        }

        var response = new GetGameStateResponse
        {
            GameId = game.Id.ToString(),
            GridSize = game.Settings.GridSize,
            GameState = game.State.ToString()
        };

        ConvertGridToStrings(game.PlayerBoard.GetFullView(), response.PlayerGrid);
        ConvertGridToStrings(game.ComputerBoard.GetPlayerView(), response.OpponentView);

        // Ajouter l'historique
        foreach (var move in game.MoveHistory)
        {
            response.MoveHistory.Add(new MoveInfo
            {
                MoveId = move.MoveId.ToString(),
                Timestamp = ((DateTimeOffset)move.Timestamp).ToUnixTimeSeconds(),
                Row = move.Row,
                Column = move.Column,
                IsPlayerMove = move.IsPlayerMove,
                IsHit = move.IsHit,
                HitShipLetter = move.HitShipLetter?.ToString() ?? ""
            });
        }

        return Task.FromResult(response);
    }

    /// <summary>
    /// Annule des coups
    /// </summary>
    public override Task<UndoMovesResponse> UndoMoves(UndoMovesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC UndoMoves appelé pour le jeu {GameId}, count: {Count}", 
            request.GameId, request.Count);

        if (!Guid.TryParse(request.GameId, out var gameId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "GameId invalide"));
        }

        var game = _gameService.GetGame(gameId);
        if (game == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Partie introuvable"));
        }

        if (request.Count <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, 
                "Le nombre de coups à annuler doit être supérieur à 0"));
        }

        // Annuler les coups
        int undoneCount = game.UndoMoves(request.Count);

        var response = new UndoMovesResponse
        {
            UndoneCount = undoneCount,
            GameState = game.State.ToString()
        };

        if (game.IsOver)
        {
            if (game.ComputerBoard.AreAllShipsSunk())
            {
                response.Winner = "Player";
            }
            else if (game.PlayerBoard.AreAllShipsSunk())
            {
                response.Winner = "AI";
            }
        }

        ConvertGridToStrings(game.PlayerBoard.GetFullView(), response.PlayerGrid);
        ConvertGridToStrings(game.ComputerBoard.GetPlayerView(), response.OpponentView);

        return Task.FromResult(response);
    }

    /// <summary>
    /// Récupère le leaderboard
    /// </summary>
    public override Task<GetLeaderboardResponse> GetLeaderboard(GetLeaderboardRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetLeaderboard appelé, count: {Count}", request.Count);

        var leaderboard = _gameService.GetLeaderboard(request.Count);
        var response = new GetLeaderboardResponse();

        foreach (var entry in leaderboard)
        {
            response.Entries.Add(new LeaderboardEntry
            {
                PlayerName = entry.PlayerName,
                Score = entry.Score,
                Difficulty = entry.Difficulty.ToString(),
                Date = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(entry.Date.ToUniversalTime()),
                Duration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(entry.Duration),
                Accuracy = entry.Accuracy,
                Victory = entry.Victory,
                PlayerHits = entry.PlayerHits,
                TotalShots = entry.TotalShots
            });
        }

        return Task.FromResult(response);
    }

    /// <summary>
    /// Termine une partie et l'ajoute au leaderboard
    /// </summary>
    public override Task<EndGameResponse> EndGame(EndGameRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC EndGame appelé pour le jeu {GameId}, joueur: {PlayerName}", 
            request.GameId, request.PlayerName);

        if (!Guid.TryParse(request.GameId, out var gameId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "GameId invalide"));
        }

        try
        {
            _gameService.EndGame(gameId, request.PlayerName);
            return Task.FromResult(new EndGameResponse 
            { 
                Success = true,
                Message = "Partie terminée et ajoutée au leaderboard"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la fin de la partie {GameId}", gameId);
            return Task.FromResult(new EndGameResponse 
            { 
                Success = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Convertit une grille 2D en liste de strings
    /// </summary>
    private void ConvertGridToStrings(char[,] grid, Google.Protobuf.Collections.RepeatedField<string> target)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            var rowString = "";
            for (int c = 0; c < cols; c++)
            {
                rowString += grid[r, c];
            }
            target.Add(rowString);
        }
    }
}
