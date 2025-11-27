using BattleShip.Models.DTOs;
using BattleShip.Models.Entities;
using FluentValidation;

namespace BattleShip.API.Validators;

public class ShootRequestValidator : AbstractValidator<ShootRequest>
{
    public ShootRequestValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.PlayerId).NotEmpty();
        RuleFor(x => x.Row).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Column).GreaterThanOrEqualTo(0);
    }
}
