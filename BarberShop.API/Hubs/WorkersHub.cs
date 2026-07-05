using Microsoft.AspNetCore.SignalR;

namespace BarberShop.API.Hubs;

public class WorkersHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public async Task NotifyWorkersChanged()
    {
        await Clients.All.SendAsync("WorkersChanged");
    }

    public async Task NotifyActiveWorkersChanged()
    {
        await Clients.All.SendAsync("ActiveWorkersChanged");
    }
}
