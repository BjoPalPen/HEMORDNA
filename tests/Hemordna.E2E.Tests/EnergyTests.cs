using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Hur är orken idag?" (Sju enkla lösningar, del 1) - a one-day multiplier on today's
/// normal budget (Support/EnergyLevel.cs), never the weekly budget itself.</summary>
[Collection(HemordnaAppCollection.Name)]
public class EnergyTests
{
    private readonly HemordnaAppFixture _app;

    public EnergyTests(HemordnaAppFixture app) => _app = app;

    private static Task GiveFullWeekAsync(HttpClient http, Guid householdId, Guid memberId, int minutesPerDay)
        => http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new
            {
                monday = minutesPerDay,
                tuesday = minutesPerDay,
                wednesday = minutesPerDay,
                thursday = minutesPerDay,
                friday = minutesPerDay,
                saturday = minutesPerDay,
                sunday = minutesPerDay
            });

    private static async Task ScheduleFourTwentyMinuteTasksAsync(HttpClient http, Guid householdId, Guid memberId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 1; i <= 4; i++)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks", new { name = $"Uppgift {i}", estimatedMinutes = 20 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today, assignToMemberId = memberId });
        }
    }

    private static async Task<int> TodaysNormalMinutesAsync(HttpClient http, Guid householdId, Guid memberId)
    {
        var household = await (await http.GetAsync($"/api/households/{householdId}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var member = household.GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("id").GetGuid() == memberId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayProperty = today.DayOfWeek.ToString().ToLowerInvariant();
        return member.GetProperty("weeklyTimeBudgetMinutes").GetProperty(dayProperty).GetInt32();
    }

    [Fact]
    public async Task Choosing_a_level_scales_todays_minutes_survives_a_reload_and_never_touches_the_weekly_budget()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Elsa");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await GiveFullWeekAsync(http, householdId, memberId, 60);
        await ScheduleFourTwentyMinuteTasksAsync(http, householdId, memberId);

        await page.ReloadAsync();

        var energy = page.GetByRole(AriaRole.Group, new() { Name = "Hur är orken idag?" });
        await Assertions.Expect(energy).ToBeVisibleAsync();
        await Assertions.Expect(energy.GetByRole(AriaRole.Button, new() { Name = "Lite", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(energy.GetByRole(AriaRole.Button, new() { Name = "Lagom", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(energy.GetByRole(AriaRole.Button, new() { Name = "Mycket", Exact = true })).ToBeVisibleAsync();

        // "Lite" (60 × 0.4 = 24 min) fits exactly one 20-minute task - the other three wait.
        await energy.GetByRole(AriaRole.Button, new() { Name = "Lite", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(".task-check")).ToHaveCountAsync(1, new() { Timeout = 10_000 });

        // The row collapses to just the chosen chip - pressing it re-opens all three.
        await Assertions.Expect(energy.GetByRole(AriaRole.Button, new() { Name = "Lagom", Exact = true })).Not.ToBeVisibleAsync();
        var chosenChip = energy.GetByRole(AriaRole.Button, new() { Name = "Lite", Exact = true });
        await Assertions.Expect(chosenChip).ToBeVisibleAsync();
        await chosenChip.ClickAsync();
        await Assertions.Expect(energy.GetByRole(AriaRole.Button, new() { Name = "Mycket", Exact = true })).ToBeVisibleAsync();

        // "Mycket" (60 × 1.3 = 78 min) fits three 20-minute tasks - the fourth still waits.
        await energy.GetByRole(AriaRole.Button, new() { Name = "Mycket", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(".task-check")).ToHaveCountAsync(3, new() { Timeout = 10_000 });

        var moveChip = page.GetByRole(AriaRole.Button, new() { Name = "Flytta till en annan dag" });
        await moveChip.ClickAsync();
        var unplannedSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Flytta till en annan dag" });
        await Assertions.Expect(unplannedSheet.Locator(".task")).ToHaveCountAsync(1);
        await unplannedSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        // Reload - the choice (not just the day's minutes) is remembered, so the row shows the
        // collapsed "Mycket" chip again rather than the three-way picker.
        await page.ReloadAsync();
        var energyAfterReload = page.GetByRole(AriaRole.Group, new() { Name = "Hur är orken idag?" });
        await Assertions.Expect(energyAfterReload.GetByRole(AriaRole.Button, new() { Name = "Mycket", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(energyAfterReload.GetByRole(AriaRole.Button, new() { Name = "Lagom", Exact = true })).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".task-check")).ToHaveCountAsync(3);

        // The normal weekly budget itself was never touched - only today's available minutes.
        Assert.Equal(60, await TodaysNormalMinutesAsync(http, householdId, memberId));
    }

    [Fact]
    public async Task A_members_energy_choice_is_not_visible_to_another_member()
    {
        var aPage = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(aPage, "Wilhelm", "Familjen Ork");

        await aPage.GotoAsync("/hushall");
        await aPage.GetByRole(AriaRole.Button, new() { Name = "Bjud in" }).ClickAsync();
        var codeLocator = aPage.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" }).GetByLabel("Inbjudningskod");
        await codeLocator.WaitForAsync();
        var inviteCode = await codeLocator.InnerTextAsync();
        await aPage.GetByRole(AriaRole.Dialog, new() { Name = "Bjud in" }).GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        var bPage = await _app.NewPageAsync();
        await SignUpHelper.RegisterAsync(bPage, "Signe");
        await bPage.GetByText("Har du en inbjudningskod?").ClickAsync();
        await bPage.GetByLabel("Inbjudningskod").FillAsync(inviteCode);
        await bPage.GetByRole(AriaRole.Button, new() { Name = "Gå med i hushållet" }).ClickAsync();
        await bPage.Locator("h1", new() { HasText = "Signe" }).WaitForAsync(new() { Timeout = 15_000 });

        var aToken = await aPage.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var aHttp = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        aHttp.DefaultRequestHeaders.Authorization = new("Bearer", aToken);
        var aMe = await (await aHttp.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = aMe.GetProperty("householdId").GetGuid();
        var aMemberId = aMe.GetProperty("memberId").GetGuid();

        var bToken = await bPage.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var bHttp = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        bHttp.DefaultRequestHeaders.Authorization = new("Bearer", bToken);
        var bMe = await (await bHttp.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var bMemberId = bMe.GetProperty("memberId").GetGuid();

        await GiveFullWeekAsync(aHttp, householdId, aMemberId, 60);
        await GiveFullWeekAsync(bHttp, householdId, bMemberId, 60);

        await aPage.GotoAsync("/");
        var aEnergy = aPage.GetByRole(AriaRole.Group, new() { Name = "Hur är orken idag?" });
        await aEnergy.GetByRole(AriaRole.Button, new() { Name = "Lite", Exact = true }).ClickAsync();
        await Assertions.Expect(aEnergy.GetByRole(AriaRole.Button, new() { Name = "Lagom", Exact = true })).Not.ToBeVisibleAsync();

        // B's own screen still shows the un-answered three-way picker - A's choice, and even the
        // fact that A chose at all, never reaches B.
        await bPage.GotoAsync("/");
        var bEnergy = bPage.GetByRole(AriaRole.Group, new() { Name = "Hur är orken idag?" });
        await Assertions.Expect(bEnergy.GetByRole(AriaRole.Button, new() { Name = "Lite", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(bEnergy.GetByRole(AriaRole.Button, new() { Name = "Lagom", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(bEnergy.GetByRole(AriaRole.Button, new() { Name = "Mycket", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task No_budget_today_hides_the_energy_prompt_entirely()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Karolina");

        // A fresh household's creator starts at zero weekly capacity (see CreateHousehold) -
        // nothing to scale a fraction of, so the question never shows.
        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "Hur är orken idag?" })).Not.ToBeVisibleAsync();
    }

    /// <summary>Regression test: adding an icon to each chip (Support/EnergyLevel.cs +
    /// Icon.razor's battery-low/medium/full) widened all three enough that at 390px "Mycket"
    /// wrapped alone onto its own row - orphaned rather than grouped. Same "N buttons always
    /// share one row, shrink and wrap their own text instead" fix as .level-picker
    /// (docs/ARCHITECTURE.md §10, Stor text is unconditional) - checked in both modes here since
    /// that is exactly where the extra width bites hardest.</summary>
    [Theory]
    [InlineData("Text (standard) - kompakt lista")]
    [InlineData("Stor text - större och tydligare")]
    public async Task All_three_energy_chips_stay_on_one_row_at_390px(string presentationLabel)
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await SignUpHelper.SignUpAsync(page, "Otto");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await GiveFullWeekAsync(http, householdId, memberId, 60);

        if (presentationLabel.StartsWith("Stor text", StringComparison.Ordinal))
        {
            await page.GotoAsync("/installningar");
            await page.GetByLabel(presentationLabel).CheckAsync();
            await Assertions.Expect(page.GetByText("Sparat")).ToBeVisibleAsync(new() { Timeout = 5_000 });
        }

        await page.GotoAsync("/");
        var energy = page.GetByRole(AriaRole.Group, new() { Name = "Hur är orken idag?" });
        await energy.WaitForAsync();

        var lite = await energy.GetByRole(AriaRole.Button, new() { Name = "Lite", Exact = true }).BoundingBoxAsync();
        var lagom = await energy.GetByRole(AriaRole.Button, new() { Name = "Lagom", Exact = true }).BoundingBoxAsync();
        var mycket = await energy.GetByRole(AriaRole.Button, new() { Name = "Mycket", Exact = true }).BoundingBoxAsync();

        Assert.NotNull(lite);
        Assert.NotNull(lagom);
        Assert.NotNull(mycket);
        Assert.Equal(lite!.Y, lagom!.Y);
        Assert.Equal(lagom.Y, mycket!.Y);
    }
}
