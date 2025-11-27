using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BattleShip.App;
using BattleShip.App.Services;
using BattleShip.Models.Interfaces;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.Configuration["BackendUrl"] ?? "http://localhost:5224") });

if (builder.Configuration.GetValue<bool>("UseGrpc"))
{
    builder.Services.AddScoped<IGameService>(sp => new GrpcGameClient(builder.Configuration["GrpcBackendUrl"] ?? "http://localhost:5001"));
}
else
{
    builder.Services.AddScoped<IGameService, GameClient>();
}

await builder.Build().RunAsync();
