using FluentValidation;
using BattleShip.API.DTOs;
using BattleShip.API.Services;

namespace BattleShip.API.Validators;

public class AttackRequestValidator : AbstractValidator<AttackRequest>
{
    private readonly GameService _gameService;

    public AttackRequestValidator(GameService gameService)
    {
        _gameService = gameService;

        // Validation du GameId
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("L'identifiant de la partie est requis")
            .NotEqual(Guid.Empty)
            .WithMessage("L'identifiant de la partie ne peut pas être vide")
            .Must(GameExists)
            .WithMessage("La partie spécifiée n'existe pas");

        // Validation intelligente de Row basée sur la taille réelle de la grille
        RuleFor(x => x)
            .Must(request => IsValidRow(request.GameId, request.Row))
            .WithMessage(request => 
            {
                var game = _gameService.GetGame(request.GameId);
                if (game == null) return "Impossible de valider la ligne : partie introuvable";
                return $"La ligne doit être entre 0 et {game.Settings.GridSize - 1}";
            })
            .When(x => GameExists(x.GameId)); // Ne valider que si le jeu existe

        // Validation intelligente de Column basée sur la taille réelle de la grille
        RuleFor(x => x)
            .Must(request => IsValidColumn(request.GameId, request.Column))
            .WithMessage(request => 
            {
                var game = _gameService.GetGame(request.GameId);
                if (game == null) return "Impossible de valider la colonne : partie introuvable";
                return $"La colonne doit être entre 0 et {game.Settings.GridSize - 1}";
            })
            .When(x => GameExists(x.GameId)); // Ne valider que si le jeu existe

        // Validation que la case n'a pas déjà été attaquée
        RuleFor(x => x)
            .Must(request => !IsCellAlreadyAttacked(request.GameId, request.Row, request.Column))
            .WithMessage(request => $"La case ({request.Row}, {request.Column}) a déjà été attaquée")
            .When(x => GameExists(x.GameId) && 
                      IsValidRow(x.GameId, x.Row) && 
                      IsValidColumn(x.GameId, x.Column)); // Ne valider que si le jeu existe et les coordonnées sont valides
    }

    private bool GameExists(Guid gameId)
    {
        return _gameService.GetGame(gameId) != null;
    }

    private bool IsValidRow(Guid gameId, int row)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null) return false;

        return row >= 0 && row < game.Settings.GridSize;
    }

    private bool IsValidColumn(Guid gameId, int column)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null) return false;

        return column >= 0 && column < game.Settings.GridSize;
    }

    private bool IsCellAlreadyAttacked(Guid gameId, int row, int column)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null) return false;

        // Vérifier dans la grille de l'ordinateur (celle que le joueur attaque)
        char cell = game.ComputerBoard.Grid[row, column];
        
        // Si la case contient 'X' (touché) ou 'O' (raté), elle a déjà été attaquée
        return cell == 'X' || cell == 'O';
    }
}
