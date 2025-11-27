using BattleShip.Models.Enums;

namespace BattleShip.Models.Entities;

public class Ship
{
    public string Name { get; set; } = string.Empty;
    public int Size { get; set; }
    public Orientation Orientation { get; set; }
    public Coordinate StartPosition { get; set; } = new(0, 0);
    public List<Coordinate> OccupiedCoordinates { get; set; } = new();
    public bool IsSunk => Hits >= Size;
    public int Hits { get; set; }
}
