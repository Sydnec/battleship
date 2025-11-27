using Battleship.Core.Enums;

namespace Battleship.Core.Models
{
    public class Game
    {
        public Guid Id { get; } = Guid.NewGuid();

        public GameSettings Settings { get; }

        public Board PlayerBoard { get; }

        public Board ComputerBoard { get; }

        public GameState State { get; set; } = GameState.Playing;

        public List<Move> MoveHistory { get; } = new List<Move>();

        // Statistiques du jeu
        public int PlayerHits { get; private set; } = 0;
        public int PlayerMisses { get; private set; } = 0;
        public int ComputerHits { get; private set; } = 0;
        public int ComputerMisses { get; private set; } = 0;
        public DateTime StartTime { get; } = DateTime.UtcNow;
        public DateTime? EndTime { get; private set; }
        public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : DateTime.UtcNow - StartTime;
        public int PlayerShipsSunk => GetSunkShipsCount(PlayerBoard);
        public int ComputerShipsSunk => GetSunkShipsCount(ComputerBoard);

        public bool IsOver
        {
            get
            {
                return this.State == GameState.GameOver;
            }
        }

        // Constructeur avec GameSettings
        public Game(GameSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (!settings.IsValid())
                throw new ArgumentException("Les paramètres de jeu ne sont pas valides.", nameof(settings));

            this.Settings = settings;
            this.PlayerBoard = new Board(settings.GridSize);
            this.ComputerBoard = new Board(settings.GridSize);
            
            // Génération automatique des bateaux
            InitializeShips();
        }

        // Constructeur par défaut avec configuration Medium
        public Game() : this(new GameSettings(AILevel.Medium))
        {
        }

        /// <summary>
        /// Initialise et place automatiquement les navires pour le joueur et l'ordinateur
        /// </summary>
        public void InitializeShips()
        {
            // Génération automatique pour le joueur
            this.PlayerBoard.GenerateShipsRandomly(this.Settings.ShipSizes);
            
            // Génération automatique pour l'ordinateur (secrète)
            this.ComputerBoard.GenerateShipsRandomly(this.Settings.ShipSizes);
            
            this.State = GameState.Playing;
        }

        /// <summary>
        /// Le joueur tire sur la grille de l'ordinateur
        /// </summary>
        public ShotResult PlayerShoot(int row, int col)
        {
            if (State != GameState.Playing)
            {
                throw new InvalidOperationException("La partie n'est pas en cours.");
            }

            var result = ComputerBoard.Shoot(row, col);
            
            // Enregistrer le coup dans l'historique
            char? hitLetter = null;
            if (result == ShotResult.Hit || result == ShotResult.Sunk)
            {
                hitLetter = ComputerBoard.Grid[row, col];
                PlayerHits++;
            }
            else
            {
                PlayerMisses++;
            }
            
            var move = new Move(row, col, isPlayerMove: true, isHit: result != ShotResult.Miss, hitLetter);
            RecordMove(move);

            // Vérifier si tous les bateaux de l'ordinateur sont coulés
            if (ComputerBoard.AreAllShipsSunk())
            {
                State = GameState.GameOver;
                EndTime = DateTime.UtcNow;
            }

            return result;
        }

        /// <summary>
        /// L'ordinateur tire sur la grille du joueur (IA simple)
        /// </summary>
        public ShotResult ComputerShoot()
        {
            if (State != GameState.Playing)
            {
                throw new InvalidOperationException("La partie n'est pas en cours.");
            }

            // IA simple : choisir une case aléatoire non encore attaquée
            (int row, int col) = GetRandomUnshotCoordinate(PlayerBoard);
            
            var result = PlayerBoard.Shoot(row, col);
            
            // Enregistrer le coup dans l'historique
            char? hitLetter = null;
            if (result == ShotResult.Hit || result == ShotResult.Sunk)
            {
                hitLetter = PlayerBoard.Grid[row, col];
                ComputerHits++;
            }
            else
            {
                ComputerMisses++;
            }
            
            var move = new Move(row, col, isPlayerMove: false, isHit: result != ShotResult.Miss, hitLetter);
            RecordMove(move);

            // Vérifier si tous les bateaux du joueur sont coulés
            if (PlayerBoard.AreAllShipsSunk())
            {
                State = GameState.GameOver;
                EndTime = DateTime.UtcNow;
            }

            return result;
        }

