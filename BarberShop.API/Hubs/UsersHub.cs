using Microsoft.AspNetCore.SignalR;

namespace BarberShop.API.Hubs;

public class UsersHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    public async Task NotifyUsersChanged()
    {
        await Clients.All.SendAsync("UsersChanged");
    }

    public async Task NotifyActiveUsersChanged()
    {
        await Clients.All.SendAsync("ActiveUsersChanged");
    }
}
