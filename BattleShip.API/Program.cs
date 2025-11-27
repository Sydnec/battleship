using BattleShip.API.Hubs;
using BattleShip.API.Services;
using BattleShip.API.Validators;
using BattleShip.Models.DTOs;
using BattleShip.Models.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddGrpc();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IGameService, GameService>();
builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<IValidator<ShootRequest>, ShootRequestValidator>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.SetIsOriginAllowed(origin => true) // Allow any origin for dev
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials()
                   .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
        });
});

var app = builder.Build();

app.UseGrpcWeb();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "BattleShip API");
    });
}

app.UseCors("AllowAll");

app.MapGrpcService<GrpcGameService>().EnableGrpcWeb();
app.MapHub<GameHub>("/gamehub");

app.MapPost("/games", async (CreateGameRequest request, IGameService gameService) =>
{
    var game = await gameService.CreateGameAsync(request.PlayerId, isSinglePlayer: request.IsSinglePlayer, difficulty: request.Difficulty);
    return Results.Created($"/games/{game.Id}", game);
})
.WithName("CreateGame");

app.MapPost("/games/{id}/shoot", async Task<Results<Ok<GameStateDto>, BadRequest<string>, ValidationProblem>> (string id, ShootRequest request, IGameService gameService, IValidator<ShootRequest> validator) =>
{
    var validationResult = await validator.ValidateAsync(request);
    if (!validationResult.IsValid)
    {
        return TypedResults.ValidationProblem(validationResult.ToDictionary());
    }

    if (id != request.GameId) return TypedResults.BadRequest("Game ID mismatch");
    
    var result = await gameService.ShootAsync(id, request.PlayerId, request.Row, request.Column);
    
    if (result == null) return TypedResults.BadRequest("Invalid move or game not found");
    
    return TypedResults.Ok(result);
})
.WithName("Shoot");

app.MapGet("/games/{id}", async (string id, IGameService gameService) =>
{
    var game = await gameService.GetGameAsync(id);
    return game is not null ? Results.Ok(game) : Results.NotFound();
})
.WithName("GetGame");

app.MapPost("/games/{id}/restart", async (string id, CreateGameRequest request, IGameService gameService) =>
{
    var game = await gameService.RestartGameAsync(id, request.PlayerId);
    return game is not null ? Results.Ok(game) : Results.BadRequest("Unable to restart game");
})
.WithName("RestartGame");

app.MapPost("/games/{id}/place-ships", async (string id, PlaceShipsRequest request, IGameService gameService) =>
{
    if (id != request.GameId) return Results.BadRequest("Game ID mismatch");
    var game = await gameService.PlaceShipsAsync(id, request.PlayerId, request.Ships);
    return game is not null ? Results.Ok(game) : Results.BadRequest("Unable to place ships");
})
.WithName("PlaceShips");

app.MapPost("/games/{id}/join", async (string id, JoinGameRequest request, IGameService gameService) =>
{
    if (id != request.GameId) return Results.BadRequest("Game ID mismatch");
    var game = await gameService.JoinGameAsync(id, request.PlayerId);
    return game is not null ? Results.Ok(game) : Results.BadRequest("Unable to join game");
})
.WithName("JoinGame");

app.MapPost("/games/{id}/undo", async (string id, IGameService gameService) =>
{
    var game = await gameService.UndoLastMoveAsync(id);
    return game is not null ? Results.Ok(game) : Results.BadRequest("Unable to undo");
})
.WithName("UndoLastMove");

app.MapGet("/leaderboard", async (ILeaderboardService service) => await service.GetLeaderboardAsync())
.WithName("GetLeaderboard");

app.Run();
