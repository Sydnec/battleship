namespace Battleship.Core.Models
{
    public class Move(int row, int column, bool isPlayerMove, bool isHit, char? hitShipLetter = null)
    {
        public Guid MoveId { get; } = Guid.NewGuid();

        public DateTime Timestamp { get; } = DateTime.UtcNow;

        public int Row { get; } = row;

        public int Column { get; } = column;

        public bool IsPlayerMove { get; } = isPlayerMove;

        public bool IsHit { get; } = isHit;

        public char? HitShipLetter { get; } = hitShipLetter;

        public override string ToString()
        {
            var player = IsPlayerMove ? "Joueur" : "IA";
            var result = IsHit ? $"Touché ({HitShipLetter})" : "Raté";
            return $"{player} - ({Row},{Column}): {result}";
        }
    }
}
