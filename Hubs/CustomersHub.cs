using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Hubs;

public class CustomersHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public async Task NotifyCustomersChanged()
    {
        await Clients.All.SendAsync("CustomersChanged");
    }

    public async Task NotifyActiveCustomersChanged()
    {
        await Clients.All.SendAsync("ActiveCustomersChanged");
    }
}
