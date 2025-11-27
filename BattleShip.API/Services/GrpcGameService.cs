using BattleShip.Models.DTOs;
using BattleShip.Models.Entities;
using BattleShip.Models.Enums;
using BattleShip.Models.Interfaces;
using BattleShip.Protos;
using Grpc.Core;

namespace BattleShip.API.Services;

public class GrpcGameService : GrpcGame.GrpcGameBase
{
    private readonly IGameService _gameService;

    public GrpcGameService(IGameService gameService)
    {
        _gameService = gameService;
    }

    public override async Task<GameProto> CreateGame(CreateGameRequestProto request, ServerCallContext context)
    {
        var difficulty = (GameDifficulty)request.Difficulty;
        var game = await _gameService.CreateGameAsync(request.PlayerId, request.IsSinglePlayer, difficulty);
        return MapToGameProto(game);
    }

    public override async Task<GameProto> JoinGame(JoinGameRequestProto request, ServerCallContext context)
    {
        var game = await _gameService.JoinGameAsync(request.GameId, request.PlayerId);
        if (game == null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unable to join game"));
        }
        return MapToGameProto(game);
    }

    public override async Task<GameStateProto> Shoot(ShootRequestProto request, ServerCallContext context)
    {
        var result = await _gameService.ShootAsync(request.GameId, request.PlayerId, request.Row, request.Col);
        if (result == null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid move or game not found"));
        }
        return MapToGameStateProto(result);
    }

    public override async Task<GameProto> GetGame(GetGameRequestProto request, ServerCallContext context)
    {
        var game = await _gameService.GetGameAsync(request.GameId);
        if (game == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Game not found"));
        }
        return MapToGameProto(game);
    }

    public override async Task<GameProto> RestartGame(RestartGameRequestProto request, ServerCallContext context)
    {
        var game = await _gameService.RestartGameAsync(request.GameId, request.PlayerId);
        if (game == null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unable to restart game"));
        }
        return MapToGameProto(game);
    }

    public override async Task<GameProto> PlaceShips(PlaceShipsRequestProto request, ServerCallContext context)
    {
        var ships = request.Ships.Select(s => new ShipPlacementDto(s.Name, s.Size, s.Row, s.Col, (Orientation)s.Orientation)).ToList();
        var game = await _gameService.PlaceShipsAsync(request.GameId, request.PlayerId, ships);
        if (game == null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unable to place ships"));
        }
        return MapToGameProto(game);
    }

    public override async Task<GameProto> UndoLastMove(UndoLastMoveRequestProto request, ServerCallContext context)
    {
        var game = await _gameService.UndoLastMoveAsync(request.GameId);
        if (game == null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Unable to undo"));
        }
        return MapToGameProto(game);
    }

    public override async Task<LeaderboardProto> GetLeaderboard(GetLeaderboardRequestProto request, ServerCallContext context)
    {
        var entries = await _gameService.GetLeaderboardAsync();
        var proto = new LeaderboardProto();
        proto.Entries.AddRange(entries.Select(e => new LeaderboardEntryProto { PlayerId = e.PlayerId, Wins = e.Wins }));
        return proto;
    }

    private GameProto MapToGameProto(Game game)
    {
        var proto = new GameProto
        {
            Id = game.Id.ToString(),
            Player1Id = game.Player1Id,
            CurrentTurnPlayerId = game.CurrentTurnPlayerId,
            Status = (int)game.Status,
            IsSinglePlayer = game.IsSinglePlayer,
            Player1Board = MapToBoardProto(game.Player1Board),
            Player2Board = MapToBoardProto(game.Player2Board)
        };

        if (game.Player2Id != null) proto.Player2Id = game.Player2Id;
        if (game.WinnerId != null) proto.WinnerId = game.WinnerId;

        return proto;
    }

    private BoardProto MapToBoardProto(Board board)
    {
        var proto = new BoardProto { Size = board.Size };
        proto.Ships.AddRange(board.Ships.Select(s => new ShipProto
        {
            Name = s.Name,
            Size = s.Size,
            Orientation = (int)s.Orientation,
            StartPosition = new CoordinateProto { Row = s.StartPosition.Row, Col = s.StartPosition.Column },
            Hits = s.Hits,
            IsSunk = s.IsSunk
        }));
        
        // OccupiedCoordinates mapping
        foreach(var ship in board.Ships)
        {
             // We can't easily map nested repeated fields inside a select without creating objects first
             // But ShipProto has repeated OccupiedCoordinates
             var shipProto = proto.Ships.Last(); // The one we just added
             shipProto.OccupiedCoordinates.AddRange(ship.OccupiedCoordinates.Select(c => new CoordinateProto { Row = c.Row, Col = c.Column }));
        }

        proto.ShotsFired.AddRange(board.ShotsFired.Select(kvp => new ShotEntryProto
        {
            Coordinate = new CoordinateProto { Row = kvp.Key.Row, Col = kvp.Key.Column },
            State = (int)kvp.Value
        }));

        return proto;
    }

    private GameStateProto MapToGameStateProto(GameStateDto dto)
    {
        var proto = new GameStateProto
        {
            GameId = dto.GameId,
            CurrentTurnPlayerId = dto.CurrentTurnPlayerId,
            Status = (int)dto.Status
        };

        if (dto.WinnerId != null) proto.WinnerId = dto.WinnerId;

        proto.MyShots.AddRange(dto.MyShots.Select(s => new ShotResultProto { Row = s.Row, Col = s.Column, State = (int)s.State }));
        proto.OpponentShots.AddRange(dto.OpponentShots.Select(s => new ShotResultProto { Row = s.Row, Col = s.Column, State = (int)s.State }));
        
        if (dto.History != null)
        {
            proto.History.AddRange(dto.History.Select(h => new MoveHistoryProto
            {
                PlayerId = h.PlayerId,
                Row = h.Row,
                Col = h.Col,
                Result = h.Result,
                SunkShipName = h.SunkShipName ?? ""
            }));
        }

        return proto;
    }
}
