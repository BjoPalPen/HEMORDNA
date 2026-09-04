using Hemordna.Client;
using Hemordna.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API lives on its own origin. The address comes from configuration so the client can be
// pointed at a deployed API without a rebuild.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "http://localhost:5199/";

// TimeProvider rather than DateTime.Now, so "today" enters the UI through one
// replaceable seam instead of being read from a static clock inside a component.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<HemordnaApiClient>();
builder.Services.AddScoped<HemordnaSession>();

await builder.Build().RunAsync();
