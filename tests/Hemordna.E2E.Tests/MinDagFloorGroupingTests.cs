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

        async Task SeedAsync(string floor, string areaName, string taskName)
        {
            var area = await (await http.PostAsJsonAsync($"/api/households/{householdId}/areas",
                new { name = areaName, floor })).Content.ReadFromJsonAsync<JsonElement>();
            var task = await (await http.PostAsJsonAsync($"/api/households/{householdId}/tasks",
                new { name = taskName, estimatedMinutes = 10, areaId = area.GetProperty("id").GetGuid() }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync($"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = today, assignToMemberId = memberId });
        }

        // Interleaved on purpose - Entré/Övre/Entré/Övre - so a pass here proves the floors
        // actually cluster, rather than just happening to already be adjacent by luck. Floor is
        // its own field now (see Area.Floor), not baked into the area's name - the two "Hall"
        // rooms below share a name but are on different floors, which is exactly the case
        // Household.AddArea's (Floor, Name) uniqueness rule exists to allow.
        await SeedAsync("Entré plan", "Kök", "Diska");
        await SeedAsync("Övre plan", "Sovrum 1", "Vädra rummet");
        await SeedAsync("Entré plan", "Hall", "Torka trappsteg");
        await SeedAsync("Övre plan", "Hall", "Dammsug golvet");

        await page.GotoAsync("/");
        await page.Locator("h1", new() { HasText = "Björn" }).WaitForAsync();

        var headings = page.Locator("h2.floor-heading, .task-group-heading span:first-child");
        await Assertions.Expect(headings).ToHaveCountAsync(6);
        var texts = (await headings.AllInnerTextsAsync()).ToArray();

        // Hela följden, inte bara att våningarna håller ihop. Sedan "Beslut: hela dagen i
        // rumsordning" följer ordningen hushållets EGEN rumsordning - samma som Rum visar - i
        // stället för vilket rum DailyPlanner råkade ranka först den morgonen. Rummen såddes
        // Kök, Sovrum 1, Hall(E), Hall(Ö), så Entré plan leder (dess första rum kom först) och
        // inom våningen kommer Kök före Hall. Att kunna påstå exakt detta ÄR poängen: en
        // ordning som byter från dag till dag går inte att lita på när man står i ett rum.
        Assert.Equal(
            ["Entré plan", "KÖK", "HALL", "Övre plan", "SOVRUM 1", "HALL"],
            texts);

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
