using System.Net.Http.Json;

namespace BattleShip.App.Services;

/// <summary>
/// Implémentation REST du service de bataille navale
/// </summary>
public class BattleShipRestService : IBattleShipService
{
    private readonly HttpClient _httpClient;
    private readonly GameState _gameState;

    public BattleShipRestService(HttpClient httpClient, GameState gameState)
    {
        _httpClient = httpClient;
        _gameState = gameState;
    }

    public async Task<bool> StartNewGameAsync(string difficulty = "Medium")
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/game/start?level={difficulty}", null);
            response.EnsureSuccessStatusCode();

            var gameResponse = await response.Content.ReadFromJsonAsync<GameResponse>();
            
            if (gameResponse == null)
                return false;

            // Mettre à jour l'état du jeu
            _gameState.GameId = gameResponse.GameId;
            _gameState.GridSize = gameResponse.GridSize;
            _gameState.Ships = gameResponse.Ships;
            _gameState.GameStatus = "Playing";
            _gameState.Winner = null;
            _gameState.MovesCount = 0;
            _gameState.MoveHistory.Clear();

            // Convertir le jagged array en tableau 2D
            _gameState.PlayerGrid = ConvertJaggedTo2D(gameResponse.PlayerGrid);
            _gameState.OpponentGrid = new bool?[gameResponse.GridSize, gameResponse.GridSize];

            _gameState.NotifyStateChanged();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur REST lors du démarrage de la partie : {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AttackAsync(int row, int col)
    {
        if (_gameState.GameId == null)
            return false;

        try
        {
            var request = new AttackRequest
            {
                GameId = _gameState.GameId.Value,
                Row = row,
                Column = col
            };

            var response = await _httpClient.PostAsJsonAsync("/api/game/attack", request);
            response.EnsureSuccessStatusCode();

            var attackResponse = await response.Content.ReadFromJsonAsync<AttackResponse>();
            
            if (attackResponse == null)
                return false;

            // Mettre à jour la grille adverse avec le résultat du tir
            bool isHit = attackResponse.PlayerAttackResult == "Hit" || attackResponse.PlayerAttackResult == "Sunk";
            _gameState.OpponentGrid[row, col] = isHit;

            // Mettre à jour la grille du joueur
            _gameState.PlayerGrid = ConvertJaggedTo2D(attackResponse.PlayerGrid);

            // Mettre à jour l'état de la partie
            _gameState.GameStatus = attackResponse.GameState;
            _gameState.Winner = attackResponse.Winner;
            _gameState.MovesCount++;

            // Mettre à jour l'historique (ajouter le coup du joueur)
            _gameState.MoveHistory.Add(new MoveInfo
            {
                MoveId = Guid.NewGuid(),
                Timestamp = DateTime.Now,
                Row = row,
                Column = col,
                IsPlayerMove = true,
                IsHit = isHit,
                HitShipLetter = attackResponse.HitShipLetter
            });

            // Ajouter le coup de l'IA si elle a joué
            if (attackResponse.AIAttack != null)
            {
                _gameState.MoveHistory.Add(new MoveInfo
                {
                    MoveId = Guid.NewGuid(),
                    Timestamp = DateTime.Now,
                    Row = attackResponse.AIAttack.Row,
                    Column = attackResponse.AIAttack.Column,
                    IsPlayerMove = false,
                    IsHit = attackResponse.AIAttack.Result != "Miss",
                    HitShipLetter = attackResponse.AIAttack.HitShipLetter
                });
            }

            _gameState.NotifyStateChanged();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur REST lors de l'attaque : {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UndoMovesAsync(int count)
    {
        if (_gameState.GameId == null)
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/game/{_gameState.GameId}/undo/{count}", null);
            response.EnsureSuccessStatusCode();

            var undoResponse = await response.Content.ReadFromJsonAsync<UndoResponse>();
            
            if (undoResponse == null)
                return false;

            // Mettre à jour les grilles
            _gameState.PlayerGrid = ConvertJaggedTo2D(undoResponse.PlayerGrid);
            _gameState.OpponentGrid = ConvertOpponentGrid(undoResponse.OpponentView);

            _gameState.GameStatus = undoResponse.GameState;
            _gameState.Winner = undoResponse.Winner;
            _gameState.MovesCount -= undoResponse.UndoneCount;

            // Supprimer les coups annulés de l'historique
            if (_gameState.MoveHistory.Count >= undoResponse.UndoneCount)
            {
                _gameState.MoveHistory.RemoveRange(
                    _gameState.MoveHistory.Count - undoResponse.UndoneCount,
                    undoResponse.UndoneCount
                );
            }

            _gameState.NotifyStateChanged();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur REST lors de l'annulation : {ex.Message}");
            return false;
        }
    }

    public async Task<List<LeaderboardEntryDto>?> GetLeaderboardAsync(int count = 10)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<LeaderboardEntryDto>>($"/api/game/leaderboard?count={count}");
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur REST lors de la récupération du leaderboard : {ex.Message}");
            return null;
        }
    }

    public async Task<bool> EndGameAsync(string playerName = "Joueur")
    {
        if (!_gameState.GameId.HasValue)
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/game/{_gameState.GameId}/end?playerName={playerName}", null);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur REST lors de la fin de la partie : {ex.Message}");
            return false;
        }
    }

    private bool?[,] ConvertOpponentGrid(char[][] opponentView)
    {
        if (opponentView == null || opponentView.Length == 0)
            return new bool?[0, 0];

        int rows = opponentView.Length;
        int cols = opponentView[0].Length;
        var result = new bool?[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                char cell = opponentView[r][c];
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

    private char[,] ConvertJaggedTo2D(char[][] jaggedArray)
    {
        if (jaggedArray == null || jaggedArray.Length == 0)
            return new char[0, 0];

        int rows = jaggedArray.Length;
        int cols = jaggedArray[0].Length;
        var result = new char[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                result[r, c] = jaggedArray[r][c];
            }
        }

        return result;
    }
}

// ===== DTOs REST =====

public class GameResponse
{
    public Guid GameId { get; set; }
    public int GridSize { get; set; }
    public char[][] PlayerGrid { get; set; } = Array.Empty<char[]>();
    public List<ShipInfo> Ships { get; set; } = new();
}

public class AttackRequest
{
    public Guid GameId { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
}

public class AttackResponse
{
    public string PlayerAttackResult { get; set; } = string.Empty;
    public char? HitShipLetter { get; set; }
    public AttackInfo? AIAttack { get; set; }
    public string GameState { get; set; } = "Playing";
    public string? Winner { get; set; }
    public char[][] PlayerGrid { get; set; } = Array.Empty<char[]>();
}

public class AttackInfo
{
    public int Row { get; set; }
    public int Column { get; set; }
    public string Result { get; set; } = string.Empty;
    public char? HitShipLetter { get; set; }
}

public class UndoResponse
{
    public int UndoneCount { get; set; }
    public string GameState { get; set; } = "Playing";
    public string? Winner { get; set; }
    public char[][] PlayerGrid { get; set; } = Array.Empty<char[]>();
    public char[][] OpponentView { get; set; } = Array.Empty<char[]>();
}

public class LeaderboardEntryDto
{
    public string PlayerName { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeSpan Duration { get; set; }
    public double Accuracy { get; set; }
    public bool Victory { get; set; }
    public int PlayerHits { get; set; }
    public int TotalShots { get; set; }
}
