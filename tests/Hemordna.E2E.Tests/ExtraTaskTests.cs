using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class ExtraTaskTests
{
    private readonly HemordnaAppFixture _app;

    public ExtraTaskTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Adding_an_extra_task_puts_it_on_todays_list_immediately()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elin");

        // A fresh household's creator starts at zero minutes a day (see CreateHousehold), so
        // this only lands on today's list - rather than silently under "till en annan dag" -
        // if AddExtraTaskAsync's availability bump actually worked.
        await page.GetByText("Lägg till en extra uppgift").ClickAsync();
        await page.GetByLabel("Namn").FillAsync("Rensa garderoben");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lagom tid" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till för i dag" }).ClickAsync();

        // Only a task on today's actual (not "till en annan dag") list gets this button.
        var completeButton = page.GetByRole(AriaRole.Button, new() { Name = "Markera Rensa garderoben som klar" });
        await Assertions.Expect(completeButton).ToBeVisibleAsync();

        await completeButton.ClickAsync();
        var row = page.Locator(".task-done", new() { HasText = "Rensa garderoben" });
        await Assertions.Expect(row).ToBeVisibleAsync();
    }
}
