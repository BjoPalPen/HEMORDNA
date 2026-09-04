using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hemordna.Client.Contracts;

namespace Hemordna.Client.Services;

/// <summary>
/// The client's only route to the server. It owns the HTTP contract and nothing else -
/// no business rules live here.
/// </summary>
public sealed class HemordnaApiClient
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;

    public HemordnaApiClient(HttpClient http, TokenStore tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    /// <summary>Signs in and stores the token. Returns false on wrong e-mail or password.</summary>
    public async Task<bool> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync(
            "api/auth/login", new { email, password }, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken);

        if (token is null)
        {
            return false;
        }

        await _tokens.SetAsync(token.Token);
        return true;
    }

    /// <summary>Registers a new user and signs them in. Returns the API's messages on failure.</summary>
    public async Task<IReadOnlyList<string>> RegisterAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync(
            "api/auth/register", new { email, password, displayName }, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken);

            if (token is not null)
            {
                await _tokens.SetAsync(token.Token);
                return [];
            }
        }

        return await ReadProblemMessagesAsync(response, cancellationToken);
    }

    public async Task SignOutAsync() => await _tokens.ClearAsync();

    /// <summary>The signed-in user, or <c>null</c> when the token is missing or no longer valid.</summary>
    public async Task<MeResponse?> GetMeAsync(CancellationToken cancellationToken = default)
        => await GetAsync<MeResponse>("api/me", cancellationToken);

    public async Task<HouseholdResponse?> GetHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
        => await GetAsync<HouseholdResponse>($"api/households/{householdId}", cancellationToken);

    public async Task<HouseholdResponse?> CreateHouseholdAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(HttpMethod.Post, "api/households", cancellationToken);
        request.Content = JsonContent.Create(new { name });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<HouseholdResponse>(cancellationToken)
            : null;
    }

    public async Task<DailyPlanResponse?> GetDailyPlanAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken = default)
        => await GetAsync<DailyPlanResponse>(
            $"api/households/{householdId}/members/{memberId}/plan?date={date:yyyy-MM-dd}",
            cancellationToken);

    /// <summary>Marks a task done. Safe to call twice - the server keeps the first completion.</summary>
    public async Task<bool> CompleteOccurrenceAsync(
        Guid householdId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post,
            $"api/households/{householdId}/occurrences/{occurrenceId}/complete",
            cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeferOccurrenceAsync(
        Guid householdId,
        Guid occurrenceId,
        DateOnly newDate,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post,
            $"api/households/{householdId}/occurrences/{occurrenceId}/defer",
            cancellationToken);
        request.Content = JsonContent.Create(new { date = newDate.ToString("yyyy-MM-dd") });

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Sets "less time today" for a member, without changing their weekly budget.</summary>
    public async Task<bool> SetAvailabilityAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        int availableMinutes,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put,
            $"api/households/{householdId}/members/{memberId}/availability",
            cancellationToken);
        request.Content = JsonContent.Create(
            new { date = date.ToString("yyyy-MM-dd"), availableMinutes });

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var request = await AuthorizedAsync(HttpMethod.Get, path, cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);

        // 404 is a legitimate answer here, not a failure: the caller asked for something
        // that does not exist, or that they may not see.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task<HttpRequestMessage> AuthorizedAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);

        if (await _tokens.GetAsync() is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static async Task<IReadOnlyList<string>> ReadProblemMessagesAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ValidationProblem>(cancellationToken);

            if (problem?.Errors is { Count: > 0 } errors)
            {
                return [.. errors.SelectMany(pair => pair.Value)];
            }

            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return [problem.Detail];
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or NotSupportedException
            or System.Text.Json.JsonException)
        {
            // Fall through to the generic message below.
        }

        return ["Något gick fel. Försök igen."];
    }

    private sealed record ValidationProblem(string? Detail, Dictionary<string, string[]>? Errors);
}