        /// <summary>
        /// L'ordinateur tire sur la grille du joueur à des coordonnées spécifiques
        /// </summary>
        public ShotResult ComputerShoot(int row, int col)
        {
            if (State != GameState.Playing)
            {
                throw new InvalidOperationException("La partie n'est pas en cours.");
            }

            var result = PlayerBoard.Shoot(row, col);
            
            // Enregistrer le coup dans l'historique
            char? hitLetter = null;
            if (result == ShotResult.Hit || result == ShotResult.Sunk)
            {
                hitLetter = PlayerBoard.Grid[row, col];
                ComputerHits++;
            }
            else
            {
                ComputerMisses++;
            }
            
            var move = new Move(row, col, isPlayerMove: false, isHit: result != ShotResult.Miss, hitLetter);
            RecordMove(move);

            // Vérifier si tous les bateaux du joueur sont coulés
            if (PlayerBoard.AreAllShipsSunk())
            {
                State = GameState.GameOver;
                EndTime = DateTime.UtcNow;
            }

            return result;
        }

        /// <summary>
        /// Obtient une coordonnée aléatoire qui n'a pas encore été tirée
        /// </summary>
        private (int row, int col) GetRandomUnshotCoordinate(Board board)
        {
            var availableCoordinates = new List<(int row, int col)>();
            
            for (int r = 0; r < board.Size; r++)
            {
                for (int c = 0; c < board.Size; c++)
                {
                    char cell = board.Grid[r, c];
                    // Cases non touchées : '\0' (vide) ou 'A'-'F' (bateau)
                    if (cell != 'X' && cell != 'O')
                    {
                        availableCoordinates.Add((r, c));
                    }
                }
            }

            if (availableCoordinates.Count == 0)
            {
                throw new InvalidOperationException("Aucune case disponible pour tirer.");
            }

            int randomIndex = Random.Shared.Next(availableCoordinates.Count);
            return availableCoordinates[randomIndex];
        }

        /// <summary>
        /// Enregistre un coup dans l'historique
        /// </summary>
        public void RecordMove(Move move)
        {
            MoveHistory.Add(move);
        }

        /// <summary>
        /// Annule le dernier coup (et le coup de l'adversaire si nécessaire)
        /// </summary>
        public bool UndoLastMove()
        {
            if (MoveHistory.Count == 0)
            {
                return false;
            }

            // Récupérer le dernier coup
            var lastMove = MoveHistory[^1];
            MoveHistory.RemoveAt(MoveHistory.Count - 1);

            // Annuler le coup sur le plateau approprié
            var board = lastMove.IsPlayerMove ? ComputerBoard : PlayerBoard;
            int row = lastMove.Row;
            int col = lastMove.Column;
            
            // Restaurer l'état de la case
            if (lastMove.IsHit && lastMove.HitShipLetter.HasValue)
            {
                // C'était un bateau touché, remettre la lettre
                board.Grid[row, col] = lastMove.HitShipLetter.Value;

                if (lastMove.IsPlayerMove)
                {
                    PlayerHits--;
                }
                else
                {
                    ComputerHits--;
                }
            }
            else
            {
                if (lastMove.IsPlayerMove)
                {
                    PlayerMisses--;
                }
                else
                {
                    ComputerMisses--;
                }
                // C'était un raté ou vide, remettre à vide
                board.Grid[row, col] = '\0';
            }

            // Si le jeu était terminé, le remettre en cours
            if (State == GameState.GameOver)
            {
                State = GameState.Playing;
                EndTime = null;
            }

            return true;
        }

        /// <summary>
        /// Annule les N derniers coups
        /// </summary>
        public int UndoMoves(int count)
        {
            int undoneCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (UndoLastMove())
                {
                    undoneCount++;
                }
                else
                {
                    break;
                }
            }
            return undoneCount;
        }

        // Obtenir le nombre de navires total
        public int GetTotalShipCount()
        {
            return this.Settings.ShipSizes.Count;
        }

        // Obtenir la liste des tailles de navires
        public List<int> GetShipSizes()
        {
            return new List<int>(this.Settings.ShipSizes);
        }

        /// <summary>
        /// Obtient la vue de la grille de l'ordinateur pour le joueur (X/O seulement)
        /// </summary>
        public char[,] GetComputerBoardPlayerView()
        {
            return ComputerBoard.GetPlayerView();
        }

        /// <summary>
        /// Obtient la vue complète de la grille du joueur (lettres + X/O)
        /// </summary>
        public char[,] GetPlayerBoardFullView()
        {
            return PlayerBoard.GetFullView();
        }

        /// <summary>
        /// Compte le nombre de bateaux coulés sur un plateau
        /// </summary>
        private int GetSunkShipsCount(Board board)
        {
            int count = 0;
            
            foreach (var ship in board.Ships)
            {
                bool isSunk = true;
                
                // Vérifier si toutes les cases du bateau sont touchées
                for (int row = 0; row < board.Size; row++)
                {
                    for (int col = 0; col < board.Size; col++)
                    {
                        if (board.Grid[row, col] == ship.Letter)
                        {
                            // Si on trouve une case non touchée du bateau, il n'est pas coulé
                            isSunk = false;
                            break;
                        }
                    }
                    if (!isSunk) break;
                }
                
                if (isSunk)
                {
                    count++;
                }
            }
            
            return count;
        }
    }
}