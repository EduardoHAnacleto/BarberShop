using BarberShop.Application.DTOs;
using FluentValidation;

namespace BarberShop.Application.Validators;

public class CustomerValidator : AbstractValidator<CustomerDTO>
{
    public CustomerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email is invalid.")
            .MaximumLength(200).WithMessage("Email must not exceed 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("PhoneNumber must not exceed 20 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.UtcNow).WithMessage("DateOfBirth must be in the past.")
            .When(x => x.DateOfBirth.HasValue && x.DateOfBirth != DateTime.MinValue);
    }
}
