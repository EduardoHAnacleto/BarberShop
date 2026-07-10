namespace BarberShop.Application.Interfaces;

public interface IAppointmentReminderService
{
    /// <summary>
    /// Sends every 24h/1h reminder currently due and marks each appointment so
    /// it is never reminded twice. Returns the number of emails sent.
    /// </summary>
    Task<int> SendDueRemindersAsync();
}
