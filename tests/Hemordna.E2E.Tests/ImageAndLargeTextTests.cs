using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// "Bild + stor text" and its focus-mode counterpart - two named PresentationMode values that
/// each carry both facts at once (see Support/PresentationModes.cs and docs/DESIGN.md §7).
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class ImageAndLargeTextTests
{
    private readonly HemordnaAppFixture _app;

    public ImageAndLargeTextTests(HemordnaAppFixture app) => _app = app;

    private static async Task<(HttpClient Http, Guid HouseholdId, Guid MemberId)> ArrangeAsync(
        IPage page, string apiUrl, string displayName)
    {
        await SignUpHelper.SignUpAsync(page, displayName);

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        var http = new HttpClient { BaseAddress = new Uri(apiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 10 })).Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        return (http, householdId, memberId);
    }

    [Fact]
    public async Task ImageAndLargeText_shows_an_icon_on_the_grouped_list_and_scales_the_text()
    {
        var page = await _app.NewPageAsync();
        var (http, householdId, memberId) = await ArrangeAsync(page, _app.ApiUrl, "Astrid");

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/preferences",
            new { presentation = "ImageAndLargeText", motivation = "None", showTimeLevel = false });

        await page.GotoAsync("/");
        await Assertions.Expect(page.Locator(".task-icon").First).ToBeVisibleAsync(new() { Timeout = 15_000 });
        Assert.Equal(
            "large",
            await page.EvaluateAsync<string?>("() => document.documentElement.getAttribute('data-text-size')"));
    }

    [Fact]
    public async Task OneAtATimeImageAndLargeText_shows_the_focus_card_with_an_icon_and_scaled_text()
    {
        var page = await _app.NewPageAsync();
        var (http, householdId, memberId) = await ArrangeAsync(page, _app.ApiUrl, "Bertil");

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/preferences",
            new { presentation = "OneAtATimeImageAndLargeText", motivation = "None", showTimeLevel = false });

        await page.GotoAsync("/");
        await Assertions.Expect(page.Locator(".focus-card")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(page.Locator(".focus-icon")).ToBeVisibleAsync();
        Assert.Equal(
            "large",
            await page.EvaluateAsync<string?>("() => document.documentElement.getAttribute('data-text-size')"));
    }
}
