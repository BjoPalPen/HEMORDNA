using System.Net;
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
if ((await health.Content.ReadAsStringAsync()).Trim() != "Healthy")
{
    throw new InvalidOperationException("Database health check did not report Healthy.");
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
