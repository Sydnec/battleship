namespace BattleShip.API.DTOs;

public class GameResponse
{
    public Guid GameId { get; set; }
    
    public int GridSize { get; set; }
    
    public char[][] PlayerGrid { get; set; } = Array.Empty<char[]>();
    
    public List<ShipInfo> Ships { get; set; } = new();
}

public class ShipInfo
{
    public char Letter { get; set; }
    public int Size { get; set; }
}
