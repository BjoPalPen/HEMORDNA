using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Beslut: Ångra och stabil lista" §B4 - "Visa nästa" in focus mode lets a member look
/// past today's first task without touching anything on the server.</summary>
[Collection(HemordnaAppCollection.Name)]
public class FocusNextTests
{
    private readonly HemordnaAppFixture _app;

    public FocusNextTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Visa_nasta_cycles_the_focus_card_without_changing_the_days_schedule()
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

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/preferences",
            new { presentation = "OneAtATime", motivation = "None", showTimeLevel = false });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string[] names = ["Diska", "Damma", "Dammsuga"];

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

        var focusName = page.Locator(".focus-name");
        var nextButton = page.GetByRole(AriaRole.Button, new() { Name = "Visa nästa" });
        await Assertions.Expect(focusName).ToBeVisibleAsync();
        await Assertions.Expect(nextButton).ToBeVisibleAsync();

        var firstName = await focusName.TextContentAsync();

        await nextButton.ClickAsync();
        var secondName = await focusName.TextContentAsync();
        Assert.NotEqual(firstName, secondName);

        await nextButton.ClickAsync();
        var thirdName = await focusName.TextContentAsync();
        Assert.NotEqual(secondName, thirdName);

        // A fourth click wraps back around to the first task - three tasks, three distinct
        // names seen, then a repeat.
        await nextButton.ClickAsync();
        var fourthName = await focusName.TextContentAsync();
        Assert.Equal(firstName, fourthName);

        // Nothing on the server changed - Vecka (and Idag's own reload) would still see all
        // three, still outstanding, none completed.
        var plan = await (await http.GetAsync(
            $"/api/households/{householdId}/members/{memberId}/plan?date={today:yyyy-MM-dd}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, plan.GetProperty("items").GetArrayLength());
        Assert.Equal(0, plan.GetProperty("completed").GetArrayLength());
    }
}
