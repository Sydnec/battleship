namespace BattleShip.API.Helpers;

/// <summary>
/// Helper pour convertir les grilles char[,] en char[][] pour la sérialisation JSON
/// </summary>
public static class GridHelper
{
    /// <summary>
    /// Convertit un tableau 2D en tableau de tableaux (jagged array)
    /// </summary>
    public static char[][] ToJaggedArray(char[,] grid)
    {
        if (grid == null)
            return Array.Empty<char[]>();

        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        
        var result = new char[rows][];
        
        for (int r = 0; r < rows; r++)
        {
            result[r] = new char[cols];
            for (int c = 0; c < cols; c++)
            {
                result[r][c] = grid[r, c];
            }
        }
        
        return result;
    }

    /// <summary>
    /// Convertit un tableau 2D en liste de listes
    /// </summary>
    public static List<List<char>> ToListOfLists(char[,] grid)
    {
        if (grid == null)
            return new List<List<char>>();

        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        
        var result = new List<List<char>>(rows);
        
        for (int r = 0; r < rows; r++)
        {
            var row = new List<char>(cols);
            for (int c = 0; c < cols; c++)
            {
                row.Add(grid[r, c]);
            }
            result.Add(row);
        }
        
        return result;
    }
}
