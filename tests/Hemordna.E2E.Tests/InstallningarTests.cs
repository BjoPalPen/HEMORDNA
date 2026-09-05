using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class InstallningarTests
{
    private readonly HemordnaAppFixture _app;

    public InstallningarTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Changing_the_presentation_and_motivation_persists_across_a_reload()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ingrid");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        await page.GetByLabel("Stor text - större och tydligare").CheckAsync();
        await page.GetByLabel("Lugn - en vänlig kommentar då och då").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();

        // Wait for the save to actually finish (the button reads "Sparar..." while in flight)
        // before reloading, or the reload can race the still-in-flight PUT.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Spara" })).ToBeEnabledAsync();

        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        await Assertions.Expect(page.GetByLabel("Stor text - större och tydligare")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByLabel("Lugn - en vänlig kommentar då och då")).ToBeCheckedAsync();
    }
}
