using BarberShop.Application.Interfaces;

namespace BarberShop.Application.Services;

public class AppointmentAccessService : IAppointmentAccessService
{
    private readonly IUnitOfWork _uow;

    public AppointmentAccessService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<bool> CanViewCustomerAsync(int callerUserId, bool isAdmin, int customerId)
    {
        if (isAdmin) return true;

        var user = await _uow.Users.GetByIdAsync(callerUserId);
        return user?.CustomerId == customerId;
    }

    public async Task<bool> CanViewWorkerAsync(int callerUserId, bool isAdmin, int workerId)
    {
        if (isAdmin) return true;

        var user = await _uow.Users.GetByIdAsync(callerUserId);
        return user?.WorkerId == workerId;
    }

    public async Task<bool> CanMutateAsync(
        int callerUserId, bool isAdmin, IEnumerable<int> appointmentIds)
    {
        if (isAdmin) return true;

        var user = await _uow.Users.GetByIdAsync(callerUserId);
        if (user == null) return false;

        // A caller may act on an appointment only if they own it: clients as
        // the appointment's customer, workers as the assigned worker. The whole
        // batch must be owned — a single foreign id denies the request.
        foreach (var id in appointmentIds)
        {
            var appointment = await _uow.Appointments.GetByIdAsync(id);
            if (appointment == null)
                return false;

            var ownsAsCustomer = user.CustomerId.HasValue
                && appointment.CustomerId == user.CustomerId.Value;
            var ownsAsWorker = user.WorkerId.HasValue
                && appointment.WorkerId == user.WorkerId.Value;

            if (!ownsAsCustomer && !ownsAsWorker)
                return false;
        }

        return true;
    }
}
