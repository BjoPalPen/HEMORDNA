using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// En stor eftersläpning får inte läsas som en vägg av misslyckanden ("Beslut: Ångra och stabil
/// lista" §B6). Tidigare löstes det med ett tak på "Sedan tidigare"-gruppen. Den gruppen finns
/// inte längre - hela dagen ligger i rumsordning (se docs/ARCHITECTURE.md "Beslut: hela dagen i
/// rumsordning") - så det som återstår är erbjudandet att sprida ut dem, vilket också var det
/// enda i taket som faktiskt hjälpte.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class OverdueBacklogTests
{
    private readonly HemordnaAppFixture _app;

    public OverdueBacklogTests(HemordnaAppFixture app) => _app = app;

    private static async Task<(HttpClient Http, Guid HouseholdId, Guid MemberId)> ArrangeAsync(IPage page, string apiUrl)
    {
        var token = await AccessTokenHelper.GetAsync(page, apiUrl);
        var http = new HttpClient { BaseAddress = new Uri(apiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 600, tuesday = 600, wednesday = 600, thursday = 600, friday = 600, saturday = 600, sunday = 600 });

        return (http, householdId, memberId);
    }

    [Fact]
    public async Task A_large_backlog_offers_to_spread_itself_out_without_hiding_anything()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Yara");
        var (http, householdId, memberId) = await ArrangeAsync(page, _app.ApiUrl);
        using var _ = http;

        var yesterday = AppDate.Today.AddDays(-1);

        for (var i = 1; i <= 7; i++)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks", new { name = $"Uppgift {i}", estimatedMinutes = 5 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = yesterday, assignToMemberId = memberId });
        }

        await page.ReloadAsync();

        // Erbjudandet finns, med hur många det gäller.
        var notice = page.GetByText("7 uppgifter är sedan tidigare.");
        await Assertions.Expect(notice).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Låt Hemordna sprida ut dem" }))
            .ToBeVisibleAsync();

        // Men ingenting göms: alla sju står kvar i sitt rum. Det gamla taket visade tre och
        // krävde "Visa alla" - det finns inte längre, eftersom en dold rad bryter rumsordningen
        // lika mycket som en felplacerad gör.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { NameRegex = new(@"^Markera Uppgift \d som klar$") }))
            .ToHaveCountAsync(7);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Visa alla" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("ul[aria-label=\"Sedan tidigare\"]")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task A_small_backlog_is_left_alone()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Zoltan");
        var (http, householdId, memberId) = await ArrangeAsync(page, _app.ApiUrl);
        using var _ = http;

        var yesterday = AppDate.Today.AddDays(-1);

        for (var i = 1; i <= 2; i++)
        {
            var task = await (await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks", new { name = $"Syssla {i}", estimatedMinutes = 5 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            await http.PostAsJsonAsync(
                $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
                new { date = yesterday, assignToMemberId = memberId });
        }

        await page.ReloadAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Markera Syssla 1 som klar" }))
            .ToBeVisibleAsync();

        // Två sena uppgifter är ingen eftersläpning - då ska appen inte säga något om det alls.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Låt Hemordna sprida ut dem" }))
            .ToHaveCountAsync(0);
    }
}
