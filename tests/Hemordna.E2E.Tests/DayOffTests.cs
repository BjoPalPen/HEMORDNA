using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// "Jag kan ... ta ledigt" - DayOffSheet, reached from Idag's own "Ta ledigt idag" chip and the
/// "Imorgon" section's own link (see docs/ARCHITECTURE.md "Beslut: Kvarlämnat, Imorgon på Idag,
/// ledig dag och tid i förväg").
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class DayOffTests
{
    private readonly HemordnaAppFixture _app;

    public DayOffTests(HemordnaAppFixture app) => _app = app;

    private static async Task<HttpClient> ArrangeAsync(IPage page, string apiUrl, string displayName)
    {
        await SignUpHelper.SignUpAsync(page, displayName);

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        var http = new HttpClient { BaseAddress = new Uri(apiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return http;
    }

    [Fact]
    public async Task Marking_today_off_shows_a_banner_and_flips_the_chip_label()
    {
        var page = await _app.NewPageAsync();
        await ArrangeAsync(page, _app.ApiUrl, "Hilda");

        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Button, new() { Name = "Ta ledigt idag" }).ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Ledig dag" });
        await sheet.WaitForAsync();

        await sheet.GetByRole(AriaRole.Button, new() { Name = "Ta med till idag" }).ClickAsync();
        await Assertions.Expect(sheet.GetByRole(AriaRole.Button, new() { Name = "Ångra ledig dag" }))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Du är ledig idag. Inget nytt läggs på dig.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Ledig idag" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Undoing_a_day_off_clears_the_banner_and_restores_the_chip_label()
    {
        var page = await _app.NewPageAsync();
        await ArrangeAsync(page, _app.ApiUrl, "Ivar");

        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Button, new() { Name = "Ta ledigt idag" }).ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Ledig dag" });
        await sheet.WaitForAsync();
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Ta med till idag" }).ClickAsync();
        await Assertions.Expect(sheet.GetByRole(AriaRole.Button, new() { Name = "Ångra ledig dag" }))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Ledig idag" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Ledig idag" }).ClickAsync();
        await sheet.WaitForAsync();
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Ångra ledig dag" }).ClickAsync();
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Du är ledig idag. Inget nytt läggs på dig.")).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Ta ledigt idag" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Taking_a_day_off_never_hides_or_removes_work_already_planned_that_day()
    {
        var page = await _app.NewPageAsync();
        var http = await ArrangeAsync(page, _app.ApiUrl, "Jonna");

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 10 })).Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.GotoAsync("/");
        await Assertions.Expect(page.Locator(".task", new() { HasText = "Diska" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });

        await page.GetByRole(AriaRole.Button, new() { Name = "Ta ledigt idag" }).ClickAsync();
        var sheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Ledig dag" });
        await sheet.WaitForAsync();
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Ta med till idag" }).ClickAsync();
        await Assertions.Expect(sheet.GetByRole(AriaRole.Button, new() { Name = "Ångra ledig dag" }))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        // A day off is a hard exclusion from ROTATION - it never touches work that is already
        // sitting there, whichever mode was picked.
        await Assertions.Expect(page.Locator(".task", new() { HasText = "Diska" })).ToBeVisibleAsync();
    }
}
