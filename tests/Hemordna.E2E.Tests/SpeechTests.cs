using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Läs upp" (Sju enkla lösningar, del 6) - focus mode only, reads the current task's
/// name, room and (if chosen) time out loud via the Web Speech API (Support/Speech.cs).</summary>
[Collection(HemordnaAppCollection.Name)]
public class SpeechTests
{
    private readonly HemordnaAppFixture _app;

    public SpeechTests(HemordnaAppFixture app) => _app = app;

    /// <summary>Overrides the real speechSynthesis (present in headless Chromium, just silent)
    /// rather than replacing window.speechSynthesis outright - that property is not reliably
    /// reassignable across browsers, but its own methods are ordinary, overridable functions.
    /// Records every spoken text into window.__speechCalls and immediately fires the utterance's
    /// own onend, exactly like a real, very fast voice would.</summary>
    private static Task FakeSpeechSynthesisAsync(IPage page) => page.AddInitScriptAsync(@"
        window.__speechCalls = [];
        if ('speechSynthesis' in window) {
            window.speechSynthesis.getVoices = () => [{ lang: 'sv-SE', name: 'Fejk svenska' }];
            window.speechSynthesis.speak = (utterance) => {
                window.__speechCalls.push(utterance.text);
                if (utterance.onend) { utterance.onend(); }
            };
            window.speechSynthesis.cancel = () => {};
        }
    ");

    [Fact]
    public async Task Pressing_las_upp_speaks_the_tasks_name_and_room()
    {
        var page = await _app.NewPageAsync();
        await FakeSpeechSynthesisAsync(page);
        await SignUpHelper.SignUpAsync(page, "Nils");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/preferences",
            new { presentation = "OneAtATime", motivation = "None", showTimeLevel = false });

        var area = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/areas", new { name = "Kök" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Diska", estimatedMinutes = 5, areaId = area.GetProperty("id").GetGuid() }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();

        var speakButton = page.GetByRole(AriaRole.Button, new() { Name = "Läs upp" });
        await Assertions.Expect(speakButton).ToBeVisibleAsync();
        await speakButton.ClickAsync();

        var spoken = await page.EvaluateAsync<string[]>("() => window.__speechCalls");
        Assert.Single(spoken);
        Assert.Contains("Diska", spoken[0]);
        Assert.Contains("Kök", spoken[0]);

        // The fake voice fires onend synchronously (see FakeSpeechSynthesisAsync), so the button
        // is already back to "Läs upp" rather than stuck on "Tyst".
        await Assertions.Expect(speakButton).ToHaveTextAsync("Läs upp");
    }

    [Fact]
    public async Task Steps_are_read_as_numbered_steps()
    {
        var page = await _app.NewPageAsync();
        await FakeSpeechSynthesisAsync(page);
        await SignUpHelper.SignUpAsync(page, "Freja");

        var token = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await (await http.GetAsync("/api/me")).Content.ReadFromJsonAsync<JsonElement>();
        var householdId = me.GetProperty("householdId").GetGuid();
        var memberId = me.GetProperty("memberId").GetGuid();

        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/weekly-budget",
            new { monday = 60, tuesday = 60, wednesday = 60, thursday = 60, friday = 60, saturday = 60, sunday = 60 });
        await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/preferences",
            new { presentation = "OneAtATime", motivation = "None", showTimeLevel = false });

        var task = await (await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks",
            new { name = "Skura golvet", estimatedMinutes = 15, description = "Ta fram hinken\nTorka" }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{task.GetProperty("id").GetGuid()}/occurrences",
            new { date = today, assignToMemberId = memberId });

        await page.ReloadAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Läs upp" }).ClickAsync();

        var spoken = await page.EvaluateAsync<string[]>("() => window.__speechCalls");
        Assert.Single(spoken);
        Assert.Contains("Steg 1: Ta fram hinken.", spoken[0]);
        Assert.Contains("Steg 2: Torka.", spoken[0]);
    }
}
