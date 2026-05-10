using BarberShop.Application.DTOs;
using FluentValidation;

namespace BarberShop.Application.Validators;

public class BusinessScheduleValidator : AbstractValidator<BusinessScheduleDTO>
{
    public BusinessScheduleValidator()
    {
        RuleFor(x => x.DayOfWeek)
            .IsInEnum().WithMessage("DayOfWeek is invalid.");

        When(x => x.IsOpen, () =>
        {
            RuleFor(x => x.OpenTime)
                .NotNull().WithMessage("OpenTime is required when the business is open.");

            RuleFor(x => x.CloseTime)
                .NotNull().WithMessage("CloseTime is required when the business is open.");

            RuleFor(x => x.CloseTime)
                .GreaterThan(x => x.OpenTime)
                .WithMessage("CloseTime must be after OpenTime.")
                .When(x => x.OpenTime.HasValue && x.CloseTime.HasValue);

            When(x => x.BreakStart.HasValue || x.BreakEnd.HasValue, () =>
            {
                RuleFor(x => x.BreakStart)
                    .NotNull().WithMessage("BreakStart is required when BreakEnd is set.");

                RuleFor(x => x.BreakEnd)
                    .NotNull().WithMessage("BreakEnd is required when BreakStart is set.")
                    .GreaterThan(x => x.BreakStart)
                    .WithMessage("BreakEnd must be after BreakStart.")
                    .When(x => x.BreakStart.HasValue && x.BreakEnd.HasValue);

                RuleFor(x => x.BreakStart)
                    .GreaterThanOrEqualTo(x => x.OpenTime)
                    .WithMessage("BreakStart must be within business hours.")
                    .When(x => x.BreakStart.HasValue && x.OpenTime.HasValue);

                RuleFor(x => x.BreakEnd)
                    .LessThanOrEqualTo(x => x.CloseTime)
                    .WithMessage("BreakEnd must be within business hours.")
                    .When(x => x.BreakEnd.HasValue && x.CloseTime.HasValue);
            });
        });
    }
}
