using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Hubs;

public class ServicesHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public async Task NotifyServicesChanged()
    {
        await Clients.All.SendAsync("ServicesChanged");
    }

    public async Task NotifyActiveServicesChanged()
    {
        await Clients.All.SendAsync("ActiveServicesChanged");
    }
}
