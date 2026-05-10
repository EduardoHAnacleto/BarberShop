using BarberShop.Application.DTOs;
using FluentValidation;

namespace BarberShop.Application.Validators;

public class ServiceValidator : AbstractValidator<ServiceDTO>
{
    public ServiceValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MinimumLength(3).WithMessage("Name must be at least 3 characters.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Duration)
            .GreaterThan(0).WithMessage("Duration must be greater than 0.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
