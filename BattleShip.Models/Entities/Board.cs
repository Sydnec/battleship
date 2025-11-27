using BattleShip.Models.Enums;

namespace BattleShip.Models.Entities;

public class Board
{
    public int Size { get; set; } = 10;
    public List<Ship> Ships { get; set; } = new();
    public Dictionary<Coordinate, CellState> ShotsFired { get; set; } = new();

    public void AddShip(Ship ship)
    {
        Ships.Add(ship);
    }

    public CellState ReceiveShot(Coordinate coordinate)
    {
        if (ShotsFired.ContainsKey(coordinate))
        {
            return ShotsFired[coordinate];
        }

        var hitShip = Ships.FirstOrDefault(s => s.OccupiedCoordinates.Contains(coordinate));

        if (hitShip != null)
        {
            hitShip.Hits++;
            var state = hitShip.IsSunk ? CellState.Sunk : CellState.Hit;
            ShotsFired[coordinate] = state;
            return state;
        }

        ShotsFired[coordinate] = CellState.Miss;
        return CellState.Miss;
    }
}
