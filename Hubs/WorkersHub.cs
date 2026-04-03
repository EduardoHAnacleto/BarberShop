using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Hubs;

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

    public async Task NotifyPromotionsChanged()
    {
        await Clients.All.SendAsync("WorkersChanged");
    }

    public async Task NotifyActivePromotionsChanged()
    {
        await Clients.All.SendAsync("ActiveWorkersChanged");
    }
}
