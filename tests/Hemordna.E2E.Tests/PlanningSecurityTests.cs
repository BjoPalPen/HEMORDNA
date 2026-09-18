using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Hemordna.E2E.Tests;
[Collection(HemordnaAppCollection.Name)]
public class PlanningSecurityTests
{
    private readonly HemordnaAppFixture _app;
    public PlanningSecurityTests(HemordnaAppFixture app) => _app = app;
    [Fact]
    public async Task Http_rejects_extreme_inputs_and_future_booking_does_not_hide_todays_plan()
    {
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        var registered = await http.PostAsJsonAsync("/api/auth/register", new {
            email = $"planning-security-{Guid.NewGuid():N}@example.com",
            password = "Hemordna-E2E-2026!", displayName = "Validering" });
        registered.EnsureSuccessStatusCode();
        var auth = await registered.Content.ReadFromJsonAsync<JsonElement>();
        http.DefaultRequestHeaders.Authorization = new("Bearer", auth.GetProperty("token").GetString());
        var created = await http.PostAsJsonAsync("/api/households", new { name = "Säker planering", memberDisplayName = "Validering" });
        created.EnsureSuccessStatusCode();
        var household = await created.Content.ReadFromJsonAsync<JsonElement>();
        var householdId = household.GetProperty("id").GetGuid();
        var memberId = household.GetProperty("members")[0].GetProperty("id").GetGuid();
        var root = $"/api/households/{householdId}";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var extreme = await http.PostAsJsonAsync(root + "/tasks/extra", new { name = "Extrem", estimatedMinutes = int.MaxValue, today });
        Assert.Equal(HttpStatusCode.BadRequest, extreme.StatusCode);
        var taskResponse = await http.PostAsJsonAsync(root + "/tasks", new {
            name = "Diska", estimatedMinutes = 20, defaultResponsibleMemberId = memberId,
            recurrence = new { frequency = "Daily", interval = 1, startDate = today } });
        taskResponse.EnsureSuccessStatusCode();
        var taskId = (await taskResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var updated = await http.PutAsJsonAsync(root + $"/tasks/{taskId}/estimated-minutes", new { estimatedMinutes = int.MaxValue });
        Assert.Equal(HttpStatusCode.BadRequest, updated.StatusCode);
        var maximum = await http.PostAsJsonAsync(root + $"/tasks/{taskId}/occurrences", new { date = DateOnly.MaxValue, assignToMemberId = memberId });
        Assert.Equal(HttpStatusCode.BadRequest, maximum.StatusCode);
        var future = await http.PostAsJsonAsync(root + $"/tasks/{taskId}/occurrences", new { date = today.AddDays(2), assignToMemberId = memberId });
        future.EnsureSuccessStatusCode();
        var plan = await http.GetAsync(root + $"/members/{memberId}/plan?date={today:yyyy-MM-dd}");
        plan.EnsureSuccessStatusCode(); // Includes actual PostgreSQL translation of cursor predicates.
        var body = await plan.Content.ReadAsStringAsync();
        Assert.Contains("Diska", body);
        var repeated = await http.GetAsync(root + $"/members/{memberId}/plan?date={today:yyyy-MM-dd}");
        repeated.EnsureSuccessStatusCode();
    }
}

