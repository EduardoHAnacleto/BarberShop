namespace BarberShop.Application.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync(string channel, string eventName);
}
