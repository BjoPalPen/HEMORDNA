using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Beslut: Ångra och stabil lista" §B3 - "Lugn" (Installningar.razor) shows one of five
/// phrases, chosen deterministically by state - see MinDag.razor's own EncouragementFor.</summary>
[Collection(HemordnaAppCollection.Name)]
public class CalmMotivationTests
{
    private readonly HemordnaAppFixture _app;

    public CalmMotivationTests(HemordnaAppFixture app) => _app = app;

    /// <summary>Signs up, turns on "Lugn", and gives the member exactly <paramref name="total"/>
    /// tasks today with <paramref name="completed"/> of them already done - the same two
    /// numbers EncouragementFor itself branches on.</summary>
    private async Task<IPage> ArrangeCalmDayAsync(string displayName, int total, int completed)
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, displayName);

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 600, tuesday = 600, wednesday = 600, thursday = 600, friday = 600, saturday = 600, sunday = 600 });
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/preferences",
            new { presentation = "Text", motivation = "Calm", showTimeLevel = false });

        var today = DateOnly.FromDateTime(DateTime.Now);
        var occurrenceIds = new List<Guid>();

        for (var i = 0; i < total; i++)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks", new { name = $"Uppgift {i}", estimatedMinutes = 5 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            var occurrence = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today, assignToMemberId = memberId }))
                .Content.ReadFromJsonAsync<JsonElement>();
            occurrenceIds.Add(occurrence.GetProperty("id").GetGuid());
        }

        for (var i = 0; i < completed; i++)
        {
            await http.PostAsync($"/api/households/{householdId}/occurrences/{occurrenceIds[i]}/complete", null);
        }

        await page.ReloadAsync();
        return page;
    }

    [Fact]
    public async Task All_done_shows_the_calm_state_heading_but_not_a_second_copy_in_day_encouragement()
    {
        var page = await ArrangeCalmDayAsync("Yara", total: 1, completed: 1);

        await Assertions.Expect(page.Locator(".day-counts")).ToHaveTextAsync("1 av 1 klara");
        await Assertions.Expect(page.Locator(".day-encouragement")).Not.ToBeVisibleAsync();

        // Said once, by .calm-state's own heading - never twice on the same screen.
        await Assertions.Expect(page.GetByText("Dagens uppgifter är klara.")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Half_or_more_done_shows_the_most_important_is_done_phrase()
    {
        var page = await ArrangeCalmDayAsync("Bosse", total: 2, completed: 1);

        await Assertions.Expect(page.Locator(".day-counts")).ToHaveTextAsync("1 av 2 klara");
        await Assertions.Expect(page.Locator(".day-encouragement")).ToHaveTextAsync("Det viktigaste är gjort.");
    }

    [Fact]
    public async Task Some_done_but_less_than_half_shows_the_continue_where_you_left_off_phrase()
    {
        var page = await ArrangeCalmDayAsync("Cecilia", total: 3, completed: 1);

        await Assertions.Expect(page.Locator(".day-counts")).ToHaveTextAsync("1 av 3 klara");
        await Assertions.Expect(page.Locator(".day-encouragement")).ToHaveTextAsync("Vill du fortsätta där du slutade?");
    }

    [Fact]
    public async Task Nothing_done_and_more_than_four_outstanding_shows_the_one_thing_at_a_time_phrase()
    {
        var page = await ArrangeCalmDayAsync("David", total: 6, completed: 0);

        await Assertions.Expect(page.Locator(".day-counts")).ToHaveTextAsync("0 av 6 klara");
        await Assertions.Expect(page.Locator(".day-encouragement")).ToHaveTextAsync("En sak i taget räcker.");
    }

    [Fact]
    public async Task Nothing_done_and_four_or_fewer_outstanding_shows_the_default_phrase()
    {
        var page = await ArrangeCalmDayAsync("Elin", total: 2, completed: 0);

        await Assertions.Expect(page.Locator(".day-counts")).ToHaveTextAsync("0 av 2 klara");
        await Assertions.Expect(page.Locator(".day-encouragement")).ToHaveTextAsync("Här är dina uppgifter för idag.");
    }

    /// <summary>"Jag börjar nu" (Sju enkla lösningar, del 2): starting a task reads the same as
    /// having already completed one - without it, this exact state (0 done, 2 outstanding) would
    /// show the DEFAULT phrase (see Nothing_done_and_four_or_fewer_outstanding_shows_the_default_phrase
    /// above), not this one.</summary>
    [Fact]
    public async Task Nothing_done_but_a_task_is_started_shows_the_continue_where_you_left_off_phrase()
    {
        var page = await ArrangeCalmDayAsync("Gustav", total: 2, completed: 0);

        await page.Locator(".task-expand").First.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Jag börjar nu" }).ClickAsync();

        await Assertions.Expect(page.Locator(".day-encouragement")).ToHaveTextAsync("Vill du fortsätta där du slutade?");
    }

    [Fact]
    public async Task None_shows_no_phrase_at_all()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Frida");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Diska", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();

        await Assertions.Expect(page.Locator(".day-counts")).ToHaveTextAsync("0 av 1 klara");
        await Assertions.Expect(page.Locator(".day-encouragement")).Not.ToBeVisibleAsync();
    }
}
