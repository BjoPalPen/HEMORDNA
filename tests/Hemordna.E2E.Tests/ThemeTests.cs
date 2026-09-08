using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>docs/ARCHITECTURE.md "Ny form" steg 5: dark tokens applied via prefers-color-scheme
/// automatically, or forced from Inställningar (data-theme, localStorage - Support/Theme.cs).</summary>
[Collection(HemordnaAppCollection.Name)]
public class ThemeTests
{
    private readonly HemordnaAppFixture _app;

    public ThemeTests(HemordnaAppFixture app) => _app = app;

    private static Task<string?> DataThemeAsync(IPage page)
        => page.EvaluateAsync<string?>("() => document.documentElement.getAttribute('data-theme')");

    private static Task<string> BodyBackgroundAsync(IPage page)
        => page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundColor");

    /// <summary>Waits for the attribute Blazor's async SetThemeAsync eventually sets, rather
    /// than asserting immediately after CheckAsync returns - the click's own event dispatch
    /// completes before the async Razor handler (JS module import + localStorage write) does.</summary>
    private static Task WaitForDataThemeAsync(IPage page, string? expected)
        => page.WaitForFunctionAsync(expected is null
            ? "() => document.documentElement.getAttribute('data-theme') === null"
            : $"() => document.documentElement.getAttribute('data-theme') === '{expected}'");

    [Fact]
    public async Task Follows_the_system_preference_by_default()
    {
        var page = await _app.NewPageAsync();
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await SignUpHelper.SignUpAsync(page, "Astrid");

        Assert.Null(await DataThemeAsync(page));
        // --kalk is #1C2024 (rgb(28, 32, 36)) in the dark palette, #F5F3EE in light - the body
        // background is the simplest observable proxy for "which palette actually applied".
        Assert.Equal("rgb(28, 32, 36)", await BodyBackgroundAsync(page));
    }

    [Fact]
    public async Task Forcing_dark_overrides_a_light_system_preference_and_survives_reload()
    {
        var page = await _app.NewPageAsync();
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light });
        await SignUpHelper.SignUpAsync(page, "Bertil");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Utseende" }).WaitForAsync();
        await page.GetByLabel("Mörkt").CheckAsync();
        await WaitForDataThemeAsync(page, "dark");

        Assert.Equal("rgb(28, 32, 36)", await BodyBackgroundAsync(page));

        await page.ReloadAsync();
        await WaitForDataThemeAsync(page, "dark");
        Assert.Equal("rgb(28, 32, 36)", await BodyBackgroundAsync(page));
    }

    [Fact]
    public async Task Forcing_light_overrides_a_dark_system_preference()
    {
        var page = await _app.NewPageAsync();
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await SignUpHelper.SignUpAsync(page, "Cornelia");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Utseende" }).WaitForAsync();
        await page.GetByLabel("Ljust").CheckAsync();
        await WaitForDataThemeAsync(page, "light");

        Assert.Equal("rgb(245, 243, 238)", await BodyBackgroundAsync(page));
    }

    [Fact]
    public async Task Switching_back_to_system_removes_the_forced_choice()
    {
        var page = await _app.NewPageAsync();
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await SignUpHelper.SignUpAsync(page, "Dagny");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Utseende" }).WaitForAsync();
        await page.GetByLabel("Ljust").CheckAsync();
        await WaitForDataThemeAsync(page, "light");

        await page.GetByLabel("Följ enhetens systeminställning").CheckAsync();
        await WaitForDataThemeAsync(page, null);
        // The system preference (still emulated as dark) is what applies once nothing overrides it.
        Assert.Equal("rgb(28, 32, 36)", await BodyBackgroundAsync(page));
    }

    private static Task<bool> HasDataCalmAsync(IPage page)
        => page.EvaluateAsync<bool>("() => document.documentElement.hasAttribute('data-calm')");

    /// <summary>"Lugnare skärm" (docs/ARCHITECTURE.md §B8/§B11) is per-device, exactly like
    /// theme - toggling it applies data-calm immediately, with no "Spara" step at all, unlike
    /// every other control on this page.</summary>
    [Fact]
    public async Task Toggling_calm_screen_sets_and_clears_data_calm_immediately_without_saving()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Erika");

        await page.GotoAsync("/installningar");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min visning" }).WaitForAsync();

        Assert.False(await HasDataCalmAsync(page));

        var calmToggle = page.GetByLabel("Lugnare skärm – inga rörelser eller genomskinliga effekter");
        await calmToggle.CheckAsync();
        await page.WaitForFunctionAsync("() => document.documentElement.hasAttribute('data-calm')");

        await calmToggle.UncheckAsync();
        await page.WaitForFunctionAsync("() => !document.documentElement.hasAttribute('data-calm')");
    }
}
