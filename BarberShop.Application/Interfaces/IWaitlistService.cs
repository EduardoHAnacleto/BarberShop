using BarberShop.Application.Common;
using BarberShop.Application.DTOs;

namespace BarberShop.Application.Interfaces;

public interface IWaitlistService
{
    Task<Result<WaitlistResponseDTO>> Join(int customerId, WaitlistRequestDTO dto);
    Task<List<WaitlistResponseDTO>> GetMineAsync(int customerId);
    Task<List<WaitlistResponseDTO>> GetAllAsync();

    // customerId is the resolved caller — a customer may only remove their
    // own entry, enforced here rather than trusting the route id alone.
    Task<Result<bool>> Leave(int customerId, int waitlistId);
    Task<Result<bool>> Delete(int id);

    /// <summary>
    /// Emails everyone waiting for this worker on this date who hasn't been
    /// notified yet. Called after a cancellation frees up the day. Returns
    /// the number of emails sent.
    /// </summary>
    Task<int> NotifyWaitlistForAsync(int workerId, DateTime date);
}
