using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Ny form 2026" del B1 (docs/ARCHITECTURE.md "Beslut: Ångra och stabil lista") -
/// completing a task on Idag offers a brief window to take it back.</summary>
[Collection(HemordnaAppCollection.Name)]
public class UndoTests
{
    private readonly HemordnaAppFixture _app;

    public UndoTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Undo_brings_a_completed_task_back_and_the_offer_expires_on_its_own()
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
            $"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();

        var checkButton = page.GetByRole(AriaRole.Button, new() { Name = "Markera Diska som klar" });
        var undoRow = page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Klar: Diska" });

        // Complete it, then take it back - the row returns as an ordinary outstanding task,
        // exactly as if it had never been completed.
        await checkButton.ClickAsync();
        await Assertions.Expect(undoRow).ToBeVisibleAsync();
        await undoRow.GetByRole(AriaRole.Button, new() { Name = "Ångra" }).ClickAsync();

        await Assertions.Expect(undoRow).Not.ToBeVisibleAsync();
        await Assertions.Expect(checkButton).ToBeVisibleAsync();

        // Complete it again - this time let the offer expire on its own instead of using it.
        await checkButton.ClickAsync();
        await Assertions.Expect(undoRow).ToBeVisibleAsync();
        await Assertions.Expect(undoRow).Not.ToBeVisibleAsync(new() { Timeout = 9_000 });
    }
}
