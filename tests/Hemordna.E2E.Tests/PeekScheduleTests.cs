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
        await page.GetByLabel("Namn").FillAsync("Sven");
        await page.Locator("form").GetByRole(AriaRole.Button, new() { Name = "Vuxen, jobbar heltid" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till medlem" }).ClickAsync();
        await Assertions.Expect(page.Locator(".list-item", new() { HasText = "Sven" })).ToBeVisibleAsync();

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

        await page.GotoAsync("/");
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

        await page.GotoAsync("/");
        await page.GetByText("Tjuvkika på ett schema").ClickAsync();
        await page.GetByLabel("Vilken dag?").SelectOptionAsync(new SelectOptionValue { Label = "Imorgon" });

        await Assertions.Expect(page.Locator(".task", new() { HasText = "Imorgondagens uppgift" }))
            .ToBeVisibleAsync();
    }
}
