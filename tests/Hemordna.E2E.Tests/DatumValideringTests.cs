using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hemordna.E2E.Tests;

/// <summary>
/// A missing date must be rejected at the boundary, not bound to <c>default(DateOnly)</c>.
/// </summary>
/// <remarks>
/// A non-nullable <see cref="DateOnly"/> on a request record cannot tell "absent" from
/// "0001-01-01", so an omitted date used to be accepted and written as year one. These drive
/// the real HTTP surface, because the defect lived in model binding rather than in any type
/// a unit test could reach.
/// </remarks>
[Collection(HemordnaAppCollection.Name)]
public class DatumValideringTests
{
    private readonly HemordnaAppFixture _app;

    public DatumValideringTests(HemordnaAppFixture app) => _app = app;

    private const string Password = "Hemordna-E2E-2026!";

    /// <summary>An account with a household, an area and one task, ready to schedule against.</summary>
    private async Task<(HttpClient Http, Guid HouseholdId, Guid MemberId, Guid TaskId)> ArrangeAsync()
    {
        var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };

        var register = await http.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"validering-{Guid.NewGuid():N}@example.com",
            password = Password,
            displayName = "Validering"
        });
        register.EnsureSuccessStatusCode();

        var token = (await register.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString();
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var household = await http.PostAsJsonAsync("/api/households", new
        {
            name = "Valideringshushållet",
            memberDisplayName = "Validering"
        });
        household.EnsureSuccessStatusCode();

        var created = await household.Content.ReadFromJsonAsync<JsonElement>();
        var householdId = created.GetProperty("id").GetGuid();
        var memberId = created.GetProperty("members")[0].GetProperty("id").GetGuid();

        var task = await http.PostAsJsonAsync($"/api/households/{householdId}/tasks", new
        {
            name = "Diska",
            estimatedMinutes = 20
        });
        task.EnsureSuccessStatusCode();

        var taskId = (await task.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        return (http, householdId, memberId, taskId);
    }

    [Fact]
    public async Task Setting_availability_without_a_date_is_rejected()
    {
        var (http, householdId, memberId, _) = await ArrangeAsync();

        var response = await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/availability",
            new { availableMinutes = 45 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Scheduling_an_occurrence_without_a_date_is_rejected()
    {
        var (http, householdId, _, taskId) = await ArrangeAsync();

        var response = await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deferring_without_a_date_is_rejected()
    {
        var (http, householdId, _, taskId) = await ArrangeAsync();

        var scheduled = await http.PostAsJsonAsync(
            $"/api/households/{householdId}/tasks/{taskId}/occurrences",
            new { date = new DateOnly(2026, 9, 4) });
        scheduled.EnsureSuccessStatusCode();

        var occurrenceId = (await scheduled.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var response = await http.PostAsJsonAsync(
            $"/api/households/{householdId}/occurrences/{occurrenceId}/defer",
            new { });

        // Without the guard the domain answered 409 about ordering, which hid the real cause.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_supplied_date_is_still_honoured()
    {
        var (http, householdId, memberId, _) = await ArrangeAsync();

        var response = await http.PutAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/availability",
            new { date = new DateOnly(2026, 9, 4), availableMinutes = 45 });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-09-04", body.GetProperty("date").GetString());
        Assert.Equal(45, body.GetProperty("availableMinutes").GetInt32());
    }
}
