namespace BarberShop.Application.Interfaces;

/// <summary>
/// Resource-level authorization for appointment data. Admins have full access;
/// clients are scoped to their own customer record and workers to their own
/// worker record. Prevents the IDOR where any authenticated user could read or
/// mutate another person's appointments.
/// </summary>
public interface IAppointmentAccessService
{
    Task<bool> CanViewCustomerAsync(int callerUserId, bool isAdmin, int customerId);
    Task<bool> CanViewWorkerAsync(int callerUserId, bool isAdmin, int workerId);
    Task<bool> CanMutateAsync(int callerUserId, bool isAdmin, IEnumerable<int> appointmentIds);
}
