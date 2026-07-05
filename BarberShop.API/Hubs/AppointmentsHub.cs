using Microsoft.AspNetCore.SignalR;

namespace BarberShop.API.Hubs;

public class AppointmentsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public async Task NotifyAppointmentsChanged()
    {
        await Clients.All.SendAsync("AppointmentsChanged");
    }

    public async Task NotifyActiveAppointmentsChanged()
    {
        await Clients.All.SendAsync("ActiveAppointmentsChanged");
    }
}
