using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Grpc = BattleShip.Grpc;

namespace BattleShip.App.Services;

/// <summary>
/// Client gRPC pour communiquer avec l'API Bataille Navale
/// </summary>
public class BattleShipGrpcClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly Grpc.BattleShipService.BattleShipServiceClient _client;

    public BattleShipGrpcClient(string serverAddress)
    {
        // Pour Blazor WASM, on doit utiliser gRPC-Web
        var httpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));
        
        // Créer un canal gRPC
        _channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions
        {
            HttpClient = httpClient
        });
        
        _client = new Grpc.BattleShipService.BattleShipServiceClient(_channel);
    }

    /// <summary>
    /// Démarre une nouvelle partie
    /// </summary>
    public async Task<Grpc.StartGameResponse> StartGameAsync(string difficulty)
    {
        var request = new Grpc.StartGameRequest 
        { 
            Difficulty = difficulty 
        };
        
        return await _client.StartGameAsync(request);
    }

    /// <summary>
    /// Effectue une attaque
    /// </summary>
    public async Task<Grpc.AttackResponse> AttackAsync(string gameId, int row, int column)
    {
        var request = new Grpc.AttackRequest
        {
            GameId = gameId,
            Row = row,
            Column = column
        };

        return await _client.AttackAsync(request);
    }

    /// <summary>
    /// Récupère l'état d'une partie
    /// </summary>
    public async Task<Grpc.GetGameStateResponse> GetGameStateAsync(string gameId)
    {
        var request = new Grpc.GetGameStateRequest
        {
            GameId = gameId
        };

        return await _client.GetGameStateAsync(request);
    }

    /// <summary>
    /// Annule les N derniers coups
    /// </summary>
    public async Task<Grpc.UndoMovesResponse> UndoMovesAsync(string gameId, int count)
    {
        var request = new Grpc.UndoMovesRequest
        {
            GameId = gameId,
            Count = count
        };

        return await _client.UndoMovesAsync(request);
    }

    public void Dispose()
    {
        _channel?.Dispose();
    }
}
