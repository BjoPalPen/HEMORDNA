using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>docs/DESIGN.md §8: the same four destinations, same order, on mobile and desktop -
/// no "Mer" tab. Inställningar and Logga ut are reached from Hushåll instead.</summary>
[Collection(HemordnaAppCollection.Name)]
public class MobileNavTests
{
    private readonly HemordnaAppFixture _app;

    public MobileNavTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Shows_the_same_four_destinations_on_mobile_as_desktop()
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await SignUpHelper.SignUpAsync(page, "Nora");

        var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Huvudmeny" });

        foreach (var visible in new[] { "Idag", "Rum", "Vecka", "Hushåll" })
        {
            await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = visible })).ToBeVisibleAsync();
        }

        await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Mer" })).Not.ToBeVisibleAsync();

        await nav.GetByRole(AriaRole.Link, new() { Name = "Hushåll" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Familjen Andersson" }).WaitForAsync();

        // The chevron is a decorative SVG icon (Icon.razor), not literal "›" text, since step 4.
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Inställningar" })).ToBeVisibleAsync();
    }

    /// <summary>"Ny form 2026" §1: the floating pill sits over the content rather than pushing
    /// it up in a reserved bar - a long list must still scroll fully clear of the pill's own
    /// bounding box, not just stop short of where the old full-width bar used to start.</summary>
    [Fact]
    public async Task Scrolling_to_the_bottom_clears_the_floating_pill()
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await SignUpHelper.SignUpAsync(page, "Sigrid");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 120, tuesday = 120, wednesday = 120, thursday = 120, friday = 120, saturday = 120, sunday = 120 });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 1; i <= 8; i++)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks",
                new { name = $"Uppgift {i}", estimatedMinutes = 5 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today, assignToMemberId = memberId });
        }

        await page.ReloadAsync();
        var lastTask = page.Locator(".task").Last;
        await lastTask.WaitForAsync();

        // ScrollIntoViewIfNeededAsync only scrolls the MINIMUM distance needed to make the
        // element visible - it does not know about .app-main's reserved padding-bottom and can
        // stop right as the element's bottom edge touches the literal viewport edge, which is
        // exactly where the floating pill sits. Scrolling to the true document bottom is what
        // this test actually means by "the user has scrolled all the way down".
        await page.EvaluateAsync("window.scrollTo(0, document.documentElement.scrollHeight)");

        var lastTaskBox = await lastTask.BoundingBoxAsync();
        var navBox = await page.Locator("nav.nav-shell").BoundingBoxAsync();

        Assert.NotNull(lastTaskBox);
        Assert.NotNull(navBox);
        Assert.True(lastTaskBox!.Y + lastTaskBox.Height <= navBox!.Y,
            $"Last task (bottom {lastTaskBox.Y + lastTaskBox.Height}) overlaps the floating nav pill (top {navBox.Y}).");
    }
}
