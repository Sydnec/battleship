namespace BattleShip.API.DTOs;

public class AttackResponse
{
    public string PlayerAttackResult { get; set; } = string.Empty;
    
    public char? HitShipLetter { get; set; }
    
    public AttackInfo? AIAttack { get; set; }
    
    public string GameState { get; set; } = "Playing";
    
    public string? Winner { get; set; }
    
    public char[][] PlayerGrid { get; set; } = Array.Empty<char[]>();
    
    public char[][] OpponentView { get; set; } = Array.Empty<char[]>();
}

public class AttackInfo
{
    public int Row { get; set; }
    public int Column { get; set; }
    public string Result { get; set; } = string.Empty;
    public char? HitShipLetter { get; set; }
}
