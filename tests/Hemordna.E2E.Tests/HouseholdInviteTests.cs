using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class HouseholdInviteTests
{
    private readonly HemordnaAppFixture _app;

    public HouseholdInviteTests(HemordnaAppFixture app) => _app = app;

    private static async Task<string> ReadInviteCodeAsync(IPage page)
    {
        await page.GotoAsync("/hushall");
        var code = page.GetByLabel("Inbjudningskod");
        await code.WaitForAsync();
        return await code.InnerTextAsync();
    }

    [Fact]
    public async Task Joining_with_a_valid_code_adds_the_new_member_to_the_same_household()
    {
        var ownerPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(ownerPage, "Cecilia", "Familjen Cecilia");
        var inviteCode = await ReadInviteCodeAsync(ownerPage);

        var joinerPage = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(joinerPage, "David");
        await joinerPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await joinerPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await joinerPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();

        await joinerPage.GetByRole(AriaRole.Heading, new() { Name = "Hej David!" }).WaitForAsync(new() { Timeout = 15_000 });

        // Both now see the same household - the owner's own name for it, not a new one.
        await joinerPage.GotoAsync("/hushall");
        await Assertions.Expect(joinerPage.GetByRole(AriaRole.Heading, new() { Name = "Familjen Cecilia" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(joinerPage.Locator(".list-item", new() { HasText = "David" })).ToBeVisibleAsync();

        await ownerPage.ReloadAsync();
        await Assertions.Expect(ownerPage.Locator(".list-item", new() { HasText = "David" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task An_unknown_code_shows_an_error_instead_of_joining()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(page, "Elin");

        await page.GetByText("Har du en inbjudningskod?").ClickAsync();
        await page.GetByLabel("Inbjudningskod").FillAsync("NOSUCH1");
        await page.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Ingen hittade den koden.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Regenerating_the_code_makes_the_old_one_stop_working()
    {
        var ownerPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(ownerPage, "Fredrik");
        var oldCode = await ReadInviteCodeAsync(ownerPage);

        await ownerPage.GetByRole(AriaRole.Button, new() { Name = "Skapa ny kod" }).ClickAsync();
        await Assertions.Expect(ownerPage.GetByText("Ny kod skapad")).ToBeVisibleAsync();
        var newCode = await ownerPage.GetByLabel("Inbjudningskod").InnerTextAsync();
        Assert.NotEqual(oldCode, newCode);

        var joinerPage = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(joinerPage, "Greta");
        await joinerPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await joinerPage.GetByLabel("Inbjudningskod").FillAsync(oldCode);
        await joinerPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();

        await Assertions.Expect(joinerPage.GetByText("Ingen hittade den koden.")).ToBeVisibleAsync();
    }
}
