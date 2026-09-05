using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class PlaneringTests
{
    private readonly HemordnaAppFixture _app;

    public PlaneringTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Shows_a_qualitative_row_for_every_weekday()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Johanna");

        await page.GotoAsync("/planering");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Min vecka" }).WaitForAsync();

        await Assertions.Expect(page.Locator(".list-item")).ToHaveCountAsync(7);
        // A fresh household starts its creator at zero minutes a day - see CreateHousehold.
        // No number anywhere - "Ingen tid" is the qualitative equivalent.
        await Assertions.Expect(page.Locator(".list-item").First).ToContainTextAsync("Ingen tid");
    }

    [Fact]
    public async Task Choosing_a_level_for_today_sets_it_without_asking_for_a_number()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Kristina");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");

        await page.GotoAsync("/planering");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Idag" }).WaitForAsync();
        await page.Locator(".card", new() { HasText = "Idag" })
            .GetByRole(AriaRole.Button, new() { Name = "Gott om tid" }).ClickAsync();

        // Nothing on the page shows a minute count - verify against the API instead, the only
        // place a number still lives.
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var plan = await (await http.GetAsync($"/api/households/{householdId}/members/{memberId}/plan?date={today:yyyy-MM-dd}"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(60, plan.GetProperty("availableMinutes").GetInt32());
    }
}
