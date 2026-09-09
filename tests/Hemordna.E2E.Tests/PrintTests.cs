using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Skriv ut" (Sju enkla lösningar, del 5) - a dedicated, always-built print-only view
/// (MinDag.razor's own "print-only" block) so a focus-mode print still shows the whole day, not
/// just the one card the screen itself shows.</summary>
[Collection(HemordnaAppCollection.Name)]
public class PrintTests
{
    private readonly HemordnaAppFixture _app;

    public PrintTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Print_media_hides_the_nav_and_shows_every_task_as_its_own_row()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ingrid");

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
            new { name = "Diska", estimatedMinutes = 5, description = "Ta fram hinken\nTorka" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Markera Diska som klar" })).ToBeVisibleAsync();

        await page.EmulateMediaAsync(new() { Media = Media.Print });

        await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Huvudmeny" })).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".chips")).Not.ToBeVisibleAsync();

        var printedRow = page.Locator(".print-task", new() { HasText = "Diska" });
        await Assertions.Expect(printedRow).ToBeVisibleAsync();

        // Steps print under the row too, not just behind an on-screen expand toggle.
        await Assertions.Expect(printedRow.Locator("ol.task-steps li")).ToHaveCountAsync(2);
    }

    /// <summary>Focus mode shows one card on screen, but print still needs the full agenda - the
    /// print-only block is built independently of IsFocusMode for exactly this reason.</summary>
    [Fact]
    public async Task Focus_mode_still_prints_the_whole_day_not_just_the_current_card()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Oskar");

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
            new { presentation = "OneAtATime", motivation = "None", showTimeLevel = false });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string[] names = ["Diska", "Damma"];

        foreach (var name in names)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks", new { name, estimatedMinutes = 5 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today, assignToMemberId = memberId });
        }

        await page.ReloadAsync();
        await Assertions.Expect(page.Locator(".focus-card")).ToBeVisibleAsync();

        await page.EmulateMediaAsync(new() { Media = Media.Print });

        // The card itself is a screen-only concept - hidden in print, same as the rest of the
        // ordinary UI - while both tasks print as their own rows regardless.
        await Assertions.Expect(page.Locator(".focus-card")).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".print-task")).ToHaveCountAsync(2);
    }
}
