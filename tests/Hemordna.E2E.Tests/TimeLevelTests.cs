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

        // The exact saved minutes, never the level word it was chosen from, and never rounded
        // or hedged ("ca 5 min") - see docs/DESIGN.md §6a.
        var chip = page.Locator(".chip-time");
        await Assertions.Expect(chip).ToBeVisibleAsync();
        await Assertions.Expect(chip).ToHaveTextAsync("5 min");
        await Assertions.Expect(page.GetByText("Lite tid")).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("ca 5 min")).Not.ToBeVisibleAsync();
    }

    /// <summary>A task whose own minutes fall between two levels (45 sits past "Lång tid"s own
    /// 30) still shows exactly what was saved, not the nearest level's number - MinutesLabel
    /// never rounds, unlike LabelFor's own "closest level" logic used for the picker buttons.</summary>
    [Fact]
    public async Task A_task_between_levels_shows_its_own_exact_minutes_not_a_rounded_level()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Nils");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/preferences",
            new { presentation = "Text", motivation = "None", showTimeLevel = true });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Storstada", estimatedMinutes = 45 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();

        await Assertions.Expect(page.Locator(".chip-time")).ToHaveTextAsync("45 min");
    }
}
