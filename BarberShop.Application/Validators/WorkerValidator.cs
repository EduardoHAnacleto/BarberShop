using BarberShop.Application.DTOs;
using FluentValidation;

namespace BarberShop.Application.Validators;

public class WorkerValidator : AbstractValidator<WorkerDTO>
{
    public WorkerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MinimumLength(10).WithMessage("Name must be at least 10 characters.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.WagePerHour)
            .GreaterThan(0).WithMessage("WagePerHour must be greater than 0.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is invalid.")
            .MaximumLength(200).WithMessage("Email must not exceed 200 characters.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("PhoneNumber must not exceed 20 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.Address)
            .MaximumLength(200).WithMessage("Address must not exceed 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));

        RuleFor(x => x.Position)
            .MaximumLength(100).WithMessage("Position must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Position));

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.UtcNow).WithMessage("DateOfBirth must be in the past.");
    }
}
