using BattleShip.Models.DTOs;
using BattleShip.Models.Entities;
using BattleShip.Models.Enums;
using BattleShip.Models.Interfaces;
using BattleShip.Protos;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;

namespace BattleShip.App.Services;

public class GrpcGameClient : IGameService
{
    private readonly GrpcGame.GrpcGameClient _client;

    public GrpcGameClient(string baseUri)
    {
        var channel = GrpcChannel.ForAddress(baseUri, new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        });
        _client = new GrpcGame.GrpcGameClient(channel);
    }

    public async Task<Game> CreateGameAsync(string playerId, bool isSinglePlayer = true, GameDifficulty difficulty = GameDifficulty.Medium)
    {
        var request = new CreateGameRequestProto { PlayerId = playerId, IsSinglePlayer = isSinglePlayer, Difficulty = (int)difficulty };
        var response = await _client.CreateGameAsync(request);
        return MapToGame(response);
    }

    public async Task<Game?> JoinGameAsync(string gameId, string playerId)
    {
        var request = new JoinGameRequestProto { GameId = gameId, PlayerId = playerId };
        try
        {
            var response = await _client.JoinGameAsync(request);
            return MapToGame(response);
        }
        catch
        {
            return null;
        }
    }

    public async Task<GameStateDto?> ShootAsync(string gameId, string playerId, int row, int col)
    {
        var request = new ShootRequestProto { GameId = gameId, PlayerId = playerId, Row = row, Col = col };
        try
        {
            var response = await _client.ShootAsync(request);
            return MapToGameStateDto(response);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Game?> GetGameAsync(string gameId)
    {
        var request = new GetGameRequestProto { GameId = gameId };
        try
        {
            var response = await _client.GetGameAsync(request);
            return MapToGame(response);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Game?> RestartGameAsync(string gameId, string playerId)
    {
        var request = new RestartGameRequestProto { GameId = gameId, PlayerId = playerId };
        try
        {
            var response = await _client.RestartGameAsync(request);
            return MapToGame(response);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Game?> PlaceShipsAsync(string gameId, string playerId, List<ShipPlacementDto> ships)
    {
        var request = new PlaceShipsRequestProto { GameId = gameId, PlayerId = playerId };
        request.Ships.AddRange(ships.Select(s => new ShipPlacementProto
        {
            Name = s.Name,
            Size = s.Size,
            Row = s.Row,
            Col = s.Col,
            Orientation = (int)s.Orientation
        }));

        try
        {
            var response = await _client.PlaceShipsAsync(request);
            return MapToGame(response);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Game?> UndoLastMoveAsync(string gameId)
    {
        var request = new UndoLastMoveRequestProto { GameId = gameId };
        try
        {
            var response = await _client.UndoLastMoveAsync(request);
            return MapToGame(response);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync()
    {
        try
        {
            var response = await _client.GetLeaderboardAsync(new GetLeaderboardRequestProto());
            return response.Entries.Select(e => new LeaderboardEntry(e.PlayerId, e.Wins)).ToList();
        }
        catch
        {
            return new List<LeaderboardEntry>();
        }
    }

    private Game MapToGame(GameProto proto)
    {
        var game = new Game
        {
            Id = proto.Id,
            Player1Id = proto.Player1Id,
            Player2Id = proto.HasPlayer2Id ? proto.Player2Id : null,
            CurrentTurnPlayerId = proto.CurrentTurnPlayerId,
            Status = (GameStatus)proto.Status,
            WinnerId = proto.HasWinnerId ? proto.WinnerId : null,
            IsSinglePlayer = proto.IsSinglePlayer,
            Player1Board = MapToBoard(proto.Player1Board),
            Player2Board = MapToBoard(proto.Player2Board)
        };
        return game;
    }

    private Board MapToBoard(BoardProto proto)
    {
        var board = new Board { Size = proto.Size > 0 ? proto.Size : 10 };
        foreach (var shipProto in proto.Ships)
        {
            var ship = new Ship
            {
                Name = shipProto.Name,
                Size = shipProto.Size,
                Orientation = (Orientation)shipProto.Orientation,
                StartPosition = new Coordinate(shipProto.StartPosition.Row, shipProto.StartPosition.Col),
                Hits = shipProto.Hits
            };
            ship.OccupiedCoordinates = shipProto.OccupiedCoordinates.Select(c => new Coordinate(c.Row, c.Col)).ToList();
            board.AddShip(ship);
        }

        foreach (var shot in proto.ShotsFired)
        {
            board.ShotsFired[new Coordinate(shot.Coordinate.Row, shot.Coordinate.Col)] = (CellState)shot.State;
        }
        return board;
    }

    private GameStateDto MapToGameStateDto(GameStateProto proto)
    {
        return new GameStateDto(
            proto.GameId,
            proto.CurrentTurnPlayerId,
            (GameStatus)proto.Status,
            proto.HasWinnerId ? proto.WinnerId : null,
            proto.MyShots.Select(s => new ShotResultDto(s.Row, s.Col, (CellState)s.State)).ToList(),
            proto.OpponentShots.Select(s => new ShotResultDto(s.Row, s.Col, (CellState)s.State)).ToList(),
            proto.History.Select(h => new MoveHistoryDto(h.PlayerId, h.Row, h.Col, h.Result, h.SunkShipName)).ToList()
        );
    }
}
