using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Ny form 2026" §3: a sticky, collapsed summary row fades in once the page has
/// scrolled - see docs/ARCHITECTURE.md "Beslut: Ny form 2026". Asserts the actual computed
/// opacity rather than Playwright's own ToBeVisibleAsync, which does not consider CSS opacity
/// at all (only display:none/visibility:hidden/zero size) and so would not have caught the real
/// bug found via screenshot review during this step: the row was rendered at opacity:1 but
/// fully hidden behind .app-topfade's own opaque-near-the-top gradient (z-index 8 vs the fade's
/// 9) - a pure stacking-order defect no DOM-level visibility check can see either.</summary>
[Collection(HemordnaAppCollection.Name)]
public class ScrollHeaderTests
{
    private readonly HemordnaAppFixture _app;

    public ScrollHeaderTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Idags_collapsed_header_only_becomes_visible_after_scrolling()
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await SignUpHelper.SignUpAsync(page, "Sara");

        // The collapsed row only renders once Idag actually has something to summarise - an
        // empty "Ledigt idag" day never shows it (see hasDayCounts in MinDag.razor).
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        // A fresh household's creator starts at zero minutes a day (see CreateHousehold) - the
        // occurrences below would otherwise land in Unplanned, not Items, and hasDayCounts
        // would stay false.
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 120, tuesday = 120, wednesday = 120, thursday = 120, friday = 120, saturday = 120, sunday = 120 });

        // Enough rows that the page is actually taller than the 844px viewport - a single task
        // leaves nothing to scroll, so window.scrollTo would silently clamp to 0 and the
        // threshold in Support/ScrollState.cs would never trip.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 1; i <= 10; i++)
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

        // h1 must stay in the DOM, at full opacity, regardless of scroll state.
        var heading = page.Locator("h1", new() { HasText = "Sara" });
        await Assertions.Expect(heading).ToBeVisibleAsync();

        Assert.Equal("0", await CollapsedOpacityAsync(page));

        await page.EvaluateAsync("window.scrollTo(0, 200)");
        // The attribute flips the instant ScrollState.cs's rAF callback runs, but the CSS
        // opacity transition (.2s) is still animating for a moment after that - wait for the
        // computed opacity itself to settle, not just the attribute that triggers it.
        await page.WaitForFunctionAsync(
            "() => getComputedStyle(document.querySelector('.day-header-collapsed')).opacity === '1'");

        Assert.Equal("1", await CollapsedOpacityAsync(page));
        await Assertions.Expect(heading).ToBeVisibleAsync();

        await page.EvaluateAsync("window.scrollTo(0, 0)");
        await page.WaitForFunctionAsync(
            "() => getComputedStyle(document.querySelector('.day-header-collapsed')).opacity === '0'");
        Assert.Equal("0", await CollapsedOpacityAsync(page));
    }

    private static async Task<string> CollapsedOpacityAsync(IPage page)
        => await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('.day-header-collapsed')).opacity");
}
