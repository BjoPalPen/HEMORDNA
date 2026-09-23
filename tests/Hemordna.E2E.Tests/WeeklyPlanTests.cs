using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Planera veckan" - se docs/ARCHITECTURE.md "Beslut: Placeringsalgoritmen".</summary>
[Collection(HemordnaAppCollection.Name)]
public class WeeklyPlanTests
{
    private readonly HemordnaAppFixture _app;

    public WeeklyPlanTests(HemordnaAppFixture app) => _app = app;

    private static ILocator Sheet(IPage page, string title) => page.GetByRole(AriaRole.Dialog, new() { Name = title });

    private async Task<(HttpClient Http, Guid HouseholdId)> AuthorizedHttpAsync(IPage page)
    {
        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();

        return (http, me.GetProperty("householdId").GetGuid());
    }

    private static async Task<Guid> CreateAreaAsync(HttpClient http, Guid householdId, string name)
    {
        var response = await http.PostAsJsonAsync($"/api/households/{householdId}/areas", new { name });
        var area = await response.Content.ReadFromJsonAsync<JsonElement>();

        return area.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CreateTaskAsync(
        HttpClient http, Guid householdId, string name, int minutes, string effort, Guid areaId, DateOnly today, string weekday)
    {
        var response = await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new
            {
                name,
                estimatedMinutes = minutes,
                areaId,
                hasRotatingResponsibility = true,
                effort,
                recurrence = new { frequency = "Weekly", interval = 1, startDate = today, weekday }
            });

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> FetchTasksAsync(HttpClient http, Guid householdId)
        => await (await http.GetAsync($"/api/households/{householdId}/tasks")).Content.ReadFromJsonAsync<JsonElement>();

    private static string WeekdayOf(JsonElement tasks, string name)
        => tasks.EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == name)
            .GetProperty("recurrence").GetProperty("weekday").GetString()!;

    [Fact]
    public async Task Previewing_the_week_shows_a_suggestion_and_using_it_changes_upcoming_placement()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Frida");

        var (http, householdId) = await AuthorizedHttpAsync(page);
        using var _ = http;

        // Give the household enough weekly capacity to actually place work, and set up the
        // exact real-world skew Björn reported: two rooms' weekly tasks collided on the same
        // weekday, regardless of their size - see docs/ARCHITECTURE.md.
        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var memberId = me.GetProperty("memberId").GetGuid();
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 120, tuesday = 120, wednesday = 120, thursday = 120, friday = 120, saturday = 120, sunday = 120 });

        var bathroom = await CreateAreaAsync(http, householdId, "Badrum");
        var kitchen = await CreateAreaAsync(http, householdId, "Kök");

        var today = AppDate.Today;
        await CreateTaskAsync(http, householdId, "Storstäda badrummet", 40, "Heavy", bathroom, today, "Monday");
        await CreateTaskAsync(http, householdId, "Diska köket varje vecka", 10, "Medium", kitchen, today, "Monday");

        var beforeTasks = await FetchTasksAsync(http, householdId);
        Assert.Equal(WeekdayOf(beforeTasks, "Storstäda badrummet"), WeekdayOf(beforeTasks, "Diska köket varje vecka"));

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Button, new() { Name = "Planera veckan" }).ClickAsync();
        var planSheet = Sheet(page, "Planera veckan");
        await planSheet.WaitForAsync();

        // Visar förslag: båda rummens besök syns i förslaget, med minuter. Etiketten "Storstäd"
        // visas inte längre - alternativ B (Björns beslut) slår ihop RegularClean och DeepClean
        // till samma vanliga "Städ"-besök, se docs/ARCHITECTURE.md "Beslut: rumsregeln per besök".
        await Assertions.Expect(planSheet.GetByText("Badrum")).ToBeVisibleAsync();
        await Assertions.Expect(planSheet.GetByText("Kök")).ToBeVisibleAsync();
        await Assertions.Expect(planSheet.GetByText("40 min", new() { Exact = false })).ToBeVisibleAsync();

        var applyButton = planSheet.GetByRole(AriaRole.Button, new() { Name = "Använd" });
        await applyButton.ClickAsync();

        // Använd ändrar kommande veckors placering - lugn bekräftelse, gäller framåt.
        await Assertions.Expect(planSheet.GetByText("Klart.", new() { Exact = false })).ToBeVisibleAsync();
        await Assertions.Expect(planSheet.GetByText("kommande veckor", new() { Exact = false })).ToBeVisibleAsync();

        var afterTasks = await FetchTasksAsync(http, householdId);
        Assert.NotEqual(
            WeekdayOf(afterTasks, "Storstäda badrummet"), WeekdayOf(afterTasks, "Diska köket varje vecka"));
    }

    /// <summary>
    /// "Planera veckan går att ändra" (Björns krav) - flytta ett besök i förhandsvisningen, använd
    /// planen, och se att flytten överlevde en riktig reload som ett lås ("alltid torsdag") - inte
    /// bara den öppna komponentinstansen. Se docs/ARCHITECTURE.md "Beslut: redigerbar plan".
    /// </summary>
    [Fact]
    public async Task Moving_a_visit_in_the_sheet_and_using_the_plan_locks_it_to_the_chosen_day()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Nils");

        var (http, householdId) = await AuthorizedHttpAsync(page);
        using var _ = http;

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var memberId = me.GetProperty("memberId").GetGuid();
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 120, tuesday = 120, wednesday = 120, thursday = 120, friday = 120, saturday = 120, sunday = 120 });

        var bathroom = await CreateAreaAsync(http, householdId, "Badrum");
        var today = AppDate.Today;
        await CreateTaskAsync(http, householdId, "Skrubba handfatet", 20, "Medium", bathroom, today, "Friday");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Button, new() { Name = "Planera veckan" }).ClickAsync();
        var planSheet = Sheet(page, "Planera veckan");
        await planSheet.WaitForAsync();

        var moveSelect = planSheet.GetByLabel("Flytta Badrum", new() { Exact = false });
        await moveSelect.WaitForAsync();
        await moveSelect.SelectOptionAsync("Thursday");

        // Flytten skickas om serverside och besöket markeras lugnt som låst till den nya dagen.
        await Assertions.Expect(planSheet.GetByText("alltid torsdag", new() { Exact = false })).ToBeVisibleAsync();

        await planSheet.GetByRole(AriaRole.Button, new() { Name = "Använd" }).ClickAsync();
        await Assertions.Expect(planSheet.GetByText("Klart.", new() { Exact = false })).ToBeVisibleAsync();

        // Två "Stäng"-knappar finns nu samtidigt: arkets eget header-kryss och bekräftelsevyns
        // egen knapp (btn-primary) - den senare kommer sist i DOM-ordningen.
        await planSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).Last.ClickAsync();

        // Riktig reload, inte bara samma komponentinstans igen - bevisar att låset faktiskt
        // sparades via API:t, inte bara lokal state i arket.
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Planera veckan" }).ClickAsync();
        var reopenedSheet = Sheet(page, "Planera veckan");
        await reopenedSheet.WaitForAsync();

        await Assertions.Expect(reopenedSheet.GetByText("alltid torsdag", new() { Exact = false })).ToBeVisibleAsync();

        var tasksAfter = await FetchTasksAsync(http, householdId);
        var task = tasksAfter.EnumerateArray().Single(t => t.GetProperty("name").GetString() == "Skrubba handfatet");
        Assert.Equal("Thursday", task.GetProperty("preferredWeekday").GetString());
        Assert.Equal("Thursday", task.GetProperty("recurrence").GetProperty("weekday").GetString());
    }
}
