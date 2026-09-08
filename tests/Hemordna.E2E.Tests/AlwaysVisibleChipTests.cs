using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Beslut: Ångra och stabil lista" §B7 - "Flytta till en annan dag" is always
/// rendered, never conditionally hidden, so the chip row's layout never reflows depending on
/// what happens to be true today.</summary>
[Collection(HemordnaAppCollection.Name)]
public class AlwaysVisibleChipTests
{
    private readonly HemordnaAppFixture _app;

    public AlwaysVisibleChipTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task With_nothing_unplanned_the_chip_stays_but_answers_with_a_status_line()
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

        // Nothing unplanned today - the chip is still there, just marked disabled for assistive
        // tech, and stays reachable/clickable (aria-disabled, not the disabled attribute).
        var moveChip = page.GetByRole(AriaRole.Button, new() { Name = "Flytta till en annan dag" });
        await Assertions.Expect(moveChip).ToBeVisibleAsync();
        await Assertions.Expect(moveChip).ToHaveAttributeAsync("aria-disabled", "true");

        // Force: aria-disabled (unlike the disabled attribute) does not block a real pointer
        // click in an actual browser - only Playwright's own actionability heuristic treats it
        // as non-interactive. The whole point of choosing aria-disabled here (docs/
        // ARCHITECTURE.md §B7) is that the chip stays truly clickable/focusable.
        await moveChip.ClickAsync(new() { Force = true });

        var notice = page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Inget att flytta just nu." });
        await Assertions.Expect(notice).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog, new() { Name = "Flytta till en annan dag" })).Not.ToBeVisibleAsync();
        await Assertions.Expect(notice).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
    }
}
