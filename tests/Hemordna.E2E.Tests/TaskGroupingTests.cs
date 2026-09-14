using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class TaskGroupingTests
{
    private readonly HemordnaAppFixture _app;

    public TaskGroupingTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Todays_tasks_are_grouped_by_room_with_overdue_items_inside_their_own_room()
    {
        // The actual product concern this addresses: DailyPlanner's own sort interleaves rooms
        // (it has no concept of "room" at all) - "Dammsug golvet" (Kök), "Byt handdukar"
        // (Badrum) and "Torka golvet" (Kök) land in that literal order given equal minutes and
        // priority, which reads as a random jumble rather than "finish the kitchen, then the
        // bathroom". The room-grouping this test verifies re-clusters an already-decided list
        // for display only - it does not change what DailyPlanner selected.
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Greta");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/availability",
            new { date = today, availableMinutes = 60 });

        var kitchenId = await CreateAreaAsync(http, householdId, "Kök");
        var bathroomId = await CreateAreaAsync(http, householdId, "Badrum");

        // Created in an order deliberately different from the room grouping we expect, so a
        // pass here cannot be an accident of creation order either.
        var vacuumId = await CreateTaskAsync(http, householdId, "Dammsug golvet", kitchenId);
        var towelsId = await CreateTaskAsync(http, householdId, "Byt handdukar", bathroomId);
        var mopId = await CreateTaskAsync(http, householdId, "Torka golvet", kitchenId);
        var overdueId = await CreateTaskAsync(http, householdId, "Diska", kitchenId);

        await ScheduleAsync(http, householdId, vacuumId, today, memberId);
        await ScheduleAsync(http, householdId, towelsId, today, memberId);
        await ScheduleAsync(http, householdId, mopId, today, memberId);
        // Still outstanding from yesterday - genuinely overdue by today.
        await ScheduleAsync(http, householdId, overdueId, today.AddDays(-1), memberId);

        await page.GotoAsync("/");

        var kitchenGroup = page.Locator("ul[aria-label=\"Kök\"]");
        var bathroomGroup = page.Locator("ul[aria-label=\"Badrum\"]");

        // Det försenade köksarbetet ligger i KÖKET, inte i en egen grupp högst upp: det är var
        // man står fysiskt som avgör vad man gör härnäst, så allt i samma rum kommer på rad.
        // Produktåterkoppling 2026-09-14, se docs/ARCHITECTURE.md "Beslut: hela dagen i
        // rumsordning". Det finns ingen separat "Sedan tidigare"-lista längre.
        await Assertions.Expect(page.Locator("ul[aria-label=\"Sedan tidigare\"]")).ToHaveCountAsync(0);
        await Assertions.Expect(kitchenGroup.GetByText("Diska")).ToBeVisibleAsync();

        // Men raden bär fortfarande sin egen markering om att den är sedan tidigare.
        await Assertions.Expect(kitchenGroup.GetByText("Sedan tidigare").First).ToBeVisibleAsync();

        // Both of today's Kök tasks are together in ONE group, despite DailyPlanner's own flat
        // order interleaving them with the Badrum task in between.
        await Assertions.Expect(kitchenGroup.GetByText("Dammsug golvet")).ToBeVisibleAsync();
        await Assertions.Expect(kitchenGroup.GetByText("Torka golvet")).ToBeVisibleAsync();
        await Assertions.Expect(kitchenGroup.GetByText("Byt handdukar")).Not.ToBeVisibleAsync();

        await Assertions.Expect(bathroomGroup.GetByText("Byt handdukar")).ToBeVisibleAsync();
        await Assertions.Expect(bathroomGroup.GetByText("Dammsug golvet")).Not.ToBeVisibleAsync();

        // Inom rummet ligger det försenade först - man tar det som redan väntat medan man är där.
        var kitchenTaskNames = (await kitchenGroup.Locator(".task-name").AllTextContentsAsync()).ToList();
        var overdueIndex = kitchenTaskNames.FindIndex(name => name.Contains("Diska"));
        var vacuumIndex = kitchenTaskNames.FindIndex(name => name.Contains("Dammsug golvet"));
        var mopIndex = kitchenTaskNames.FindIndex(name => name.Contains("Torka golvet"));

        Assert.True(overdueIndex == 0, $"Försenat ska ligga först i rummet, låg på {overdueIndex}.");
        Assert.True(vacuumIndex >= 0 && mopIndex >= 0 && vacuumIndex < mopIndex);
    }

    private static async Task<Guid> CreateAreaAsync(HttpClient http, Guid householdId, string name)
    {
        var area = await (await http.PostAsJsonAsync($"/api/households/{householdId}/areas", new { name }))
            .Content.ReadFromJsonAsync<JsonElement>();
        return area.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTaskAsync(HttpClient http, Guid householdId, string name, Guid areaId)
    {
        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name, estimatedMinutes = 5, areaId }))
            .Content.ReadFromJsonAsync<JsonElement>();
        return task.GetProperty("id").GetGuid();
    }

    private static Task ScheduleAsync(HttpClient http, Guid householdId, Guid taskId, DateOnly date, Guid memberId)
        => http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date, assignToMemberId = memberId });

    /// <summary>
    /// Björns egen rapport 2026-09-14: "så skall jag torka av handfatet på liten wc och det finns
    /// fler uppgifter i samma yta ... så skall se komma på rad inte att jag skall torka av
    /// handfatet i badrummet där uppe efter torka av handfatet på liten wc". Två rum på olika
    /// våningar med IDENTISKT namngivna uppgifter är det värsta fallet: den gamla
    /// "Sedan tidigare"-gruppen sorterade på namn, så just de två hamnade bredvid varandra
    /// medan resten av lilla wc:t låg någon annanstans.
    /// </summary>
    [Fact]
    public async Task All_of_a_rooms_tasks_come_in_a_row_even_when_another_floor_has_the_same_task_name()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Björn");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 600, tuesday = 600, wednesday = 600, thursday = 600, friday = 600, saturday = 600, sunday = 600 });

        var litetWc = await CreateAreaAsync(http, householdId, "Entré plan – Litet wc");
        var badrum = await CreateAreaAsync(http, householdId, "Övre plan – Badrum");

        // Samma uppgiftsnamn i båda rummen, och båda försenade - exakt det som förr drog ihop dem.
        var wcSink = await CreateTaskAsync(http, householdId, "Torka av handfatet", litetWc);
        var upstairsSink = await CreateTaskAsync(http, householdId, "Torka av handfatet", badrum);
        var wcToilet = await CreateTaskAsync(http, householdId, "Torka av toalettstolen", litetWc);
        var wcFloor = await CreateTaskAsync(http, householdId, "Dammsug golvet", litetWc);

        await ScheduleAsync(http, householdId, wcSink, yesterday, memberId);
        await ScheduleAsync(http, householdId, upstairsSink, yesterday, memberId);
        await ScheduleAsync(http, householdId, wcToilet, today, memberId);
        await ScheduleAsync(http, householdId, wcFloor, today, memberId);

        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Björn" }).WaitForAsync();

        var names = (await page.Locator(".task-name").AllTextContentsAsync())
            .Select(name => name.Trim())
            .ToList();

        // Lilla wc:ts tre uppgifter ligger på rad, och badrummets handfat ligger utanför dem.
        var wcIndexes = new[] { "Torka av handfatet", "Torka av toalettstolen", "Dammsug golvet" }
            .Select(name => names.FindIndex(n => n.Contains(name)))
            .ToList();

        Assert.DoesNotContain(-1, wcIndexes);

        var first = wcIndexes.Min();
        var last = wcIndexes.Max();
        Assert.Equal(2, last - first);

        // Det andra handfatet - samma namn, annan våning - ligger efter hela lilla wc:t.
        var upstairsIndex = names.FindLastIndex(n => n.Contains("Torka av handfatet"));
        Assert.True(upstairsIndex > last, "Badrummets handfat bröt in i lilla wc:ts svit.");
    }
}
