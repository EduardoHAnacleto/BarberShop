using BarberShop.Application.DTOs;
using FluentValidation;

namespace BarberShop.Application.Validators;

public class UserRequestValidator : AbstractValidator<UserRequestDTO>
{
    public UserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is invalid.")
            .MaximumLength(200).WithMessage("Email must not exceed 200 characters.");

        RuleFor(x => x.PasswordHash)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");
    }
}
