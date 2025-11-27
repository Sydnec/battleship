using Battleship.Core.Enums;

namespace Battleship.Core.Models
{
    public class Board
    {
        public int Size { get; }
        
        public char[,] Grid { get; }

        public List<Ship> Ships { get; } = new List<Ship>();

        public Board(int size)
        {
            this.Size = size;
            this.Grid = new char[size, size];
            
            // Initialisation : grille vide
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    this.Grid[r, c] = '\0'; // Vide
                }
            }
        }
        
        private void ValidateCoordinates(int row, int col)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size)
            {
                throw new ArgumentOutOfRangeException($"Coordonnées ({row},{col}) hors de la grille.");
            }
        }

        public void GenerateShipsRandomly(List<int> shipSizes)
        {
            char shipLetter = 'A';
            
            foreach (var size in shipSizes)
            {
                bool placed = false;
                int attempts = 0;
                const int maxAttempts = 1000; // Éviter une boucle infinie
                
                while (!placed && attempts < maxAttempts)
                {
                    attempts++;
                    
                    // Orientation aléatoire
                    bool isHorizontal = Random.Shared.Next(2) == 0;
                    
                    // Position de départ aléatoire
                    int row = Random.Shared.Next(Size);
                    int col = Random.Shared.Next(Size);
                    
                    if (CanPlaceShip(row, col, size, isHorizontal))
                    {
                        PlaceShip(row, col, size, isHorizontal, shipLetter);
                        placed = true;
                    }
                }
                
                if (!placed)
                {
                    throw new InvalidOperationException($"Impossible de placer le bateau {shipLetter} après {maxAttempts} tentatives.");
                }
                
                shipLetter++;
            }
        }

        private bool CanPlaceShip(int startRow, int startCol, int size, bool isHorizontal)
        {
            // Vérifier que le bateau ne dépasse pas de la grille
            if (isHorizontal)
            {
                if (startCol + size > Size) return false;
            }
            else
            {
                if (startRow + size > Size) return false;
            }

            // Vérifier qu'aucune case n'est déjà occupée
            for (int i = 0; i < size; i++)
            {
                int row = isHorizontal ? startRow : startRow + i;
                int col = isHorizontal ? startCol + i : startCol;
                
                if (Grid[row, col] != '\0')
                {
                    return false;
                }
            }

            return true;
        }

        private void PlaceShip(int startRow, int startCol, int size, bool isHorizontal, char letter)
        {
            var ship = new Ship(letter, size);
            Ships.Add(ship);

            for (int i = 0; i < size; i++)
            {
                int row = isHorizontal ? startRow : startRow + i;
                int col = isHorizontal ? startCol + i : startCol;
                
                Grid[row, col] = letter;
            }
        }

        public ShotResult Shoot(int row, int col)
        {
            ValidateCoordinates(row, col);
            
            char cell = Grid[row, col];
            
            // Vérifier si déjà tiré
            if (cell == 'X' || cell == 'O')
            {
                throw new InvalidOperationException("Cette case a déjà été attaquée.");
            }

            // Case vide = raté
            if (cell == '\0')
            {
                Grid[row, col] = 'O';
                return ShotResult.Miss;
            }

            // Case avec bateau = touché
            char shipLetter = cell;
            Grid[row, col] = 'X';
            
            // Vérifier si le bateau est coulé
            bool isSunk = IsShipSunk(shipLetter);

            return isSunk ? ShotResult.Sunk : ShotResult.Hit;
        }
        
        /// <summary>
        /// Vérifie si un bateau (identifié par sa lettre) est complètement coulé
        /// </summary>
        private bool IsShipSunk(char shipLetter)
        {
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    // Si on trouve encore une case de ce bateau non touchée
                    if (Grid[r, c] == shipLetter)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public char[,] GetPlayerView()
        {
            var view = new char[Size, Size];
            
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    char cell = Grid[r, c];
                    
                    if (cell == 'X' || cell == 'O')
                    {
                        view[r, c] = cell; // Afficher X ou O
                    }
                    else
                    {
                        view[r, c] = '\0'; // Masquer le reste (vide ou bateau non touché)
                    }
                }
            }
            
            return view;
        }

        public char[,] GetFullView()
        {
            // La grille contient déjà tout : bateaux (A-F), X, O, vide (\0)
            return Grid;
        }

        public bool AreAllShipsSunk()
        {
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    char cell = Grid[r, c];
                    
                    // Si on trouve une lettre de bateau (A-F), il reste des parties non touchées
                    if (cell >= 'A' && cell <= 'F')
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}