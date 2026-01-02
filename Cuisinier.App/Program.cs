using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Cuisinier.App;
using Cuisinier.App.Services;
using MudBlazor.Services;
using Refit;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API URL configuration (will be configured via Aspire or appsettings.json)
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

// Refit client configuration
builder.Services.AddRefitClient<IMenuApi>()
    .ConfigureHttpClient((sp, c) => 
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var url = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
        c.BaseAddress = new Uri(url);
    });

builder.Services.AddRefitClient<IRecipeApi>()
    .ConfigureHttpClient((sp, c) => 
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var url = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
        c.BaseAddress = new Uri(url);
    });

builder.Services.AddRefitClient<IShoppingListApi>()
    .ConfigureHttpClient((sp, c) => 
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var url = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
        c.BaseAddress = new Uri(url);
    });

// MudBlazor
builder.Services.AddMudServices();

await builder.Build().RunAsync();
