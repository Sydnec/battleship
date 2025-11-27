namespace BattleShip.API.DTOs;

public class AttackRequest
{
    public Guid GameId { get; set; }

    public int Row { get; set; }

    public int Column { get; set; }
}
