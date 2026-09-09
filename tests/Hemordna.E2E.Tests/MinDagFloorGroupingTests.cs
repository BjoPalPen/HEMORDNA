using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>Product feedback: a floor's rooms should "hold together" on Idag instead of
/// scattering wherever DailyPlanner happened to rank each room's tasks - see
/// docs/ARCHITECTURE.md "Rumsgruppering på Min dag".</summary>
[Collection(HemordnaAppCollection.Name)]
public class MinDagFloorGroupingTests
{
    private readonly HemordnaAppFixture _app;

    public MinDagFloorGroupingTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task Same_floor_rooms_cluster_together_even_when_created_interleaved()
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

        await http.PutAsJsonAsync($"/api/households/{householdId}/members/{memberId}/availability",
            new { date = today, availableMinutes = 120 });

        async Task SeedAsync(string areaName, string taskName)
        {
            var area = await (await http.PostAsJsonAsync($"/api/households/{householdId}/areas",
                new { name = areaName })).Content.ReadFromJsonAsync<JsonElement>();
            var task = await (await http.PostAsJsonAsync($"/api/households/{householdId}/tasks",
                new { name = taskName, estimatedMinutes = 10, areaId = area.GetProperty("id").GetGuid() }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync($"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today, assignToMemberId = memberId });
        }

        // Interleaved on purpose - Entré/Övre/Entré/Övre - so a pass here proves the floors
        // actually cluster, rather than just happening to already be adjacent by luck.
        await SeedAsync("Entré plan – Kök", "Diska");
        await SeedAsync("Övre plan – Sovrum 1", "Vädra rummet");
        await SeedAsync("Entré plan – Hall", "Torka trappsteg");
        await SeedAsync("Övre plan – Hall", "Dammsug golvet");

        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Björn" }).WaitForAsync();

        var headings = page.Locator("h2.floor-heading, .task-group-heading span:first-child");
        await Assertions.Expect(headings).ToHaveCountAsync(6);
        var texts = (await headings.AllInnerTextsAsync()).ToArray();

        // Both rooms of one floor appear as a contiguous block, immediately followed by the
        // other floor's heading and its own two rooms - never interleaved between floors.
        var floorHeadingIndexes = texts
            .Select((text, index) => (text, index))
            .Where(pair => pair.text is "Övre plan" or "Entré plan")
            .Select(pair => pair.index)
            .ToList();
        Assert.Equal(2, floorHeadingIndexes.Count);
        Assert.Equal(0, floorHeadingIndexes[0]);
        Assert.Equal(3, floorHeadingIndexes[1]);

        var firstFloorRooms = texts[1..3];
        var secondFloorRooms = texts[4..6];
        Assert.Equal(["HALL", "SOVRUM 1"], firstFloorRooms.Order());
        Assert.Equal(["HALL", "KÖK"], secondFloorRooms.Order());

        // The floor prefix never appears in full, and a grouped row does not repeat its own
        // room heading as a chip (see MinDagDetailTests) - "Hall" only ever appears as a room
        // heading, twice (once per floor), never as "Övre plan – Hall" or a per-row chip.
        // ".chip-today" ("Börja här", Sju enkla lösningar del 3) is excluded here on purpose -
        // it is unrelated to room chips and expected on the planner's first task whenever more
        // than one task is outstanding, which is exactly this test's own setup.
        await Assertions.Expect(page.GetByText("Övre plan – Hall")).Not.ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("span.chip:not(.chip-today)")).ToHaveCountAsync(0);
    }
}
