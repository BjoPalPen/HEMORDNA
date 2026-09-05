using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

public class ProdDiagTests
{
    [Fact]
    public async Task Diagnose_production()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            // Local DNS has not caught up with the new A record yet, but public resolvers
            // already see it - force Chromium to resolve the domain to the known IP instead
            // of waiting on this machine's resolver.
            Args = ["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu",
                "--host-resolver-rules=MAP app.hemordna.se 62.238.45.45"]
        });
        var page = await browser.NewPageAsync();

        page.Console += (_, msg) => Console.WriteLine($"CONSOLE[{msg.Type}]: {msg.Text}");
        page.PageError += (_, err) => Console.WriteLine($"PAGEERROR: {err}");
        page.RequestFailed += (_, req) => Console.WriteLine($"REQFAILED: {req.Url} {req.Failure}");
        page.Response += (_, resp) =>
        {
            if (!resp.Ok)
            {
                Console.WriteLine($"BADRESPONSE: {resp.Status} {resp.Url}");
            }
        };

        await page.GotoAsync("https://app.hemordna.se");
        await page.WaitForTimeoutAsync(8000);

        Console.WriteLine("URL now: " + page.Url);
    }
}
