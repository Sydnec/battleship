using System.Net.Http.Json;
using BattleShip.Models.DTOs;
using BattleShip.Models.Entities;
using BattleShip.Models.Enums;
using BattleShip.Models.Interfaces;

namespace BattleShip.App.Services;

public class GameClient : IGameService
{
    private readonly HttpClient _httpClient;

    public GameClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Game> CreateGameAsync(string playerId, bool isSinglePlayer = true, GameDifficulty difficulty = GameDifficulty.Medium)
    {
        var response = await _httpClient.PostAsJsonAsync("/games", new CreateGameRequest(playerId, difficulty, isSinglePlayer));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Game>() ?? throw new InvalidOperationException("Failed to create game");
    }

    public async Task<Game?> JoinGameAsync(string gameId, string playerId)
    {
        var response = await _httpClient.PostAsJsonAsync($"/games/{gameId}/join", new JoinGameRequest(gameId, playerId));
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Game>();
    }

    public async Task<GameStateDto?> ShootAsync(string gameId, string playerId, int row, int col)
    {
        var response = await _httpClient.PostAsJsonAsync($"/games/{gameId}/shoot", new ShootRequest(gameId, playerId, row, col));
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<GameStateDto>();
    }

    public async Task<Game?> GetGameAsync(string gameId)
    {
        return await _httpClient.GetFromJsonAsync<Game>($"/games/{gameId}");
    }

    public async Task<Game?> RestartGameAsync(string gameId, string playerId)
    {
        var response = await _httpClient.PostAsJsonAsync($"/games/{gameId}/restart", new CreateGameRequest(playerId));
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Game>();
    }

    public async Task<Game?> PlaceShipsAsync(string gameId, string playerId, List<ShipPlacementDto> ships)
    {
        var response = await _httpClient.PostAsJsonAsync($"/games/{gameId}/place-ships", new PlaceShipsRequest(gameId, playerId, ships));
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Game>();
    }

    public async Task<Game?> UndoLastMoveAsync(string gameId)
    {
        var response = await _httpClient.PostAsync($"/games/{gameId}/undo", null);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Game>();
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<LeaderboardEntry>>("/leaderboard") ?? new List<LeaderboardEntry>();
    }
}
