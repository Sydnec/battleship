using BattleShip.Models.Enums;

namespace BattleShip.Models.Entities;

public class Game
{
    public string Id { get; set; } = GenerateShortId();
    public string Player1Id { get; set; } = string.Empty;
    public string? Player2Id { get; set; }
    public Board Player1Board { get; set; } = new();
    public Board Player2Board { get; set; } = new();
    public string CurrentTurnPlayerId { get; set; } = string.Empty;
    public GameStatus Status { get; set; } = GameStatus.WaitingForPlayer;
    public string? WinnerId { get; set; }
    public bool IsSinglePlayer { get; set; }
    public GameDifficulty Difficulty { get; set; } = GameDifficulty.Medium;
    public List<Move> MoveHistory { get; set; } = new();

    private static string GenerateShortId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
