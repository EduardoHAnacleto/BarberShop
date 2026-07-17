using Microsoft.AspNetCore.SignalR;

namespace BarberShop.API.Hubs;

public class WorkerSchedulesHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
