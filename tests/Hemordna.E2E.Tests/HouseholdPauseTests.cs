using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class HouseholdPauseTests
{
    private readonly HemordnaAppFixture _app;

    public HouseholdPauseTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Pausing_a_member_is_reflected_when_reopening_their_sheet()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elin");

        await page.GotoAsync("/hushall");
        await HushallHelper.AddMemberWithoutAccountAsync(page, "Sven", "Vuxen, jobbar heltid");

        // Per-member pause now lives in the member's own sheet, alongside their role - see
        // MemberSheet.razor.
        var sheet = await HushallHelper.OpenMemberSheetAsync(page, "Sven");
        await sheet.GetByLabel("Pausa till och med").FillAsync("2026-12-24");
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Pausa", Exact = true }).ClickAsync();

        await Assertions.Expect(sheet.GetByText("Pausad till och med")).ToBeVisibleAsync();

        // Reload and reopen to confirm the pause actually persisted, not just an optimistic
        // client-side flag.
        await page.ReloadAsync();
        sheet = await HushallHelper.OpenMemberSheetAsync(page, "Sven");
        await Assertions.Expect(sheet.GetByText("Pausad till och med")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Pausing_the_whole_household_shows_a_status_notice()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Karin");

        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Button, new() { Name = "Pausa hushållet" }).ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Pausa hushållet" });

        await sheet.GetByLabel("Pausa till och med").FillAsync("2026-12-24");
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Pausa hushållet" }).ClickAsync();

        await Assertions.Expect(sheet.GetByText("Hushållet är pausat till och med")).ToBeVisibleAsync();

        await sheet.GetByRole(AriaRole.Button, new() { Name = "Återuppta nu" }).ClickAsync();
        await Assertions.Expect(sheet.GetByText("Hushållet är pausat till och med")).Not.ToBeVisibleAsync();
    }
}
