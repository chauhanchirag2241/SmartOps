using FluentValidation;
using SmartOps.Application.Modules.Identity;

namespace SmartOps.Application.Modules.Identity.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email or mobile number is required.")
            .Must(BeValidLoginIdentifier).WithMessage("Enter a valid email or 10-digit mobile number.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");
    }

    private static bool BeValidLoginIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Contains('@'))
        {
            return trimmed.Contains('.') && trimmed.Length >= 5;
        }

        string digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return digits.Length is >= 10 and <= 15;
    }
}
