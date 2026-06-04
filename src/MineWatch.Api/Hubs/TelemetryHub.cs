using Microsoft.AspNetCore.SignalR;

namespace MineWatch.Api.Hubs;

public class TelemetryHub : Hub
{
    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }

    public async Task SubscribeVehicle(string vehicleNo)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"vehicle-{vehicleNo}");
    }

    public async Task UnsubscribeVehicle(string vehicleNo)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"vehicle-{vehicleNo}");
    }
}
