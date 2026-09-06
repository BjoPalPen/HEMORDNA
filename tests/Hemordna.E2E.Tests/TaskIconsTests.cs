using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class TaskIconsTests
{
    private readonly HemordnaAppFixture _app;

    public TaskIconsTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Setting_presentation_to_image_and_text_shows_an_icon_guessed_from_the_tasks_name()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ida");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");

        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var taskId = task.GetProperty("id").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.GotoAsync("/");
        await Assertions.Expect(page.Locator(".task-icon")).Not.ToBeVisibleAsync();

        await page.GotoAsync("/installningar");
        await page.GetByLabel("Bild + text - med bilder för tydlighet").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Spara" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Spara" })).ToBeEnabledAsync();

        await page.GotoAsync("/");

        // A fresh household's creator starts at zero minutes a day (see CreateHousehold), so
        // the task just created lands in "till en annan dag" rather than today's list - expand
        // it rather than fight the budget just to see the icon.
        await page.GetByText("till en annan dag").ClickAsync();

        var icon = page.Locator(".task-icon");
        await Assertions.Expect(icon).ToBeVisibleAsync();
        await Assertions.Expect(icon).ToHaveAttributeAsync("src", "icons/tasks/restaurant.svg");
    }
}
