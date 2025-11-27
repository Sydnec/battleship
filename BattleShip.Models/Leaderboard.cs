using Battleship.Core.Enums;

namespace Battleship.Core.Models
{
    /// <summary>
    /// Représente une entrée dans le classement
    /// </summary>
    public class LeaderboardEntry
    {
        public Guid GameId { get; set; }
        public string PlayerName { get; set; } = "Joueur";
        public DateTime Date { get; set; }
        public TimeSpan Duration { get; set; }
        public AILevel Difficulty { get; set; }
        public bool Victory { get; set; }
        public int PlayerHits { get; set; }
        public int PlayerMisses { get; set; }
        public int TotalShots => PlayerHits + PlayerMisses;
        public double Accuracy => TotalShots > 0 ? (double)PlayerHits / TotalShots * 100 : 0;
        public int ComputerShipsSunk { get; set; }
        public int Score { get; set; }
    }

    /// <summary>
    /// Service de gestion du leaderboard
    /// </summary>
    public class Leaderboard
    {
        private readonly List<LeaderboardEntry> _entries = new();

        public IReadOnlyList<LeaderboardEntry> Entries => _entries.AsReadOnly();

        /// <summary>
        /// Ajoute une entrée au classement
        /// </summary>
        public void AddEntry(Game game, string playerName = "Joueur")
        {
            if (game == null || !game.IsOver)
                return;

            var victory = game.ComputerBoard.AreAllShipsSunk();
            var score = CalculateScore(game, victory);

            var entry = new LeaderboardEntry
            {
                GameId = game.Id,
                PlayerName = playerName,
                Date = game.EndTime ?? DateTime.UtcNow,
                Duration = game.Duration,
                Difficulty = game.Settings.Level,
                Victory = victory,
                PlayerHits = game.PlayerHits,
                PlayerMisses = game.PlayerMisses,
                ComputerShipsSunk = game.ComputerShipsSunk,
                Score = score
            };

            _entries.Add(entry);
            _entries.Sort((a, b) => b.Score.CompareTo(a.Score)); // Trier par score décroissant
        }

        /// <summary>
        /// Calcule le score d'une partie
        /// </summary>
        private int CalculateScore(Game game, bool victory)
        {
            if (!victory)
                return 0;

            int baseScore = 1000;
            
            // Bonus selon la difficulté
            int difficultyMultiplier = game.Settings.Level switch
            {
                AILevel.Easy => 1,
                AILevel.Medium => 2,
                AILevel.Hard => 3,
                _ => 1
            };

            // Bonus pour la précision
            double accuracy = game.PlayerHits + game.PlayerMisses > 0
                ? (double)game.PlayerHits / (game.PlayerHits + game.PlayerMisses)
                : 0;
            int accuracyBonus = (int)(accuracy * 500);

            // Bonus pour la rapidité (moins de temps = plus de points)
            int speedBonus = Math.Max(0, 500 - (int)game.Duration.TotalSeconds);

            // Bonus pour les bateaux coulés
            int sunkBonus = game.ComputerShipsSunk * 100;

            return (baseScore + accuracyBonus + speedBonus + sunkBonus) * difficultyMultiplier;
        }

        /// <summary>
        /// Récupère le top N des entrées
        /// </summary>
        public List<LeaderboardEntry> GetTop(int count = 10)
        {
            return _entries.Take(count).ToList();
        }

        /// <summary>
        /// Récupère les victoires uniquement
        /// </summary>
        public List<LeaderboardEntry> GetVictories()
        {
            return _entries.Where(e => e.Victory).ToList();
        }
    }
}
