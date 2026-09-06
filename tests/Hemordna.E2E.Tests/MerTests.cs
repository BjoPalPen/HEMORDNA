using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class MerTests
{
    private readonly HemordnaAppFixture _app;

    public MerTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Signing_out_clears_the_session_and_a_reload_asks_to_sign_in_again()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Utloggad");

        await page.GotoAsync("/mer");
        await page.GetByRole(AriaRole.Button, new() { Name = "Logga ut" }).ClickAsync();

        await page.GetByRole(AriaRole.Tab, new() { Name = "Logga in" }).WaitForAsync();
        Assert.Contains("logga-in", page.Url);

        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Logga in" }).WaitForAsync();
    }
}
