using BarberShop.Application.Common;
using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface IWorkerScheduleService
{
    // Only the override rows that exist for this worker — callers merge with
    // the shop's BusinessSchedule for any day missing here.
    Task<List<WorkerScheduleDTO>> GetByWorkerAsync(int workerId);

    // Creates or replaces the worker's override for a single weekday.
    Task<Result<WorkerScheduleDTO>> UpsertAsync(int workerId, DayOfWeek day, WorkerScheduleDTO dto);

    // Deletes the override, reverting that weekday back to the shop default.
    Task<Result<bool>> RemoveOverrideAsync(int workerId, DayOfWeek day);
}
