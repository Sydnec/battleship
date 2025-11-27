namespace Battleship.Core.Models
{
    public class Ship
    {
        public char Letter { get; }

        public int Size { get; }

        public Ship(char letter, int size)
        {
            // Validation de base des paramètres
            if (size < 1 || size > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "La taille du navire doit être comprise entre 1 et 4.");
            }
            if (letter < 'A' || letter > 'Z')
            {
                throw new ArgumentOutOfRangeException(nameof(letter), "La lettre du navire doit être une lettre majuscule.");
            }
            
            this.Letter = letter;
            this.Size = size;
        }

        public override string ToString()
        {
            return $"Ship {Letter} (Size: {Size})";
        }
    }
}
