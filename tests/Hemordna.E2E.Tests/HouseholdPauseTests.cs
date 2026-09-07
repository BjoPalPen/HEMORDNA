using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class HouseholdPauseTests
{
    private readonly HemordnaAppFixture _app;

    public HouseholdPauseTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Pausing_a_member_shows_a_pause_chip_next_to_their_name()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elin");

        await page.GotoAsync("/hushall");
        await page.GetByLabel("Namn").FillAsync("Sven");
        await page.Locator("form").GetByRole(AriaRole.Button, new() { Name = "Vuxen, jobbar heltid" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till medlem" }).ClickAsync();

        // Scoped past the button's own aria-label: the row and its own conditionally-rendered
        // edit form both mention "Sven" in their text, so a plain HasText match is ambiguous -
        // see TaskFrequencyTests for the same issue with "Ändra frekvens".
        var svenRow = page.Locator(".list-item:has(button[aria-label=\"Ta bort Sven\"])");
        await Assertions.Expect(svenRow).ToBeVisibleAsync();

        await svenRow.GetByRole(AriaRole.Button, new() { Name = "Pausa Sven" }).ClickAsync();
        await page.GetByLabel("Pausa \"Sven\" till och med").FillAsync("2026-12-24");
        await page.GetByRole(AriaRole.Button, new() { Name = "Pausa", Exact = true }).ClickAsync();

        await Assertions.Expect(svenRow).ToContainTextAsync("Pausad t.o.m.");
    }

    [Fact]
    public async Task Pausing_the_whole_household_shows_a_status_notice()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Karin");

        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Pausa hela hushållet" }).ScrollIntoViewIfNeededAsync();
        await page.GetByLabel("Pausa till och med").FillAsync("2026-12-24");
        await page.GetByRole(AriaRole.Button, new() { Name = "Pausa hushållet" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Hushållet är pausat t.o.m.")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Återuppta nu" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Hushållet är pausat t.o.m.")).Not.ToBeVisibleAsync();
    }
}
