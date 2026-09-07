using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class PeekScheduleTests
{
    private readonly HemordnaAppFixture _app;

    public PeekScheduleTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Peeking_at_another_members_day_shows_their_tasks_without_a_way_to_complete_them()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elin");

        await page.GotoAsync("/hushall");
        await HushallHelper.AddMemberWithoutAccountAsync(page, "Sven", "Vuxen, jobbar heltid");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sven" })).ToBeVisibleAsync();

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();

        var household = await (await http.GetAsync($"/api/households/{householdId}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var sven = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("displayName").GetString() == "Sven");
        var svenId = sven.GetProperty("id").GetGuid();

        // Give Sven both money and time so his own task actually lands on his list, not
        // "till en annan dag" - see MinDag.razor's extra-task availability comment for why a
        // fresh member otherwise starts at zero minutes a day.
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{svenId}/availability",
            new { date = DateOnly.FromDateTime(DateTime.UtcNow), availableMinutes = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Svens uppgift", estimatedMinutes = 10 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = svenId });

        // "Tjuvkika på ett schema" moved from Idag to Vecka - see docs/ARCHITECTURE.md "Ny form".
        await page.GotoAsync("/vecka");
        await page.GetByText("Tjuvkika på ett schema").ClickAsync();
        await page.GetByLabel("Vems dag?").SelectOptionAsync(new SelectOptionValue { Label = "Sven" });

        var peekedTask = page.Locator(".task", new() { HasText = "Svens uppgift" });
        await Assertions.Expect(peekedTask).ToBeVisibleAsync();

        // The whole point of a "peek": nothing here can be marked done or postponed.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Markera Svens uppgift som klar" }))
            .Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Peeking_at_tomorrow_shows_a_task_scheduled_for_tomorrow()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Nils");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/availability",
            new { date = tomorrow, availableMinutes = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Imorgondagens uppgift", estimatedMinutes = 10 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = tomorrow, assignToMemberId = memberId });

        // "Tjuvkika på ett schema" moved from Idag to Vecka - see docs/ARCHITECTURE.md "Ny form".
        await page.GotoAsync("/vecka");
        await page.GetByText("Tjuvkika på ett schema").ClickAsync();
        await page.GetByLabel("Vilken dag?").SelectOptionAsync(new SelectOptionValue { Label = "Imorgon" });

        await Assertions.Expect(page.Locator(".task", new() { HasText = "Imorgondagens uppgift" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Peeking_at_tomorrow_labels_todays_still_outstanding_occurrence_separately_from_tomorrows_own()
    {
        // The actual production report this fixes: a daily task not yet completed today was
        // shown twice under "Imorgon" with no explanation, looking like a genuine duplicate.
        // It is really two different occurrences (today's, still overdue, folded forward -
        // the same behaviour the main Min dag view already labels "sedan tidigare" - and
        // tomorrow's own fresh one) but the peek view forgot to show that label at all.
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Otto");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/availability",
            new { date = tomorrow, availableMinutes = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Bädda sängen", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        // Today's occurrence is left outstanding (never completed) - genuinely overdue by the
        // time "tomorrow" is peeked - alongside a separate, freshly scheduled one for tomorrow.
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId });
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = tomorrow, assignToMemberId = memberId });

        // "Tjuvkika på ett schema" moved from Idag to Vecka - see docs/ARCHITECTURE.md "Ny form".
        await page.GotoAsync("/vecka");
        await page.GetByText("Tjuvkika på ett schema").ClickAsync();
        await page.GetByLabel("Vilken dag?").SelectOptionAsync(new SelectOptionValue { Label = "Imorgon" });

        // Scoped to the peek's own list - the member's main Min dag list above it separately
        // shows today's still-outstanding "Bädda sängen" too, with the same ".task" class.
        var peekList = page.GetByRole(AriaRole.List, new() { Name = "Tjuvkikad dag" });
        await Assertions.Expect(peekList.Locator(".task", new() { HasText = "Bädda sängen" })).ToHaveCountAsync(2);

        // Exactly one of the two carries the "from before" label - the still-outstanding one
        // from today - so the two are never mistaken for the same thing shown twice.
        await Assertions.Expect(peekList.GetByText("sedan tidigare")).ToHaveCountAsync(1);
    }
}
