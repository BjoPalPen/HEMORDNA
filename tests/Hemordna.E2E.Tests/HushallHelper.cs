using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>Shared Hushåll interactions - member management moved behind avatars and sheets in
/// "Ny form" steg 4 (see docs/ARCHITECTURE.md), so every test that used to fill the page's own
/// inline "Lägg till medlem" form now goes through the same "Bjud in" sheet.</summary>
internal static class HushallHelper
{
    /// <summary>Assumes the page is already on /hushall.</summary>
    internal static async Task AddMemberWithoutAccountAsync(IPage page, string name, string roleLabel)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" });
        await sheet.GetByText("Eller lägg till en medlem utan eget konto").ClickAsync();
        await sheet.GetByLabel("Namn").FillAsync(name);
        await sheet.Locator("form").GetByRole(AriaRole.Button, new() { Name = roleLabel }).ClickAsync();

        var submit = sheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till medlem" });
        await submit.ClickAsync();
        await Assertions.Expect(submit).ToBeEnabledAsync();

        // The sheet does not close itself on submit - left open, it keeps intercepting pointer
        // events on the rest of the page for whoever calls this next.
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();
    }

    /// <summary>Opens a member's own sheet from their avatar - the avatar's accessible name is
    /// all of its text (initial, name, role), so this matches on the name as a substring.</summary>
    internal static async Task<ILocator> OpenMemberSheetAsync(IPage page, string name)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = name }).First.ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = name });
        await sheet.WaitForAsync();
        return sheet;
    }
}
