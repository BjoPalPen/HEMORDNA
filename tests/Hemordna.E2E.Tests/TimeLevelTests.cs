using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Beslut: Ångra och stabil lista" §B5 - time as a level word, never a minute count,
/// on "Idag" (docs/PRODUCT.md §4/§8).</summary>
[Collection(HemordnaAppCollection.Name)]
public class TimeLevelTests
{
    private readonly HemordnaAppFixture _app;

    public TimeLevelTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Toggled_on_shows_a_time_level_chip_and_never_the_minute_count()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Yara");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Diska", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Markera Diska som klar" })).ToBeVisibleAsync();

        // Off by default - no chip, no digit anywhere in the row.
        await Assertions.Expect(page.Locator(".chip-time")).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("5 min")).Not.ToBeVisibleAsync();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/preferences",
            new { presentation = "Text", motivation = "None", showTimeLevel = true });
        await page.ReloadAsync();

        var chip = page.Locator(".chip-time");
        await Assertions.Expect(chip).ToBeVisibleAsync();
        await Assertions.Expect(chip).ToHaveTextAsync("Lite tid");

        // Never the raw number, on or off.
        await Assertions.Expect(page.GetByText("5 min")).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("5", new() { Exact = true })).Not.ToBeVisibleAsync();
    }
}
