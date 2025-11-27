using BattleShip.API.Services;
using BattleShip.API.DTOs;
using BattleShip.API.Helpers;
using BattleShip.API.Validators;
using BattleShip.API.GrpcServices;
using Battleship.Core.Enums;
using Battleship.Core.Models;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSingleton<GameService>();

// Enregistrer les validateurs FluentValidation
builder.Services.AddScoped<IValidator<AttackRequest>, AttackRequestValidator>();

// Ajouter le support gRPC
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
});

// Configure CORS pour permettre les appels depuis l'application Blazor (REST + gRPC-Web)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp", policy =>
    {
        policy.WithOrigins("http://localhost:5046", "https://localhost:5046")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowBlazorApp");
app.UseHttpsRedirection();
app.UseGrpcWeb();

// Enregistrer le service gRPC avec support gRPC-Web
app.MapGrpcService<BattleShipGrpcService>().EnableGrpcWeb();

var gameService = app.Services.GetRequiredService<GameService>();

// ===== ENDPOINTS BATAILLE NAVALE =====

app.MapPost("/api/game/start", (AILevel? level) =>
{
    var game = gameService.CreateGame(level ?? AILevel.Medium);
    
    var response = new GameResponse
    {
        GameId = game.Id,
        GridSize = game.Settings.GridSize,
        PlayerGrid = GridHelper.ToJaggedArray(game.PlayerBoard.Grid),
        Ships = game.PlayerBoard.Ships.Select(s => new ShipInfo
        {
            Letter = s.Letter,
            Size = s.Size
        }).ToList()
    };
    
    return Results.Ok(response);
})
.WithName("StartGame")
.WithDescription("Démarre une nouvelle partie de bataille navale")
.Produces<GameResponse>(StatusCodes.Status200OK);

app.MapPost("/api/game/attack", async (AttackRequest request, IValidator<AttackRequest> validator) =>
{
    // Valider la requête (inclut la vérification d'existence du jeu et des coordonnées)
    var validationResult = await validator.ValidateAsync(request);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    try
    {
        var game = gameService.GetGame(request.GameId);
        
        // Le jeu existe forcément car le validateur l'a vérifié
        if (game!.IsOver)
        {
            return Results.BadRequest(new { error = "La partie est terminée" });
        }

        // Attaque du joueur (les coordonnées sont déjà validées par le validateur)
        var playerResult = gameService.PlayerAttack(request.GameId, request.Row, request.Column);
        
        char? hitLetter = null;
        if (playerResult != ShotResult.Miss)
        {
            hitLetter = game.ComputerBoard.Grid[request.Row, request.Column];
        }

        // Vérifier si la partie est terminée après l'attaque du joueur
        var response = new AttackResponse
        {
            PlayerAttackResult = playerResult.ToString(),
            HitShipLetter = hitLetter,
            GameState = game.State.ToString(),
            OpponentView = GridHelper.ToJaggedArray(game.ComputerBoard.GetPlayerView())
        };

        // Si la partie n'est pas terminée, l'IA attaque
        if (!game.IsOver)
        {
            var (aiRow, aiCol, aiResult) = gameService.AIAttack(request.GameId);
            
            char? aiHitLetter = null;
            if (aiResult != ShotResult.Miss)
            {
                aiHitLetter = game.PlayerBoard.Grid[aiRow, aiCol];
            }
            
            response.AIAttack = new AttackInfo
            {
                Row = aiRow,
                Column = aiCol,
                Result = aiResult.ToString(),
                HitShipLetter = aiHitLetter
            };
            
            // Mettre à jour l'état après l'attaque de l'IA
            response.GameState = game.State.ToString();
        }

        // Mettre à jour la grille du joueur APRÈS l'attaque de l'IA (si elle a eu lieu)
        response.PlayerGrid = GridHelper.ToJaggedArray(game.PlayerBoard.GetFullView());

        // Déterminer le gagnant si la partie est terminée
        if (game.IsOver)
        {
            response.Winner = game.ComputerBoard.AreAllShipsSunk() ? "Player": "AI";
        }

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("Attack")
.WithDescription("Effectue une attaque sur la grille adverse")
.Produces<AttackResponse>(StatusCodes.Status200OK)
.ProducesValidationProblem(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status400BadRequest);

app.MapGet("/api/game/{gameId:guid}", (Guid gameId) =>
{
    var game = gameService.GetGame(gameId);
    if (game == null)
    {
        return Results.NotFound(new { error = "Partie introuvable" });
    }

    var response = new
    {
        GameId = game.Id,
        GridSize = game.Settings.GridSize,
        GameState = game.State.ToString(),
        PlayerGrid = GridHelper.ToJaggedArray(game.PlayerBoard.GetFullView()),
        OpponentView = GridHelper.ToJaggedArray(game.ComputerBoard.GetPlayerView()),
        MoveHistory = game.MoveHistory.Select(m => new
        {
            m.MoveId,
            m.Timestamp,
            m.Row,
            m.Column,
            m.IsPlayerMove,
            m.IsHit,
            m.HitShipLetter
        }).ToList()
    };

    return Results.Ok(response);
})
.WithName("GetGameState")
.WithDescription("Récupère l'état actuel d'une partie")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

/// <summary>
/// Annule les N derniers coups
/// </summary>
app.MapPost("/api/game/{gameId:guid}/undo/{count:int}", (Guid gameId, int count) =>
{
    var game = gameService.GetGame(gameId);
    if (game == null)
    {
        return Results.NotFound(new { error = "Partie introuvable" });
    }

    if (count <= 0)
    {
        return Results.BadRequest(new { error = "Le nombre de coups à annuler doit être supérieur à 0" });
    }

    // Annuler les coups
    int undoneCount = game.UndoMoves(count);

    var response = new
    {
        UndoneCount = undoneCount,
        GameState = game.State.ToString(),
        Winner = game.IsOver ? (game.ComputerBoard.AreAllShipsSunk() ? "Player" : "AI") : null,
        PlayerGrid = GridHelper.ToJaggedArray(game.PlayerBoard.GetFullView()),
        OpponentView = GridHelper.ToJaggedArray(game.ComputerBoard.GetPlayerView())
    };

    return Results.Ok(response);
})
.WithName("UndoMoves")
.WithDescription("Annule les N derniers coups")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status400BadRequest);

/// <summary>
/// Récupère le leaderboard
/// </summary>
app.MapGet("/api/game/leaderboard", (int? count) =>
{
    var entries = gameService.GetLeaderboard(count ?? 10);
    return Results.Ok(entries);
})
.WithName("GetLeaderboard")
.WithDescription("Récupère le classement des meilleures parties")
.Produces<List<LeaderboardEntry>>(StatusCodes.Status200OK);

/// <summary>
/// Termine une partie et l'ajoute au leaderboard
/// </summary>
app.MapPost("/api/game/{gameId:guid}/end", (Guid gameId, string? playerName) =>
{
    var game = gameService.GetGame(gameId);
    if (game == null)
    {
        return Results.NotFound(new { error = "Partie introuvable" });
    }

    if (!game.IsOver)
    {
        return Results.BadRequest(new { error = "La partie n'est pas terminée" });
    }

    gameService.EndGame(gameId, playerName ?? "Joueur");
    
    return Results.Ok(new { message = "Partie ajoutée au classement" });
})
.WithName("EndGame")
.WithDescription("Termine une partie et l'ajoute au leaderboard")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status400BadRequest);

app.Run();
