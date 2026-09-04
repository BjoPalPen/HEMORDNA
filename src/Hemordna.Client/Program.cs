using System.Globalization;
using Hemordna.Client;
using Hemordna.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// Hemordna is Swedish, so dates and numbers are Swedish no matter what the browser is set to.
// Without this the app inherits the browser's culture and renders "Friday 4 September" to
// anyone whose system is not Swedish.
var swedish = new CultureInfo("sv-SE");
CultureInfo.DefaultThreadCurrentCulture = swedish;
CultureInfo.DefaultThreadCurrentUICulture = swedish;

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
builder.Services.AddScoped(sp => new HouseholdRealtimeClient(apiBaseAddress, sp.GetRequiredService<TokenStore>()));

await builder.Build().RunAsync();
