using BarberShop.Application.DTOs;
using FluentValidation;

namespace BarberShop.Application.Validators;

public class AppointmentRequestValidator : AbstractValidator<AppointmentRequestDTO>
{
    public AppointmentRequestValidator()
    {
        RuleFor(x => x.WorkerId)
            .GreaterThan(0).WithMessage("WorkerId must be greater than 0.");

        RuleFor(x => x.CustomerId)
            .GreaterThan(0).WithMessage("CustomerId must be greater than 0.");

        RuleFor(x => x.ServiceId)
            .GreaterThan(0).WithMessage("ServiceId must be greater than 0.");

        RuleFor(x => x.ScheduledFor)
            .GreaterThan(DateTime.UtcNow).WithMessage("ScheduledFor must be a future date.");

        RuleFor(x => x.ExtraDetails)
            .MaximumLength(500).WithMessage("ExtraDetails must not exceed 500 characters.");
    }
}
