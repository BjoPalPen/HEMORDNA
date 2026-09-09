using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Beslut: Ångra och stabil lista" §B7 - "Flytta till en annan dag" is always
/// rendered, never conditionally hidden, so the chip row's layout never reflows depending on
/// what happens to be true today.</summary>
[Collection(HemordnaAppCollection.Name)]
public class AlwaysVisibleChipTests
{
    private readonly HemordnaAppFixture _app;

    public AlwaysVisibleChipTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task With_nothing_unplanned_the_chip_stays_but_answers_with_a_status_line()
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

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks", new { name = "Diska", estimatedMinutes = 5 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Markera Diska som klar" })).ToBeVisibleAsync();

        // Nothing unplanned today - the chip is still there, just marked disabled for assistive
        // tech, and stays reachable/clickable (aria-disabled, not the disabled attribute).
        var moveChip = page.GetByRole(AriaRole.Button, new() { Name = "Flytta till en annan dag" });
        await Assertions.Expect(moveChip).ToBeVisibleAsync();
        await Assertions.Expect(moveChip).ToHaveAttributeAsync("aria-disabled", "true");

        // Force: aria-disabled (unlike the disabled attribute) does not block a real pointer
        // click in an actual browser - only Playwright's own actionability heuristic treats it
        // as non-interactive. The whole point of choosing aria-disabled here (docs/
        // ARCHITECTURE.md §B7) is that the chip stays truly clickable/focusable.
        await moveChip.ClickAsync(new() { Force = true });

        var notice = page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Inget att flytta just nu." });
        await Assertions.Expect(notice).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog, new() { Name = "Flytta till en annan dag" })).Not.ToBeVisibleAsync();
        await Assertions.Expect(notice).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
    }

    /// <summary>Regression test, reported from a real user's own screenshot: at 390px "Ta ledigt
    /// idag" wrapped alone onto its own row below "Flytta till en annan dag"/"Extra uppgift" -
    /// the same "never let one item spill alone" fix as .level-picker and .energy-options
    /// (docs/ARCHITECTURE.md §10, Stor text is unconditional), just shrink-to-fit here rather
    /// than equal thirds since "Flytta till en annan dag" is meaningfully longer than the other
    /// two.</summary>
    [Theory]
    [InlineData("Text (standard) - kompakt lista")]
    [InlineData("Stor text - större och tydligare")]
    public async Task All_three_day_chips_stay_on_one_row_at_390px(string presentationLabel)
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await SignUpHelper.SignUpAsync(page, "Nils");

        if (presentationLabel.StartsWith("Stor text", StringComparison.Ordinal))
        {
            await page.GotoAsync("/installningar");
            await page.GetByLabel(presentationLabel).CheckAsync();
            await Assertions.Expect(page.GetByText("Sparat")).ToBeVisibleAsync(new() { Timeout = 5_000 });
            await page.GotoAsync("/");
        }

        await page.Locator("h1", new() { HasText = "Nils" }).WaitForAsync();

        var flytta = await page.GetByRole(AriaRole.Button, new() { Name = "Flytta till en annan dag" }).BoundingBoxAsync();
        var extra = await page.GetByRole(AriaRole.Button, new() { Name = "Extra uppgift" }).BoundingBoxAsync();
        var ledigt = await page.GetByRole(AriaRole.Button, new() { Name = "Ta ledigt idag" }).BoundingBoxAsync();

        Assert.NotNull(flytta);
        Assert.NotNull(extra);
        Assert.NotNull(ledigt);
        Assert.Equal(flytta!.Y, extra!.Y);
        Assert.Equal(extra.Y, ledigt!.Y);
    }
}
