namespace BattleShip.App.Services;

public class GameState
{
    public Guid? GameId { get; set; }

    public int GridSize { get; set; } = 10;

    public char[,] PlayerGrid { get; set; } = new char[10, 10];

    public bool?[,] OpponentGrid { get; set; } = new bool?[10, 10];

    public string GameStatus { get; set; } = "NotStarted";

    public string? Winner { get; set; }

    public List<ShipInfo> Ships { get; set; } = new();

    public int MovesCount { get; set; } = 0;

    // Statistiques
    public int PlayerHits { get; set; } = 0;
    public int PlayerMisses { get; set; } = 0;
    public int ComputerHits { get; set; } = 0;
    public int ComputerMisses { get; set; } = 0;
    public int PlayerShipsSunk { get; set; } = 0;
    public int ComputerShipsSunk { get; set; } = 0;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    // Bateaux coulés récemment (pour les notifications)
    public List<string> RecentlySunkShips { get; set; } = new();

    /// <summary>
    /// Historique des coups
    /// </summary>
    public List<MoveInfo> MoveHistory { get; set; } = new();

    public void Reset()
    {
        GameId = null;
        GridSize = 10;
        PlayerGrid = new char[10, 10];
        OpponentGrid = new bool?[10, 10];
        GameStatus = "NotStarted";
        Winner = null;
        Ships.Clear();
        MovesCount = 0;
        PlayerHits = 0;
        PlayerMisses = 0;
        ComputerHits = 0;
        ComputerMisses = 0;
        PlayerShipsSunk = 0;
        ComputerShipsSunk = 0;
        StartTime = null;
        EndTime = null;
        RecentlySunkShips.Clear();
        MoveHistory.Clear();
    }

    public event Action? OnChange;

    public void NotifyStateChanged() => OnChange?.Invoke();
}

public class ShipInfo
{
    public char Letter { get; set; }
    public int Size { get; set; }
}

public class MoveInfo
{
    public Guid MoveId { get; set; }
    public DateTime Timestamp { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public bool IsPlayerMove { get; set; }
    public bool IsHit { get; set; }
    public char? HitShipLetter { get; set; }
}
