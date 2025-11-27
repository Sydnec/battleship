namespace Battleship.Core.Enums
{
    public enum AILevel
    {
        Easy,
        Medium,
        Hard
    }

    public enum GameState
    {   
        Playing,   
        GameOver
    }

    public enum ShotResult
    {
        Miss,      // Raté (O)
        Hit,       // Touché (X)
        Sunk       // Coulé (X + bateau détruit)
    }

    public enum Orientation
    {
        Horizontal,
        Vertical
    }
}