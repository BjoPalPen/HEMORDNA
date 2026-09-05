using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>Shared onboarding path so each test file does not re-implement sign-up.</summary>
internal static class SignUpHelper
{
    internal const string Password = "Hemordna-E2E-2026!";

    internal static string UniqueEmail() => $"e2e-{Guid.NewGuid():N}@example.com";

    /// <summary>Registers, names a household and lands on Min dag.</summary>
    internal static async Task SignUpAsync(IPage page, string displayName, string householdName = "Familjen Andersson")
    {
        await RegisterAsync(page, displayName);

        await page.GetByLabel("Hushållets namn").FillAsync(householdName);
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa hushåll" }).ClickAsync();

        await page.GetByRole(AriaRole.Heading, new() { Name = $"Hej {displayName}!" })
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    /// <summary>
    /// Registers an account and stops on the "name your household or join one" screen -
    /// for tests that then join an existing household instead of creating a new one.
    /// </summary>
    internal static async Task RegisterAsync(IPage page, string displayName)
    {
        await page.GotoAsync("/logga-in");

        await page.GetByRole(AriaRole.Tab, new() { Name = "Skapa konto" }).ClickAsync();
        await page.GetByLabel("Ditt namn").FillAsync(displayName);
        await page.GetByLabel("E-post").FillAsync(UniqueEmail());
        await page.GetByLabel("Lösenord").FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa konto" }).ClickAsync();

        await page.GetByLabel("Hushållets namn").WaitForAsync(new() { Timeout = 15_000 });
    }
}
