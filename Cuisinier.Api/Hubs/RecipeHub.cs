using Microsoft.AspNetCore.SignalR;

namespace Cuisinier.Api.Hubs;

public class RecipeHub : Hub
{
    public async Task JoinMenuGroup(int menuId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"menu-{menuId}");
    }
}

