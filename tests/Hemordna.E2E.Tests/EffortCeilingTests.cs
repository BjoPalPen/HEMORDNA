using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>Ork per person och veckodag - see docs/ARCHITECTURE.md "Beslut: Ork per person och
/// veckodag".</summary>
[Collection(HemordnaAppCollection.Name)]
public class EffortCeilingTests
{
    private readonly HemordnaAppFixture _app;

    public EffortCeilingTests(HemordnaAppFixture app) => _app = app;

    /// <summary>Scopes to one weekday's own row inside the "orkar" disclosure (role="group"
    /// aria-label="Ork Måndag" etc. - see MemberSheet.razor - "Ork " avoids colliding with the
    /// existing "Anpassa tid per veckodag" section's own plain "Måndag" input label), so the
    /// button click lands on that day's level-picker and not one of the other six.</summary>
    private static ILocator DayRow(ILocator sheet, string weekdayLabel)
        => sheet.GetByRole(AriaRole.Group, new() { Name = $"Ork {weekdayLabel}", Exact = true });

    [Fact]
    public async Task Setting_a_days_effort_ceiling_is_saved_and_survives_a_reload()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Noomi");

        await page.GotoAsync("/hushall");
        var sheet = await HushallHelper.OpenMemberSheetAsync(page, "Noomi");

        await sheet.GetByText("Hur mycket orkar personen per veckodag").ClickAsync();

        // Måndag starts at Tung (Heavy) - the default, "ingen begränsning" - see
        // docs/ARCHITECTURE.md.
        await Assertions.Expect(DayRow(sheet, "Måndag").GetByRole(AriaRole.Button, new() { Name = "Tung" }))
            .ToHaveClassAsync(new Regex("btn-primary"));

        await DayRow(sheet, "Måndag").GetByRole(AriaRole.Button, new() { Name = "Lätt" }).ClickAsync();
        var saveButton = sheet.Locator("details", new() { HasText = "Hur mycket orkar personen per veckodag" })
            .GetByRole(AriaRole.Button, new() { Name = "Spara" });
        await saveButton.ClickAsync();
        // Wait for the save's own round trip to finish (button text back to "Spara", not
        // "Sparar...") before reloading, or the reload can race the still-in-flight PUT.
        await Assertions.Expect(saveButton).ToHaveTextAsync("Spara");

        // A real reload, not just re-opening the same component instance, to prove it round-
        // tripped through the API.
        await page.ReloadAsync();
        var reopenedSheet = await HushallHelper.OpenMemberSheetAsync(page, "Noomi");
        await reopenedSheet.GetByText("Hur mycket orkar personen per veckodag").ClickAsync();

        await Assertions.Expect(DayRow(reopenedSheet, "Måndag").GetByRole(AriaRole.Button, new() { Name = "Lätt" }))
            .ToHaveClassAsync(new Regex("btn-primary"));
        // Other weekdays are untouched by a single day's edit.
        await Assertions.Expect(DayRow(reopenedSheet, "Tisdag").GetByRole(AriaRole.Button, new() { Name = "Tung" }))
            .ToHaveClassAsync(new Regex("btn-primary"));
    }

    [Fact]
    public async Task Picking_a_role_sets_a_starting_effort_ceiling_that_stays_freely_editable()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Otto");

        await page.GotoAsync("/hushall");
        var sheet = await HushallHelper.OpenMemberSheetAsync(page, "Otto");

        var adultButton = sheet.GetByRole(AriaRole.Button, new() { Name = "Vuxen, jobbar heltid" });
        await adultButton.ClickAsync();
        // The click starts three concurrent API calls (role, budget, ceiling) and a household
        // reload - wait for that whole round trip to finish (the buttons re-enable) before
        // reading anything back, or the assertions below race the still-in-flight request.
        await Assertions.Expect(adultButton).ToBeEnabledAsync();

        // A real reload - not just re-opening the same component instance - proves the preset's
        // effort ceiling actually round-tripped through the API rather than only ever existing
        // in the component's own in-memory state.
        await page.ReloadAsync();
        var reopenedSheet = await HushallHelper.OpenMemberSheetAsync(page, "Otto");
        await reopenedSheet.GetByText("Hur mycket orkar personen per veckodag").ClickAsync();

        // AdultFullTime -> Lätt mån-fre, Tung lör-sön - se HouseholdRolePresets.EffortCeilingFor.
        await Assertions.Expect(DayRow(reopenedSheet, "Onsdag").GetByRole(AriaRole.Button, new() { Name = "Lätt" }))
            .ToHaveClassAsync(new Regex("btn-primary"));
        await Assertions.Expect(DayRow(reopenedSheet, "Lördag").GetByRole(AriaRole.Button, new() { Name = "Tung" }))
            .ToHaveClassAsync(new Regex("btn-primary"));

        // Still freely editable afterwards - the preset is only a starting point.
        await DayRow(reopenedSheet, "Onsdag").GetByRole(AriaRole.Button, new() { Name = "Tung" }).ClickAsync();
        var saveButton = reopenedSheet.Locator("details", new() { HasText = "Hur mycket orkar personen per veckodag" })
            .GetByRole(AriaRole.Button, new() { Name = "Spara" });
        await saveButton.ClickAsync();
        await Assertions.Expect(saveButton).ToHaveTextAsync("Spara");

        await Assertions.Expect(DayRow(reopenedSheet, "Onsdag").GetByRole(AriaRole.Button, new() { Name = "Tung" }))
            .ToHaveClassAsync(new Regex("btn-primary"));
    }
}
