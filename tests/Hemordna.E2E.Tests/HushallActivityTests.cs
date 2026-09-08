using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class HushallActivityTests
{
    private readonly HemordnaAppFixture _app;

    public HushallActivityTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Completing_a_task_shows_it_in_the_householders_recent_activity()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Karin");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");

        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Vattna blommorna", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var occurrence = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var occurrenceId = occurrence.GetProperty("id").GetGuid();

        var completed = await http.PostAsync(
            $"/api/households/{householdId}/occurrences/{occurrenceId}/complete", content: null);
        completed.EnsureSuccessStatusCode();

        await page.GotoAsync("/hushall");

        // Product feedback: "X markerade Y som klar" plus a clock time per row read as noise -
        // just a checkmark and the task's name now, no attribution.
        await Assertions.Expect(page.Locator(".task", new() { HasText = "Vattna blommorna" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Todays_activity_shows_a_household_wide_count_including_what_is_still_outstanding()
    {
        // PRODUCT.md §8: Hemordna never compares household members with each other - the count
        // next to "Idag" is a shared household total (out of everything due, not just what one
        // person's own list happened to include), never a per-member figure to compare.
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elsa");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        async Task CompleteOneTaskAsync(string name)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks",
                new { name, estimatedMinutes = 5 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            var taskId = task.GetProperty("id").GetGuid();

            var occurrence = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{taskId}/occurrences",
                new { date = today, assignToMemberId = memberId }))
                .Content.ReadFromJsonAsync<JsonElement>();
            var occurrenceId = occurrence.GetProperty("id").GetGuid();

            (await http.PostAsync($"/api/households/{householdId}/occurrences/{occurrenceId}/complete", content: null))
                .EnsureSuccessStatusCode();
        }

        // Still outstanding - part of today's total, but not completed, so the count must not
        // simply read "2 av 2" (everything that happened to be completed).
        var outstandingTask = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{outstandingTask.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await CompleteOneTaskAsync("Vattna blommorna");
        await CompleteOneTaskAsync("Bädda sängen");

        await page.GotoAsync("/hushall");

        var todayGroup = page.Locator(".activity-day", new() { HasText = "Idag" });
        await Assertions.Expect(todayGroup).ToContainTextAsync("2 av 3 uppgifter klara i hushållet");
        await Assertions.Expect(todayGroup.Locator(".day-ring")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_skipped_occurrence_does_not_stop_todays_ring_from_reaching_full()
    {
        // A skipped occurrence ("not needed this time", e.g. left behind by a frequency change -
        // see TaskFrequencyTests) is a conscious decision to shrink the day's scope, not an
        // unfinished item. If it stayed in the denominator forever, the ring could never reach
        // 100% again that day no matter what still gets done - a quiet, permanent "unfinished"
        // mark that contradicts PRODUCT.md §8's no-guilt framing.
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Nils");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Gets left behind as Skipped once its frequency moves to a weekday other than today.
        var dailyTask = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new
            {
                name = "Torka golvet",
                estimatedMinutes = 10,
                hasRotatingResponsibility = false,
                assignToMemberId = memberId,
                recurrence = new { frequency = "Daily", interval = 1, startDate = today }
            }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var dailyTaskId = dailyTask.GetProperty("id").GetGuid();

        var otherWeekday = today.DayOfWeek == DayOfWeek.Monday ? DayOfWeek.Wednesday : DayOfWeek.Monday;
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/tasks/{dailyTaskId}/frequency",
            new { recurrence = new { frequency = "Weekly", interval = 1, startDate = today, weekday = otherWeekday.ToString() } });

        var completedTask = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Vattna blommorna", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var occurrence = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{completedTask.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId }))
            .Content.ReadFromJsonAsync<JsonElement>();
        (await http.PostAsync(
            $"/api/households/{householdId}/occurrences/{occurrence.GetProperty("id").GetGuid()}/complete", content: null))
            .EnsureSuccessStatusCode();

        await page.GotoAsync("/hushall");

        var todayGroup = page.Locator(".activity-day", new() { HasText = "Idag" });
        await Assertions.Expect(todayGroup).ToContainTextAsync("1 av 1 uppgifter klara i hushållet");
        await Assertions.Expect(todayGroup.Locator(".day-ring-fill")).ToHaveAttributeAsync("stroke-dasharray", "100 0");
    }
}
