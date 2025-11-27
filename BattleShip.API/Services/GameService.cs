using Battleship.Core.Models;
using Battleship.Core.Enums;
using System.Collections.Concurrent;

namespace BattleShip.API.Services;

public class GameService
{
    // Stockage thread-safe des parties en cours
    private readonly ConcurrentDictionary<Guid, GameSession> _games = new();
    
    // Leaderboard global
    private readonly Leaderboard _leaderboard = new();

    public Leaderboard Leaderboard => _leaderboard;

    public Game CreateGame(AILevel level = AILevel.Medium)
    {
        var settings = new GameSettings(level);
        var game = new Game(settings);
        
        // Créer une session avec les coups possibles de l'IA mélangés
        var session = new GameSession
        {
            Game = game,
            AIAvailableMoves = GenerateShuffledMoves(game.Settings.GridSize)
        };
        
        _games[game.Id] = session;
        
        return game;
    }

    public Game? GetGame(Guid gameId)
    {
        return _games.TryGetValue(gameId, out var session) ? session.Game : null;
    }


    public ShotResult PlayerAttack(Guid gameId, int row, int col)
    {
        if (!_games.TryGetValue(gameId, out var session))
        {
            throw new InvalidOperationException("Partie introuvable");
        }

        return session.Game.PlayerShoot(row, col);
    }

    public (int Row, int Col, ShotResult Result) AIAttack(Guid gameId)
    {
        if (!_games.TryGetValue(gameId, out var session))
        {
            throw new InvalidOperationException("Partie introuvable");
        }

        // Prendre le prochain coup disponible dans la liste mélangée
        if (session.AIAvailableMoves.Count == 0)
        {
            throw new InvalidOperationException("Aucun coup disponible pour l'IA");
        }

        var nextMove = session.AIAvailableMoves[session.AICurrentMoveIndex];
        session.AICurrentMoveIndex++;

        var result = session.Game.ComputerShoot(nextMove.Row, nextMove.Col);
        
        return (nextMove.Row, nextMove.Col, result);
    }

    public bool DeleteGame(Guid gameId)
    {
        return _games.TryRemove(gameId, out _);
    }

    /// <summary>
    /// Termine une partie et l'ajoute au leaderboard
    /// </summary>
    public void EndGame(Guid gameId, string playerName = "Joueur")
    {
        if (_games.TryGetValue(gameId, out var session) && session.Game.IsOver)
        {
            _leaderboard.AddEntry(session.Game, playerName);
        }
    }

    /// <summary>
    /// Récupère le top 10 du leaderboard
    /// </summary>
    public List<LeaderboardEntry> GetLeaderboard(int count = 10)
    {
        return _leaderboard.GetTop(count);
    }

    private List<(int Row, int Col)> GenerateShuffledMoves(int gridSize)
    {
        var moves = new List<(int Row, int Col)>();
        
        // Générer tous les coups possibles
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                moves.Add((row, col));
            }
        }
        
        // Mélanger aléatoirement (algorithme Fisher-Yates)
        for (int i = moves.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (moves[i], moves[j]) = (moves[j], moves[i]);
        }
        
        return moves;
    }
}
internal class GameSession
{
    public Game Game { get; set; } = null!;
    public List<(int Row, int Col)> AIAvailableMoves { get; set; } = new();
    public int AICurrentMoveIndex { get; set; } = 0;
}
