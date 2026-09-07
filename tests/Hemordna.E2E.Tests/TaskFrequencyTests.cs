using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class TaskFrequencyTests
{
    private readonly HemordnaAppFixture _app;

    public TaskFrequencyTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Changing_a_tasks_frequency_updates_what_is_shown_without_recreating_it()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Tova");

        await page.GotoAsync("/omraden");
        await page.GetByText("Lägg till ett tomt område i stället").ClickAsync();
        await page.GetByLabel("Nytt område").FillAsync("Kök");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till område" }).ClickAsync();

        var kitchenCard = page.Locator(".card")
            .Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = "Kök", Exact = true }) });
        await kitchenCard.WaitForAsync();

        await kitchenCard.GetByText("Lägg till en uppgift i Kök").ClickAsync();
        await kitchenCard.GetByLabel("Namn").FillAsync("Diska");
        // Default estimate is fine - only the recurrence choice matters for this test.
        await kitchenCard.GetByLabel("Upprepning").SelectOptionAsync("Daily");
        await kitchenCard.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        // Scoped by its own "Ändra frekvens" button, not by name alone: once opened, the
        // edit form's own row also contains "Diska" (in its label), which a plain HasText
        // match would ambiguously catch too.
        var taskRow = kitchenCard.Locator(".list-item:has(button[aria-label=\"Ändra frekvens för Diska\"])");
        await Assertions.Expect(taskRow).ToContainTextAsync("varje dag");

        await taskRow.GetByRole(AriaRole.Button, new() { Name = "Ändra frekvens för Diska" }).ClickAsync();
        await kitchenCard.GetByLabel("Ny upprepning för \"Diska\"").SelectOptionAsync("Weekly");
        await kitchenCard.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();

        await Assertions.Expect(taskRow).ToContainTextAsync("varje vecka");
        await Assertions.Expect(taskRow).Not.ToContainTextAsync("varje dag");
    }
}
