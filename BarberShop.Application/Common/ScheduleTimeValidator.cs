namespace BarberShop.Application.Common;

// Shared validation for the open/close/break time fields that BusinessSchedule
// (shop-wide day) and WorkerSchedule (per-worker override) both carry. Returns
// an error message, or null when the combination is valid.
//
// A closed day is always valid — its time fields are ignored by the
// availability computation, so whatever they hold is inert. An open day, on
// the other hand, must be internally coherent: an inverted or half-specified
// break would otherwise be accepted and then silently dropped by the slot
// overlap check (an interval with From > Until never overlaps anything),
// leaving the admin believing a break exists that the booking flow ignores.
public static class ScheduleTimeValidator
{
    public static string? Validate(
        bool isOpen,
        TimeSpan? openTime,
        TimeSpan? closeTime,
        TimeSpan? breakStart,
        TimeSpan? breakEnd)
    {
        if (!isOpen) return null;

        if (!openTime.HasValue || !closeTime.HasValue)
            return "OpenTime and CloseTime are required when the schedule is open.";

        if (closeTime <= openTime)
            return "CloseTime must be after OpenTime.";

        if (breakStart.HasValue != breakEnd.HasValue)
            return "BreakStart and BreakEnd must be provided together.";

        if (breakStart.HasValue && breakEnd.HasValue)
        {
            if (breakEnd <= breakStart)
                return "BreakEnd must be after BreakStart.";

            if (breakStart < openTime || breakEnd > closeTime)
                return "The break must be within the open hours.";
        }

        return null;
    }
}
