using System.Net;
using System.Text.Json;
using Microsoft.Playwright;

if (args.Length != 1 || !Uri.TryCreate(args[0], UriKind.Absolute, out var origin)
    || origin.Scheme is not ("http" or "https"))
{
    Console.Error.WriteLine("Usage: dotnet run --project scripts/Smoke -- https://app.hemordna.se");
    return 1;
}

using var http = new HttpClient { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(30) };
using var health = await http.GetAsync("/health");
health.EnsureSuccessStatusCode();

// /health reports every check by name, not just an overall status word. A degraded check still
// answers 200, so the body - not the status code - is what decides whether this passes. Each
// check's description is printed because that is where "which timezone is the server actually
// using" shows up: a server that fell back to UTC reports Degraded here rather than only saying
// so in a log nobody reads.
var report = JsonDocument.Parse(await health.Content.ReadAsStringAsync()).RootElement;
foreach (var check in report.GetProperty("checks").EnumerateArray())
{
    var name = check.GetProperty("name").GetString();
    var status = check.GetProperty("status").GetString();
    var description = check.GetProperty("description").GetString();

    Console.WriteLine($"  health/{name}: {status}{(description is null ? "" : $" ({description})")}");
}

if (report.GetProperty("status").GetString() != "Healthy")
{
    throw new InvalidOperationException(
        $"Health check did not report Healthy: {report.GetRawText()}");
}

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
foreach (var width in new[] { 390, 1280 })
{
    await using var context = await browser.NewContextAsync(new()
    {
        BaseURL = origin.ToString(),
        ViewportSize = new() { Width = width, Height = 900 },
        Locale = "sv-SE"
    });
    var errors = new List<string>();
    var page = await context.NewPageAsync();
    page.PageError += (_, error) => errors.Add(error);
    await page.GotoAsync("/logga-in");
    await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Hemordna", Exact = true }))
        .ToBeVisibleAsync(new() { Timeout = 30_000 });
    await Assertions.Expect(page.GetByLabel("E-post")).ToBeVisibleAsync();
    await page.GetByLabel("E-post").FillAsync($"smoke-unknown-{Guid.NewGuid():N}@example.com");
    await page.GetByLabel("Lösenord").FillAsync("Not-A-Real-Password-2026!");
    var response = await page.RunAndWaitForResponseAsync(
        () => page.GetByRole(AriaRole.Button, new() { Name = "Logga in", Exact = true }).ClickAsync(),
        response => new Uri(response.Url).AbsolutePath == "/api/auth/login");
    if (response.Status != (int)HttpStatusCode.Unauthorized)
    {
        throw new InvalidOperationException($"Anonymous login returned {response.Status}, expected 401.");
    }
    await Assertions.Expect(page.GetByText("E-postadressen eller lösenordet stämmer inte.")).ToBeVisibleAsync();
    if (errors.Count > 0)
    {
        throw new InvalidOperationException("Browser errors: " + string.Join("; ", errors));
    }
    Console.WriteLine($"PASS: client loaded and anonymous login handled at {width}px.");
}
Console.WriteLine("PASS: production health and browser smoke checks.");
return 0;
