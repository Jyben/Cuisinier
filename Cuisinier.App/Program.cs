using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Cuisinier.App;
using Cuisinier.App.Services;
using Cuisinier.App.Components;
using Cuisinier.App.Middleware;
using MudBlazor.Services;
using Refit;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API URL configuration (will be configured via Aspire or appsettings.json)
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

// Blazored LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Authentication services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider, AuthStateProvider>();
builder.Services.AddAuthorizationCore();

// Auth API client
builder.Services.AddScoped<AuthHeaderHandler>();
builder.Services.AddRefitClient<IAuthApi>()
    .ConfigureHttpClient((sp, c) => 
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var url = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
        c.BaseAddress = new Uri(url);
    });

// Refit client configuration with auth handler
builder.Services.AddRefitClient<IMenuApi>()
    .AddHttpMessageHandler<AuthHeaderHandler>()
    .ConfigureHttpClient((sp, c) => 
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var url = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
        c.BaseAddress = new Uri(url);
    });

builder.Services.AddRefitClient<IRecipeApi>()
    .AddHttpMessageHandler<AuthHeaderHandler>()
    .ConfigureHttpClient((sp, c) => 
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var url = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
        c.BaseAddress = new Uri(url);
    });

builder.Services.AddRefitClient<IShoppingListApi>()
    .AddHttpMessageHandler<AuthHeaderHandler>()
    .ConfigureHttpClient((sp, c) => 
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var url = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
        c.BaseAddress = new Uri(url);
    });

builder.Services.AddRefitClient<IFavoriteApi>()
    .AddHttpMessageHandler<AuthHeaderHandler>()
    .ConfigureHttpClient((sp, c) => 
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var url = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
        c.BaseAddress = new Uri(url);
    });

builder.Services.AddRefitClient<IDishApi>()
    .AddHttpMessageHandler<AuthHeaderHandler>()
    .ConfigureHttpClient((sp, c) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var url = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
        c.BaseAddress = new Uri(url);
    });

builder.Services.AddRefitClient<IFamilyApi>()
    .AddHttpMessageHandler<AuthHeaderHandler>()
    .ConfigureHttpClient((sp, c) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var url = config["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
        c.BaseAddress = new Uri(url);
    });

// MudBlazor
builder.Services.AddMudServices();

await builder.Build().RunAsync();
