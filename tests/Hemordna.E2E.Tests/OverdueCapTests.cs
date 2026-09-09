using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Beslut: Ångra och stabil lista" §B6 - a long "Sedan tidigare" reads as a wall of
/// failure, not a plan, so more than a handful stay capped until asked to see the rest.</summary>
[Collection(HemordnaAppCollection.Name)]
public class OverdueCapTests
{
    private readonly HemordnaAppFixture _app;

    public OverdueCapTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Seven_overdue_tasks_show_three_plus_a_count_until_visa_alla()
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
            new { monday = 600, tuesday = 600, wednesday = 600, thursday = 600, friday = 600, saturday = 600, sunday = 600 });

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        for (var i = 1; i <= 7; i++)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks", new { name = $"Uppgift {i}", estimatedMinutes = 5 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = yesterday, assignToMemberId = memberId });
        }

        await page.ReloadAsync();

        var overdueList = page.GetByRole(AriaRole.List, new() { Name = "Sedan tidigare" });
        await Assertions.Expect(overdueList).ToBeVisibleAsync();

        // Capped: exactly 3 checkable rows, plus the "... och 4 till" row with both actions.
        await Assertions.Expect(overdueList.GetByRole(AriaRole.Button, new() { NameRegex = new("^Markera Uppgift \\d som klar$") }))
            .ToHaveCountAsync(3);
        var moreRow = page.Locator(".task-more");
        await Assertions.Expect(moreRow).ToContainTextAsync("och 4 till");

        var showAll = moreRow.GetByRole(AriaRole.Button, new() { Name = "Visa alla" });
        await Assertions.Expect(showAll).ToBeVisibleAsync();
        await Assertions.Expect(moreRow.GetByRole(AriaRole.Button, new() { Name = "Låt Hemordna sprida ut dem" }))
            .ToBeVisibleAsync();

        // The heading itself still counts all seven, capped or not.
        await Assertions.Expect(page.GetByText("Sedan tidigare")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("7 kvar")).ToBeVisibleAsync();

        await showAll.ClickAsync();

        await Assertions.Expect(overdueList.GetByRole(AriaRole.Button, new() { NameRegex = new("^Markera Uppgift \\d som klar$") }))
            .ToHaveCountAsync(7);
        await Assertions.Expect(moreRow).Not.ToBeVisibleAsync();
    }

    /// <summary>The three shown when capped are the three GENUINELY oldest
    /// (OriginalScheduledDate), not just whichever three the server happened to list first -
    /// PlannedTaskResponse gained that field specifically to make this possible (previously a
    /// documented, reported contract gap - see ARCHITECTURE.md §B6).</summary>
    [Fact]
    public async Task Capped_overdue_items_are_the_oldest_by_original_scheduled_date_not_server_order()
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
            new { monday = 600, tuesday = 600, wednesday = 600, thursday = 600, friday = 600, saturday = 600, sunday = 600 });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Deliberately created out of chronological order, so a naive "server/creation order"
        // cap would show the wrong three. "ZZOldest" sorts last alphabetically but is scheduled
        // furthest in the past - proves the sort key is the date, name only a tiebreaker.
        (string Name, int DaysAgo)[] tasks =
        [
            ("Nyast", 1), ("ZZOldest", 6), ("Mellan", 3), ("Näst", 2), ("Fyra", 4), ("Fem", 5), ("Sex", 6)
        ];

        foreach (var (name, daysAgo) in tasks)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks", new { name, estimatedMinutes = 5 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today.AddDays(-daysAgo), assignToMemberId = memberId });
        }

        await page.ReloadAsync();

        var overdueList = page.GetByRole(AriaRole.List, new() { Name = "Sedan tidigare" });
        await Assertions.Expect(overdueList).ToBeVisibleAsync();

        // .task-name's own text node (the bare name) read directly, rather than its full text
        // content - AllTextContentsAsync would also pick up any chip rendered inside the same
        // span (room, time, "Börja här", ...), with no reliable separator to split back out.
        var names = await overdueList.Locator(".task-name").EvaluateAllAsync<string[]>(
            "els => els.map(el => el.childNodes[0].textContent.trim())");
        // "Sex" and "ZZOldest" share the oldest date (6 days ago) - name is the documented
        // tiebreaker, so "Sex" (alphabetically first) shows before "ZZOldest".
        Assert.Equal(["Sex", "ZZOldest", "Fem"], names);
    }
}
