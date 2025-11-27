using BattleShip.Models.Enums;

namespace BattleShip.Models.Entities;

public class Move
{
    public string PlayerId { get; set; } = string.Empty;
    public int Row { get; set; }
    public int Col { get; set; }
    public CellState Result { get; set; }
    public string? SunkShipName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
