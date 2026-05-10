using BarberShop.Application.DTOs;
using FluentValidation;

namespace BarberShop.Application.Validators;

public class LoginValidator : AbstractValidator<LoginDTO>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is invalid.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");
    }
}
