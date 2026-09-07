using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class TaskFrequencyTests
{
    private readonly HemordnaAppFixture _app;

    public TaskFrequencyTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Changing_a_tasks_frequency_updates_what_is_shown_without_recreating_it()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Tova");

        await page.GotoAsync("/omraden");
        await page.GetByText("Lägg till ett tomt område i stället").ClickAsync();
        await page.GetByLabel("Nytt område").FillAsync("Kök");
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till område" }).ClickAsync();

        var kitchenCard = page.Locator(".card")
            .Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = "Kök", Exact = true }) });
        await kitchenCard.WaitForAsync();

        await kitchenCard.GetByText("Lägg till en uppgift i Kök").ClickAsync();
        await kitchenCard.GetByLabel("Namn").FillAsync("Diska");
        // Default estimate is fine - only the recurrence choice matters for this test.
        await kitchenCard.GetByLabel("Upprepning").SelectOptionAsync("Daily");
        await kitchenCard.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();

        // Scoped by its own "Ändra frekvens" button, not by name alone: once opened, the
        // edit form's own row also contains "Diska" (in its label), which a plain HasText
        // match would ambiguously catch too.
        var taskRow = kitchenCard.Locator(".list-item:has(button[aria-label=\"Ändra frekvens för Diska\"])");
        await Assertions.Expect(taskRow).ToContainTextAsync("varje dag");

        await taskRow.GetByRole(AriaRole.Button, new() { Name = "Ändra frekvens för Diska" }).ClickAsync();
        await kitchenCard.GetByLabel("Ny upprepning för \"Diska\"").SelectOptionAsync("Weekly");
        await kitchenCard.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();

        await Assertions.Expect(taskRow).ToContainTextAsync("varje vecka");
        await Assertions.Expect(taskRow).Not.ToContainTextAsync("varje dag");
    }

    [Fact]
    public async Task Changing_frequency_retires_the_now_stale_outstanding_occurrence()
    {
        // The actual production bug this fixes: moving a task to a new weekday left its old,
        // already-generated occurrence outstanding forever - nagging every day (endlessly
        // deferred) alongside a fresh one generated for the new weekday once it arrived. See
        // docs/ARCHITECTURE.md.
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Wilma");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{me.GetProperty("memberId").GetGuid()}/availability",
            new { date = today, availableMinutes = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new
            {
                name = "Torka golvet",
                estimatedMinutes = 10,
                hasRotatingResponsibility = true,
                recurrence = new { frequency = "Daily", interval = 1, startDate = today }
            }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        await page.GotoAsync("/");
        var completeButton = page.GetByRole(AriaRole.Button, new() { Name = "Markera Torka golvet som klar" });
        await Assertions.Expect(completeButton).ToBeVisibleAsync();

        // Move it to a weekday that is not today, far enough that no fresh occurrence for it is
        // due yet - anything still shown afterwards must be the old, stale one.
        var otherWeekday = today.DayOfWeek == DayOfWeek.Monday ? DayOfWeek.Wednesday : DayOfWeek.Monday;
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/frequency",
            new { recurrence = new { frequency = "Weekly", interval = 1, startDate = today, weekday = otherWeekday.ToString() } });

        await page.ReloadAsync();
        await Assertions.Expect(completeButton).Not.ToBeVisibleAsync();
    }
}
