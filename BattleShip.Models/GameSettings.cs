using Battleship.Core.Enums;

namespace Battleship.Core.Models
{
    public class GameSettings
    {
        public int GridSize { get; set; }
        
        public AILevel AIIntelligence { get; set; }
        
        // Alias pour compatibilité
        public AILevel Level => AIIntelligence;
        
        public List<int> ShipSizes { get; set; } = new List<int>();

        public GameSettings(AILevel level = AILevel.Medium)
        {
            switch (level)
            {
                case AILevel.Easy:
                    GridSize = 9;
                    AIIntelligence = AILevel.Easy;
                    ShipSizes = new List<int> { 1, 2, 3, 4 };
                    break;
                case AILevel.Medium:
                    GridSize = 10;
                    AIIntelligence = AILevel.Medium;
                    ShipSizes = new List<int> { 1, 2, 3, 3, 4, 4 };
                    break;
                case AILevel.Hard:
                    GridSize = 11;
                    AIIntelligence = AILevel.Hard;
                    ShipSizes = new List<int> { 1, 1, 2, 2, 3, 4 };
                    break;
            }
        }

        public bool IsValid()
        {
            if (GridSize < 5 || GridSize > 20)
                return false;

            if (ShipSizes == null || ShipSizes.Count == 0)
                return false;

            // Vérifier que toutes les tailles de bateaux sont valides (1-4)
            foreach (var size in ShipSizes)
            {
                if (size < 1 || size > 4)
                    return false;
            }

            return true;
        }
    }
}
