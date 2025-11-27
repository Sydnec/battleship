using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BattleShip.App;
using BattleShip.App.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configuration du HttpClient pour l'API REST
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri("http://localhost:5224") 
});

// Enregistrer les services
builder.Services.AddSingleton<GameState>();

// ===== CHOISIR L'IMPLÉMENTATION =====
// Option 1: REST (HTTP/JSON)
builder.Services.AddScoped<IBattleShipService, BattleShipRestService>();

// Option 2: gRPC (gRPC-Web) - Décommenter pour utiliser gRPC
// builder.Services.AddScoped<IBattleShipService, BattleShipGrpcService>();

await builder.Build().RunAsync();
