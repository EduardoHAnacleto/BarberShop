using BarberShop.Application.Common;
using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface IAvailabilityService
{
    /// <summary>
    /// Computes the bookable "HH:mm" start times for a worker on a given day
    /// for a service, taking into account the business schedule (open/close/
    /// break), exceptional closures, the worker's active appointments, the
    /// service duration and the same-day minimum lead time.
    /// </summary>
    Task<Result<AvailabilityResponseDTO>> GetAvailabilityAsync(
        int workerId, DateOnly date, int serviceId);
}
