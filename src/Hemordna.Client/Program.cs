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

// In local dev the API lives on its own origin (its own dotnet-run process/port), so
// appsettings.json names it explicitly - overridden per machine by the gitignored
// appsettings.Development.json for LAN testing. In production the API is served from the
// same origin as this client (see Hemordna.Api's UseStaticFiles/MapFallbackToFile), so
// appsettings.Production.json blanks the setting and this falls back to the page's own
// origin instead - never a value hardcoded for someone else's machine.
var configuredApiBaseAddress = builder.Configuration["ApiBaseAddress"];
var apiBaseAddress = string.IsNullOrWhiteSpace(configuredApiBaseAddress)
    ? builder.HostEnvironment.BaseAddress
    : configuredApiBaseAddress;

// TimeProvider rather than DateTime.Now, so "today" enters the UI through one
// replaceable seam instead of being read from a static clock inside a component.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<HemordnaApiClient>();
builder.Services.AddScoped<HemordnaSession>();
builder.Services.AddScoped<WebAuthnClient>();
builder.Services.AddScoped(sp => new HouseholdRealtimeClient(apiBaseAddress, sp.GetRequiredService<TokenStore>()));

await builder.Build().RunAsync();
