using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

[Collection(HemordnaAppCollection.Name)]
public class AuthSecurityTests
{
    private const string Password = "Security-Test-Password-2026!";
    private const string NewPassword = "Security-New-Password-2026!";
    private readonly HemordnaAppFixture _app;

    public AuthSecurityTests(HemordnaAppFixture app) => _app = app;

    private async Task<(HttpClient Http, string Email, string Token)> RegisterAsync()
    {
        var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        var email = $"e2e-security-{Guid.NewGuid():N}@example.com";
        using var response = await http.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, displayName = "Security" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return (http, email, token);
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_account_even_for_the_correct_password()
    {
        var (http, email, _) = await RegisterAsync();
        using (http)
        {
            for (var i = 0; i < 5; i++)
            {
                using var failed = await http.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong" });
                Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            }
            using var locked = await http.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
            Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        }
    }

    [Fact]
    public async Task Successful_login_resets_failed_attempts()
    {
        var (http, email, _) = await RegisterAsync();
        using (http)
        {
            for (var round = 0; round < 2; round++)
            {
                for (var i = 0; i < 4; i++)
                {
                    using var failed = await http.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong" });
                    Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
                }
                using var success = await http.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
                Assert.Equal(HttpStatusCode.OK, success.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Password_change_revokes_old_tokens_and_returns_a_working_replacement()
    {
        var (http, _, oldToken) = await RegisterAsync();
        using (http)
        {
            using var changed = await http.PostAsJsonAsync("/api/auth/change-password",
                new { currentPassword = Password, newPassword = NewPassword });
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            var body = await changed.Content.ReadFromJsonAsync<JsonElement>();
            using var oldSession = await http.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
            var replacement = body.GetProperty("token").GetString()!;
            Assert.NotEqual(oldToken, replacement);
            http.DefaultRequestHeaders.Authorization = new("Bearer", replacement);
            using var newSession = await http.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.OK, newSession.StatusCode);
        }
    }

    [Fact]
    public async Task Password_reset_revokes_previously_issued_tokens()
    {
        var (http, email, _) = await RegisterAsync();
        using (http)
        {
            using var requested = await http.PostAsJsonAsync("/api/auth/forgot-password", new { email });
            Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
            var html = await http.GetStringAsync($"/api/auth/dev/last-email?email={Uri.EscapeDataString(email)}");
            var link = new Uri(WebUtility.HtmlDecode(Regex.Match(html, "href=\"([^\"]+)\"").Groups[1].Value));
            var query = System.Web.HttpUtility.ParseQueryString(link.Query);
            using var reset = await http.PostAsJsonAsync("/api/auth/reset-password",
                new { email, token = query["token"], newPassword = NewPassword });
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
            using var oldSession = await http.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        }
    }

    [Fact]
    public async Task Failed_role_preset_shows_an_error_and_preserves_saved_capacity()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "RoleFailure");
        await page.GotoAsync("/hushall");
        await HushallHelper.AddMemberWithoutAccountAsync(page, "Nils", "Vuxen, jobbar heltid");
        var sheet = await HushallHelper.OpenMemberSheetAsync(page, "Nils");
        await page.RouteAsync("**/members/*/role", route => route.FulfillAsync(new()
        {
            Status = 500, ContentType = "application/problem+json", Body = "{}"
        }));
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Pensionär / hemma dagtid", Exact = true }).ClickAsync();
        await Assertions.Expect(sheet.GetByText("Det gick inte att spara. Försök igen.")).ToBeVisibleAsync();
        await page.UnrouteAsync("**/members/*/role");
        await page.ReloadAsync();
        sheet = await HushallHelper.OpenMemberSheetAsync(page, "Nils");
        await sheet.GetByText("Anpassa tid per veckodag").ClickAsync();
        await Assertions.Expect(sheet.GetByLabel("Måndag", new() { Exact = true })).ToHaveValueAsync("35");
    }

    [Fact]
    public async Task Invalid_capacity_rejects_the_entire_role_preset()
    {
        var (http, _, _) = await RegisterAsync();
        using (http)
        {
            using var created = await http.PostAsJsonAsync("/api/households", new { name = "Atomic" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var household = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = household.GetProperty("id").GetGuid();
            var member = household.GetProperty("members")[0];
            var memberId = member.GetProperty("id").GetGuid();
            using var invalid = await http.PutAsJsonAsync($"/api/households/{id}/members/{memberId}/role", new
            {
                role = "Retired",
                weeklyTimeBudgetMinutes = new { monday = -1, tuesday = 65, wednesday = 65, thursday = 65, friday = 65, saturday = 65, sunday = 65 },
                weeklyEffortCeiling = new { monday = "Heavy", tuesday = "Heavy", wednesday = "Heavy", thursday = "Heavy", friday = "Heavy", saturday = "Heavy", sunday = "Heavy" }
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var saved = await http.GetFromJsonAsync<JsonElement>($"/api/households/{id}");
            var savedMember = saved.GetProperty("members")[0];
            Assert.Equal(member.GetProperty("role").ToString(), savedMember.GetProperty("role").ToString());
            Assert.Equal(member.GetProperty("weeklyTimeBudgetMinutes").ToString(), savedMember.GetProperty("weeklyTimeBudgetMinutes").ToString());
        }
    }
}
