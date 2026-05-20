using FluentValidation;
using SmartOps.Application.Modules.Identity;

namespace SmartOps.Application.Modules.Identity.Validators;

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequestDto>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
