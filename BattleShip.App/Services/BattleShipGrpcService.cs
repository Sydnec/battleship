using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using BattleShip.Grpc;

namespace BattleShip.App.Services;

/// <summary>
/// Implémentation gRPC du service de bataille navale (compatible Blazor WASM)
/// </summary>
public class BattleShipGrpcService : IBattleShipService, IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly BattleShipService.BattleShipServiceClient _client;
    private readonly GameState _gameState;

    public BattleShipGrpcService(GameState gameState)
    {
        _gameState = gameState;
        
        // Créer un HttpClient avec gRPC-Web pour Blazor WASM
        var httpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));
        
        // Créer un canal gRPC vers le port 5224 (HTTP/1.1 + gRPC-Web)
        _channel = GrpcChannel.ForAddress("http://localhost:5224", new GrpcChannelOptions
        {
            HttpClient = httpClient
        });
        
        _client = new BattleShipService.BattleShipServiceClient(_channel);
    }

    public async Task<bool> StartNewGameAsync(string difficulty = "Medium")
    {
        try
        {
            var request = new StartGameRequest { Difficulty = difficulty };
            var response = await _client.StartGameAsync(request);

            // Mettre à jour l'état du jeu
            _gameState.GameId = Guid.Parse(response.GameId);
            _gameState.GridSize = response.GridSize;
            _gameState.Ships = response.Ships.Select(s => new ShipInfo
            {
                Letter = s.Letter[0], // Prendre le premier caractère
                Size = s.Size
            }).ToList();
            _gameState.GameStatus = "Playing";
            _gameState.Winner = null;
            _gameState.MovesCount = 0;
            _gameState.MoveHistory.Clear();

            // Convertir le repeated string en tableau 2D
            _gameState.PlayerGrid = ConvertRepeatedGridTo2D(response.PlayerGrid, response.GridSize);
            _gameState.OpponentGrid = new bool?[response.GridSize, response.GridSize];

            _gameState.NotifyStateChanged();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur gRPC lors du démarrage de la partie : {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AttackAsync(int row, int col)
    {
        if (_gameState.GameId == null)
            return false;

        try
        {
            var request = new BattleShip.Grpc.AttackRequest
            {
                GameId = _gameState.GameId.Value.ToString(),
                Row = row,
                Column = col
            };

            var response = await _client.AttackAsync(request);

            // Mettre à jour la grille adverse avec le résultat du tir
            bool isHit = response.PlayerAttackResult == "Hit" || response.PlayerAttackResult == "Sunk";
            _gameState.OpponentGrid[row, col] = isHit;

            // Mettre à jour la grille du joueur
            _gameState.PlayerGrid = ConvertRepeatedGridTo2D(response.PlayerGrid, _gameState.GridSize);

            // Mettre à jour l'état de la partie
            _gameState.GameStatus = response.GameState;
            _gameState.Winner = string.IsNullOrEmpty(response.Winner) ? null : response.Winner;
            _gameState.MovesCount++;

            // Mettre à jour l'historique (ajouter le coup du joueur)
            char? hitLetter = string.IsNullOrEmpty(response.HitShipLetter) ? null : response.HitShipLetter[0];
            _gameState.MoveHistory.Add(new MoveInfo
            {
                MoveId = Guid.NewGuid(),
                Timestamp = DateTime.Now,
                Row = row,
                Column = col,
                IsPlayerMove = true,
                IsHit = isHit,
                HitShipLetter = hitLetter
            });

            // Ajouter le coup de l'IA si elle a joué
            if (response.AiAttack != null)
            {
                char? aiHitLetter = string.IsNullOrEmpty(response.AiAttack.HitShipLetter) ? null : response.AiAttack.HitShipLetter[0];
                _gameState.MoveHistory.Add(new MoveInfo
                {
                    MoveId = Guid.NewGuid(),
                    Timestamp = DateTime.Now,
                    Row = response.AiAttack.Row,
                    Column = response.AiAttack.Column,
                    IsPlayerMove = false,
                    IsHit = response.AiAttack.Result != "Miss",
                    HitShipLetter = aiHitLetter
                });
            }

            _gameState.NotifyStateChanged();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur gRPC lors de l'attaque : {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UndoMovesAsync(int count)
    {
        if (_gameState.GameId == null)
            return false;

        try
        {
            var request = new UndoMovesRequest
            {
                GameId = _gameState.GameId.Value.ToString(),
                Count = count
            };

            var response = await _client.UndoMovesAsync(request);

            // Mettre à jour les grilles
            _gameState.PlayerGrid = ConvertRepeatedGridTo2D(response.PlayerGrid, _gameState.GridSize);
            _gameState.OpponentGrid = ConvertOpponentGridFromRepeated(response.OpponentView, _gameState.GridSize);

            _gameState.GameStatus = response.GameState;
            _gameState.Winner = string.IsNullOrEmpty(response.Winner) ? null : response.Winner;
            _gameState.MovesCount -= response.UndoneCount;

            // Supprimer les coups annulés de l'historique
            if (_gameState.MoveHistory.Count >= response.UndoneCount)
            {
                _gameState.MoveHistory.RemoveRange(
                    _gameState.MoveHistory.Count - response.UndoneCount,
                    response.UndoneCount
                );
            }

            _gameState.NotifyStateChanged();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur gRPC lors de l'annulation : {ex.Message}");
            return false;
        }
    }

    public async Task<List<LeaderboardEntryDto>?> GetLeaderboardAsync(int count = 10)
    {
        try
        {
            var request = new GetLeaderboardRequest { Count = count };
            var response = await _client.GetLeaderboardAsync(request);
            
            return response.Entries.Select(e => new LeaderboardEntryDto
            {
                PlayerName = e.PlayerName,
                Score = e.Score,
                Difficulty = e.Difficulty,
                Date = e.Date.ToDateTime(),
                Duration = e.Duration.ToTimeSpan(),
                Accuracy = e.Accuracy,
                Victory = e.Victory,
                PlayerHits = e.PlayerHits,
                TotalShots = e.TotalShots
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur gRPC lors de la récupération du leaderboard : {ex.Message}");
            return null;
        }
    }

    public async Task<bool> EndGameAsync(string playerName = "Joueur")
    {
        if (!_gameState.GameId.HasValue)
            return false;

        try
        {
            var request = new EndGameRequest 
            { 
                GameId = _gameState.GameId.ToString()!,
                PlayerName = playerName 
            };
            var response = await _client.EndGameAsync(request);
            return response.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur gRPC lors de la fin de la partie : {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Convertit un repeated string (gRPC) en tableau 2D
    /// Chaque string représente une ligne de la grille
    /// </summary>
    private char[,] ConvertRepeatedGridTo2D(Google.Protobuf.Collections.RepeatedField<string> grid, int size)
    {
        var result = new char[size, size];
        
        for (int r = 0; r < size && r < grid.Count; r++)
        {
            var row = grid[r];
            for (int c = 0; c < size && c < row.Length; c++)
            {
                result[r, c] = row[c];
            }
        }

        return result;
    }

    /// <summary>
    /// Convertit la vue adverse (repeated string) en grille bool?
    /// </summary>
    private bool?[,] ConvertOpponentGridFromRepeated(Google.Protobuf.Collections.RepeatedField<string> opponentView, int size)
    {
        var result = new bool?[size, size];

        for (int r = 0; r < size && r < opponentView.Count; r++)
        {
            var row = opponentView[r];
            for (int c = 0; c < size && c < row.Length; c++)
            {
                char cell = row[c];
                if (cell == 'X')
                    result[r, c] = true;  // Touché
                else if (cell == 'O')
                    result[r, c] = false; // Raté
                else
                    result[r, c] = null;  // Non exploré
            }
        }

        return result;
    }

    public void Dispose()
    {
        _channel?.Dispose();
    }
}
