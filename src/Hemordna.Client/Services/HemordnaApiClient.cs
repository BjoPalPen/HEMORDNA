using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    /// <summary>
    /// Requests a password-reset e-mail. Always looks like it succeeded to the caller - the
    /// API gives the same response whether or not the address has an account, so this cannot
    /// be used to find out which e-mail addresses are registered.
    /// </summary>
    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
        => await _http.PostAsJsonAsync("api/auth/forgot-password", new { email }, cancellationToken);

    /// <summary>Sets a new password from a reset link. Returns the API's messages on failure.</summary>
    public async Task<IReadOnlyList<string>> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync(
            "api/auth/reset-password", new { email, token, newPassword }, cancellationToken);

        return response.IsSuccessStatusCode
            ? []
            : await ReadProblemMessagesAsync(response, cancellationToken);
    }

    /// <summary>Changes the signed-in user's password. Returns the API's messages on failure.</summary>
    public async Task<IReadOnlyList<string>> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(HttpMethod.Post, "api/auth/change-password", cancellationToken);
        request.Content = JsonContent.Create(new { currentPassword, newPassword });

        var response = await _http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return await ReadProblemMessagesAsync(response, cancellationToken);
        }

        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken);
        if (token is null)
        {
            return ["Kunde inte uppdatera inloggningen. Logga in igen."];
        }

        await _tokens.SetAsync(token.Token);
        return [];
    }

    public async Task<IReadOnlyList<PasskeyResponse>> ListPasskeysAsync(CancellationToken cancellationToken = default)
        => await GetAsync<IReadOnlyList<PasskeyResponse>>("api/auth/passkeys/", cancellationToken) ?? [];

    /// <summary>The raw options JSON for <see cref="WebAuthnClient.RegisterAsync"/> - opaque
    /// to this layer, see its own remarks.</summary>
    public async Task<string?> GetPasskeyRegisterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(HttpMethod.Post, "api/auth/passkeys/register/options", cancellationToken);
        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsStringAsync(cancellationToken)
            : null;
    }

    /// <summary>Completes a passkey registration. <paramref name="attestationJson"/> is the
    /// opaque JSON <see cref="WebAuthnClient.RegisterAsync"/> returned, sent as-is: the server
    /// deserializes it itself, deliberately bypassing this app's own JSON options - see
    /// PasskeyEndpoints.Fido2JsonOptions for why.</summary>
    public async Task<IReadOnlyList<string>> VerifyPasskeyRegisterAsync(
        string attestationJson, CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(HttpMethod.Post, "api/auth/passkeys/register/verify", cancellationToken);
        request.Content = new StringContent(attestationJson, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? []
            : await ReadProblemMessagesAsync(response, cancellationToken);
    }

    public async Task<bool> DeletePasskeyAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(HttpMethod.Delete, $"api/auth/passkeys/{credentialId}", cancellationToken);
        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// No e-mail or username needed - the server hands back an empty-allow-list challenge and
    /// lets the browser itself offer whichever discoverable passkey it has for this site, plus
    /// a flow id that ties this challenge to the matching <see cref="LoginWithPasskeyAsync"/>
    /// call. <c>null</c> only on a network problem.
    /// </summary>
    public async Task<PasskeyLoginOptions?> GetPasskeyLoginOptionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsync("api/auth/passkeys/login/options", content: null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        // The envelope PasskeyEndpoints.GetLoginOptionsAsync hand-builds: {"flowId":"...",
        // "options":{...}} - "options" is pulled back out as raw text so WebAuthnClient still
        // sees exactly the same JSON shape it always has, untouched by this unwrapping.
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return new PasskeyLoginOptions(
            envelope.GetProperty("flowId").GetString()!,
            envelope.GetProperty("options").GetRawText());
    }

    /// <summary>Signs in with a passkey and stores the token, mirroring
    /// <see cref="LoginAsync(string, string, CancellationToken)"/>. <paramref name="assertionJson"/> is the
    /// opaque JSON <see cref="WebAuthnClient.AuthenticateAsync"/> returned, sent as-is - see
    /// VerifyPasskeyRegisterAsync's remarks.</summary>
    public async Task<bool> LoginWithPasskeyAsync(
        string flowId, string assertionJson, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsync(
            $"api/auth/passkeys/login/verify?flowId={Uri.EscapeDataString(flowId)}",
            new StringContent(assertionJson, Encoding.UTF8, "application/json"),
            cancellationToken);

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

    /// <summary>Joins an existing household via its invite code, instead of creating a new one.</summary>
    public async Task<JoinHouseholdOutcome> JoinHouseholdAsync(
        string inviteCode,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(HttpMethod.Post, "api/households/join", cancellationToken);
        request.Content = JsonContent.Create(new JoinHouseholdRequest(inviteCode));

        var response = await _http.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return JoinHouseholdOutcome.ForSuccess(
                await response.Content.ReadFromJsonAsync<HouseholdResponse>(cancellationToken));
        }

        return response.StatusCode switch
        {
            HttpStatusCode.NotFound => JoinHouseholdOutcome.ForFailure(JoinHouseholdError.InvalidCode),
            HttpStatusCode.Conflict => JoinHouseholdOutcome.ForFailure(JoinHouseholdError.AlreadyInHousehold),
            _ => JoinHouseholdOutcome.ForFailure(JoinHouseholdError.Unknown)
        };
    }

    /// <summary>Issues a fresh invite code, so one shared with the wrong person stops working.</summary>
    public async Task<HouseholdResponse?> RegenerateInviteCodeAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/invite-code/regenerate", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<HouseholdResponse>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Wipes every room, task and history row, keeping the household and its members - see
    /// docs/ARCHITECTURE.md "Beslut: Rensa ett hushåll". Irreversible; the caller is
    /// responsible for confirming with whoever asked for it before calling this.
    /// </summary>
    public async Task<HouseholdResponse?> ResetHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/reset", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<HouseholdResponse>(cancellationToken)
            : null;
    }

    public async Task<IReadOnlyList<TaskDefinitionResponse>> ListTasksAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
        => await GetAsync<IReadOnlyList<TaskDefinitionResponse>>(
            $"api/households/{householdId}/tasks", cancellationToken) ?? [];

    public async Task<TaskDefinitionResponse?> CreateTaskAsync(
        Guid householdId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var httpRequest = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/tasks", cancellationToken);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _http.SendAsync(httpRequest, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDefinitionResponse>(cancellationToken)
            : null;
    }

    /// <summary>
    /// "Extra uppgift" on Min dag - creates the task and schedules it on the caller themselves,
    /// today, in one call. Open to every member, unlike <see cref="CreateTaskAsync"/> - see
    /// docs/ARCHITECTURE.md "Beslut: Vem får ändra vad". <paramref name="today"/> is this
    /// device's own local date - see <see cref="CompleteOccurrenceAsync"/>'s own remarks for why.
    /// </summary>
    public async Task<TaskOccurrenceResponse?> CreateExtraTaskAsync(
        Guid householdId,
        string name,
        int estimatedMinutes,
        string? description,
        Guid? areaId,
        DateOnly? today = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/tasks/extra", cancellationToken);
        request.Content = JsonContent.Create(
            new CreateExtraTaskRequest(name, estimatedMinutes, description, areaId, today));

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskOccurrenceResponse>(cancellationToken)
            : null;
    }

    /// <summary>Deactivates the task rather than deleting it - see TaskDefinition for why.</summary>
    public async Task<bool> DeactivateTaskAsync(
        Guid householdId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Delete, $"api/households/{householdId}/tasks/{taskId}", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Changes how often a task repeats - the same shape a household would otherwise only get
    /// to pick once, at creation. <paramref name="recurrence"/> and <paramref name="staleAfterDays"/>
    /// are mutually exclusive; passing one clears the other server-side.
    /// </summary>
    public async Task<TaskDefinitionResponse?> UpdateTaskFrequencyAsync(
        Guid householdId,
        Guid taskId,
        RecurrenceRuleContract? recurrence,
        int? staleAfterDays,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/tasks/{taskId}/frequency", cancellationToken);
        request.Content = JsonContent.Create(new { recurrence, staleAfterDays });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDefinitionResponse>(cancellationToken)
            : null;
    }

    /// <summary>Changes who normally owns a task - a specific member, or null to rotate between everyone.</summary>
    public async Task<TaskDefinitionResponse?> UpdateTaskAssignmentAsync(
        Guid householdId,
        Guid taskId,
        Guid? memberId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/tasks/{taskId}/assignment", cancellationToken);
        request.Content = JsonContent.Create(new { memberId });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDefinitionResponse>(cancellationToken)
            : null;
    }

    /// <summary>Moves a task to a different room, or null for "Övrigt".</summary>
    public async Task<TaskDefinitionResponse?> MoveTaskToAreaAsync(
        Guid householdId,
        Guid taskId,
        Guid? areaId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/tasks/{taskId}/area", cancellationToken);
        request.Content = JsonContent.Create(new { areaId });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDefinitionResponse>(cancellationToken)
            : null;
    }

    public async Task<TaskDefinitionResponse?> SetTaskRequiresAdultAsync(
        Guid householdId,
        Guid taskId,
        bool requiresAdult,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/tasks/{taskId}/requires-adult", cancellationToken);
        request.Content = JsonContent.Create(new { requiresAdult });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDefinitionResponse>(cancellationToken)
            : null;
    }

    public async Task<TaskDefinitionResponse?> ChangeTaskEstimatedMinutesAsync(
        Guid householdId,
        Guid taskId,
        int estimatedMinutes,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/tasks/{taskId}/estimated-minutes", cancellationToken);
        request.Content = JsonContent.Create(new { estimatedMinutes });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDefinitionResponse>(cancellationToken)
            : null;
    }

    /// <summary>How much the task takes out of whoever does it - see TaskEffort. Household
    /// configuration, same authorization as time and frequency.</summary>
    public async Task<TaskDefinitionResponse?> ChangeTaskEffortAsync(
        Guid householdId,
        Guid taskId,
        string effort,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/tasks/{taskId}/effort", cancellationToken);
        request.Content = JsonContent.Create(new { effort });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDefinitionResponse>(cancellationToken)
            : null;
    }

    /// <summary>"Alltid på en viss veckodag" - null clears the lock. Household configuration,
    /// same authorization as frequency. A 409 (task has no weekly/monthly recurrence to lock
    /// to) surfaces as a null return, same as a 404 - the caller only needs to know it failed.
    /// <paramref name="today"/> is this device's own local date - see
    /// <see cref="CompleteOccurrenceAsync"/>'s own remarks for why; it is the exact date the
    /// re-anchored recurrence is anchored from.</summary>
    public async Task<TaskDefinitionResponse?> SetTaskPreferredWeekdayAsync(
        Guid householdId,
        Guid taskId,
        string? weekday,
        DateOnly? today = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/tasks/{taskId}/preferred-weekday", cancellationToken);
        request.Content = JsonContent.Create(new { weekday, today });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TaskDefinitionResponse>(cancellationToken)
            : null;
    }

    public async Task<AreaResponse?> RenameAreaAsync(
        Guid householdId,
        Guid areaId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/areas/{areaId}/name", cancellationToken);
        request.Content = JsonContent.Create(new { name });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AreaResponse>(cancellationToken)
            : null;
    }

    /// <summary>Moves an area to a (possibly different) floor, or clears it when
    /// <paramref name="floor"/> is null - see Domain's Household.SetAreaFloor.</summary>
    public async Task<AreaResponse?> SetAreaFloorAsync(
        Guid householdId,
        Guid areaId,
        string? floor,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/areas/{areaId}/floor", cancellationToken);
        request.Content = JsonContent.Create(new SetAreaFloorRequest(floor));

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AreaResponse>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Re-anchors already-created recurring tasks so they spread across the week instead of
    /// clustering on whichever day they were created - see RebalanceSchedule.
    /// </summary>
    public async Task<RebalanceScheduleResponse?> RebalanceScheduleAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/tasks/rebalance-schedule", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RebalanceScheduleResponse>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Reassigns already-outstanding rotating occurrences to match each active member's current
    /// share of the household's capacity - see RebalanceTaskAssignments.
    /// </summary>
    public async Task<RebalanceAssignmentsResponse?> RebalanceAssignmentsAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/tasks/rebalance-assignments", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RebalanceAssignmentsResponse>(cancellationToken)
            : null;
    }

    /// <summary>
    /// Refreshes members whose weekly budget still matches an old role-preset formula to the
    /// current one - see RefreshRolePresetBudgets.
    /// </summary>
    public async Task<RefreshRoleBudgetsResponse?> RefreshRoleBudgetsAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/members/refresh-role-budgets", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RefreshRoleBudgetsResponse>(cancellationToken)
            : null;
    }

    public async Task<bool> ScheduleOccurrenceAsync(
        Guid householdId,
        Guid taskId,
        DateOnly date,
        Guid? assignToMemberId,
        bool addedAsExtra = false,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/tasks/{taskId}/occurrences", cancellationToken);
        request.Content = JsonContent.Create(
            new { date = date.ToString("yyyy-MM-dd"), assignToMemberId, addedAsExtra });

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<AreaResponse?> AddAreaAsync(
        Guid householdId,
        string name,
        string? floor = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(HttpMethod.Post, $"api/households/{householdId}/areas", cancellationToken);
        request.Content = JsonContent.Create(new AddAreaRequest(name, floor));

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AreaResponse>(cancellationToken)
            : null;
    }

    public async Task<HouseholdMemberResponse?> AddMemberAsync(
        Guid householdId,
        string displayName,
        WeeklyTimeBudgetContract? weeklyTimeBudget,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(HttpMethod.Post, $"api/households/{householdId}/members", cancellationToken);
        request.Content = JsonContent.Create(new AddMemberRequest(displayName, weeklyTimeBudget, role));

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<HouseholdMemberResponse>(cancellationToken)
            : null;
    }

    /// <summary>Sets a role and optionally its capacity preset in one atomic update.</summary>
    public async Task<bool> SetMemberRoleAsync(
        Guid householdId,
        Guid memberId,
        string? role,
        CancellationToken cancellationToken = default,
        WeeklyTimeBudgetContract? weeklyTimeBudget = null,
        WeeklyEffortCeilingContract? weeklyEffortCeiling = null)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/members/{memberId}/role", cancellationToken);
        request.Content = JsonContent.Create(new SetMemberRoleRequest(role, weeklyTimeBudget, weeklyEffortCeiling));

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Pauses the whole household through and including <paramref name="until"/>, or resumes it when <c>null</c>.</summary>
    public async Task<bool> PauseHouseholdAsync(
        Guid householdId,
        DateOnly? until,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/pause", cancellationToken);
        request.Content = JsonContent.Create(new PauseRequest(until));

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Pauses one member through and including <paramref name="until"/>, or resumes them when <c>null</c>.</summary>
    public async Task<bool> PauseHouseholdMemberAsync(
        Guid householdId,
        Guid memberId,
        DateOnly? until,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/members/{memberId}/pause", cancellationToken);
        request.Content = JsonContent.Create(new PauseRequest(until));

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Pauses one room through and including <paramref name="until"/>, or resumes it when <c>null</c>.</summary>
    public async Task<bool> PauseAreaAsync(
        Guid householdId,
        Guid areaId,
        DateOnly? until,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/areas/{areaId}/pause", cancellationToken);
        request.Content = JsonContent.Create(new PauseRequest(until));

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Deactivates the area rather than deleting it - see Area for why.</summary>
    public async Task<bool> DeactivateAreaAsync(
        Guid householdId,
        Guid areaId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Delete, $"api/households/{householdId}/areas/{areaId}", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Grants or removes a member's ability to manage the household - see docs/ARCHITECTURE.md
    /// "Beslut: Vem får ändra vad". Only the caller's own household, and only when the caller
    /// already has this ability themselves - enforced server-side, see HouseholdManageFilter.</summary>
    public async Task<bool> SetMemberCanManageHouseholdAsync(
        Guid householdId,
        Guid memberId,
        bool canManage,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/members/{memberId}/can-manage", cancellationToken);
        request.Content = JsonContent.Create(new SetCanManageHouseholdRequest(canManage));

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Deactivates the member rather than deleting them - see HouseholdMember for why.</summary>
    public async Task<bool> DeactivateMemberAsync(
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Delete, $"api/households/{householdId}/members/{memberId}", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>"Planera veckan" - a read-only preview, saves nothing. <paramref name="today"/>
    /// is this device's own local date; the server's own date is used when it is null.
    /// <paramref name="moves"/> is the household member's own pending edits to the suggestion
    /// (Björns krav, "Planera veckan går att ändra") - treated server-side exactly like an
    /// existing "Alltid på" lock. POST, not GET, since moves don't fit a query string - see the
    /// API's own endpoint comment.</summary>
    public async Task<WeeklyPlanResponse?> GetWeeklyPlanAsync(
        Guid householdId,
        DateOnly? today = null,
        IReadOnlyList<WeeklyPlanMoveRequest>? moves = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/weekly-plan/preview", cancellationToken);
        request.Content = JsonContent.Create(new { today, moves });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<WeeklyPlanResponse>(cancellationToken)
            : null;
    }

    /// <summary>"Använd" - writes the plan previewed by GetWeeklyPlanAsync. Gäller framåt; rör
    /// aldrig redan utlagda förekomster - se ApplyWeeklyPlan. <paramref name="moves"/> must be the
    /// same moves the previewed plan was computed with, or the two will disagree.</summary>
    public async Task<ApplyWeeklyPlanResponse?> ApplyWeeklyPlanAsync(
        Guid householdId,
        DateOnly? today = null,
        IReadOnlyList<WeeklyPlanMoveRequest>? moves = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/weekly-plan/apply", cancellationToken);
        request.Content = JsonContent.Create(new { today, moves });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ApplyWeeklyPlanResponse>(cancellationToken)
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

    /// <summary>
    /// Marks a task done. Safe to call twice - the server keeps the first completion.
    /// <paramref name="today"/> is this device's own local date - it decides whether the
    /// completion earns "tid i förväg" (see CompleteTaskOccurrence), and can differ from the
    /// server's own date around midnight. Left null falls back to the server's date.
    /// </summary>
    public async Task<bool> CompleteOccurrenceAsync(
        Guid householdId,
        Guid occurrenceId,
        DateOnly? today = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post,
            $"api/households/{householdId}/occurrences/{occurrenceId}/complete",
            cancellationToken);

        if (today is { } value)
        {
            request.Content = JsonContent.Create(new { today = value.ToString("yyyy-MM-dd") });
        }

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// "Jobba i förväg" for a single occurrence - pulls a not-yet-due task to today. Only the
    /// member it is assigned to may bring it forward. <paramref name="today"/> is this device's
    /// own local date - it decides the "not yet due" boundary, same reasoning as
    /// <see cref="CompleteOccurrenceAsync"/>'s own <c>today</c>. Left null falls back to the
    /// server's date.
    /// </summary>
    public async Task<bool> BringOccurrenceForwardAsync(
        Guid householdId,
        Guid occurrenceId,
        DateOnly? today = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post,
            $"api/households/{householdId}/occurrences/{occurrenceId}/bring-forward",
            cancellationToken);

        if (today is { } value)
        {
            request.Content = JsonContent.Create(new { today = value.ToString("yyyy-MM-dd") });
        }

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Undoes bringing an occurrence forward, putting it back on its original date.</summary>
    public async Task<bool> UndoBringForwardAsync(
        Guid householdId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post,
            $"api/households/{householdId}/occurrences/{occurrenceId}/undo-bring-forward",
            cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Marks a date as the member's own day off. <paramref name="mode"/> is
    /// "BringAllForward" or "DeferAll" - see SetMemberDayOff.DayOffMode.
    /// <paramref name="today"/> is this device's own local date - see
    /// <see cref="CompleteOccurrenceAsync"/>'s own <c>today</c> for why. Left null falls back to
    /// the server's date.
    /// </summary>
    public async Task<DayOffResponse?> SetDayOffAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        string mode,
        DateOnly? today = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put,
            $"api/households/{householdId}/members/{memberId}/days-off/{date:yyyy-MM-dd}",
            cancellationToken);
        request.Content = JsonContent.Create(
            new { mode, today = today?.ToString("yyyy-MM-dd") });

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<DayOffResponse>(cancellationToken)
            : null;
    }

    /// <summary>Clears a member's day off. Whatever already moved out of the way while it was
    /// set stays exactly where it ended up - see ClearMemberDayOff.</summary>
    public async Task<bool> ClearDayOffAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Delete,
            $"api/households/{householdId}/members/{memberId}/days-off/{date:yyyy-MM-dd}",
            cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<MemberDayOffResponse>> GetDaysOffAsync(
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default)
        => await GetAsync<IReadOnlyList<MemberDayOffResponse>>(
            $"api/households/{householdId}/members/{memberId}/days-off", cancellationToken) ?? [];

    /// <summary>
    /// The signed-in member's own "tid i förväg" balance - never anyone else's, see
    /// GetMemberTimeCredit. <paramref name="today"/> is this device's own local date, bounding
    /// the balance's 60-day lookback window - see <see cref="CompleteOccurrenceAsync"/>'s own
    /// <c>today</c> for why. Left null falls back to the server's date.
    /// </summary>
    public async Task<TimeCreditResponse?> GetTimeCreditAsync(
        Guid householdId,
        DateOnly? today = null,
        CancellationToken cancellationToken = default)
        => await GetAsync<TimeCreditResponse>(
            today is { } value
                ? $"api/households/{householdId}/time-credit?today={value:yyyy-MM-dd}"
                : $"api/households/{householdId}/time-credit",
            cancellationToken);

    /// <summary>Undoes a completion - only the member who completed it can, and only within a
    /// short window server-side (see TaskOccurrence.Reopen). A rejected attempt (wrong person,
    /// window closed) comes back as a non-success status, same as any other rule violation.</summary>
    public async Task<bool> ReopenOccurrenceAsync(
        Guid householdId,
        Guid occurrenceId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post,
            $"api/households/{householdId}/occurrences/{occurrenceId}/reopen",
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

    /// <summary>Sets "less time today" for a member, without changing their weekly budget.
    /// <paramref name="effortCeiling"/> is "Hur är orken idag?"'s ceiling ("Light" for "Lite",
    /// null for "Lagom"/"Mycket" or no choice) - sent exactly as given, so a caller that must not
    /// disturb an already-set ceiling (e.g. widening today's minutes for "Extra uppgift") has to
    /// resend it - see EnergyLevel.EffortCeilingFor and MinDag.razor's own call sites.</summary>
    public async Task<bool> SetAvailabilityAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        int availableMinutes,
        string? effortCeiling = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put,
            $"api/households/{householdId}/members/{memberId}/availability",
            cancellationToken);
        request.Content = JsonContent.Create(
            new { date = date.ToString("yyyy-MM-dd"), availableMinutes, effortCeiling });

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Replaces a member's normal weekly time budget - their ordinary week, not a single day.</summary>
    public async Task<bool> SetWeeklyBudgetAsync(
        Guid householdId,
        Guid memberId,
        WeeklyTimeBudgetContract weeklyTimeBudget,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put,
            $"api/households/{householdId}/members/{memberId}/weekly-budget",
            cancellationToken);
        request.Content = JsonContent.Create(weeklyTimeBudget);

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>How much this member takes on per weekday - see WeeklyEffortCeilingContract.
    /// Same authorization as SetWeeklyBudgetAsync.</summary>
    public async Task<bool> SetWeeklyEffortCeilingAsync(
        Guid householdId,
        Guid memberId,
        WeeklyEffortCeilingContract weeklyEffortCeiling,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put,
            $"api/households/{householdId}/members/{memberId}/effort-ceiling",
            cancellationToken);
        request.Content = JsonContent.Create(weeklyEffortCeiling);

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<RecentActivityResponse>> GetRecentActivityAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
        => await GetAsync<IReadOnlyList<RecentActivityResponse>>(
            $"api/households/{householdId}/activity", cancellationToken) ?? [];

    public async Task<IReadOnlyList<DailyActivitySummaryResponse>> GetDailyActivitySummaryAsync(
        Guid householdId,
        int days = 7,
        CancellationToken cancellationToken = default)
        => await GetAsync<IReadOnlyList<DailyActivitySummaryResponse>>(
            $"api/households/{householdId}/activity/daily-summary?days={days}", cancellationToken) ?? [];

    public async Task<IReadOnlyList<MemberDayStatusResponse>> GetWeeklyStatusAsync(
        Guid householdId,
        DateOnly date,
        CancellationToken cancellationToken = default)
        => await GetAsync<IReadOnlyList<MemberDayStatusResponse>>(
            $"api/households/{householdId}/weekly-status?date={date:yyyy-MM-dd}", cancellationToken) ?? [];

    public async Task<PreferenceResponse?> GetPreferenceAsync(
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken = default)
        => await GetAsync<PreferenceResponse>(
            $"api/households/{householdId}/members/{memberId}/preferences", cancellationToken);

    public async Task<bool> SetPreferenceAsync(
        Guid householdId,
        Guid memberId,
        string presentation,
        string motivation,
        bool showTimeLevel,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/members/{memberId}/preferences", cancellationToken);
        request.Content = JsonContent.Create(new { presentation, motivation, showTimeLevel });

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// The signed-in member's own reminders due within [<paramref name="from"/>, <paramref name="to"/>] -
    /// see docs/PRODUCT.md §11. Never anyone else's, even in the same household - the server
    /// resolves the owner from the caller's own token, not from anything this client sends.
    /// </summary>
    public async Task<IReadOnlyList<ReminderResponse>> GetOwnRemindersAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
        => await GetAsync<IReadOnlyList<ReminderResponse>>(
            $"api/households/{householdId}/reminders?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            cancellationToken) ?? [];

    /// <summary>Books a new, upcoming reminder for the signed-in member.</summary>
    public async Task<ReminderResponse?> CreateReminderAsync(
        Guid householdId,
        string title,
        string? location,
        DateOnly date,
        TimeOnly? timeOfDay,
        int? travelMinutes = null,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/reminders", cancellationToken);
        request.Content = JsonContent.Create(
            new CreateReminderRequest(title, location, date, timeOfDay, travelMinutes));

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReminderResponse>(cancellationToken)
            : null;
    }

    public async Task<ReminderResponse?> ChangeReminderTitleAsync(
        Guid householdId,
        Guid reminderId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/reminders/{reminderId}/title", cancellationToken);
        request.Content = JsonContent.Create(new ChangeReminderTitleRequest(title));

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReminderResponse>(cancellationToken)
            : null;
    }

    /// <summary>Changes the location, or clears it with a blank value.</summary>
    public async Task<ReminderResponse?> ChangeReminderLocationAsync(
        Guid householdId,
        Guid reminderId,
        string? location,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/reminders/{reminderId}/location", cancellationToken);
        request.Content = JsonContent.Create(new ChangeReminderLocationRequest(location));

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReminderResponse>(cancellationToken)
            : null;
    }

    /// <summary>Moves a reminder to a new date and time of day.</summary>
    public async Task<ReminderResponse?> MoveReminderAsync(
        Guid householdId,
        Guid reminderId,
        DateOnly date,
        TimeOnly? timeOfDay,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/reminders/{reminderId}/move", cancellationToken);
        request.Content = JsonContent.Create(new MoveReminderRequest(date, timeOfDay));

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReminderResponse>(cancellationToken)
            : null;
    }

    /// <summary>Sets or clears the travel time before the reminder's time of day - see docs/PRODUCT.md §11.</summary>
    public async Task<ReminderResponse?> SetReminderTravelMinutesAsync(
        Guid householdId,
        Guid reminderId,
        int? travelMinutes,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Put, $"api/households/{householdId}/reminders/{reminderId}/travel-minutes", cancellationToken);
        request.Content = JsonContent.Create(new SetReminderTravelMinutesRequest(travelMinutes));

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReminderResponse>(cancellationToken)
            : null;
    }

    /// <summary>Cancels the reminder. Idempotent - safe to call again on one already cancelled.</summary>
    public async Task<ReminderResponse?> CancelReminderAsync(
        Guid householdId,
        Guid reminderId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/reminders/{reminderId}/cancel", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReminderResponse>(cancellationToken)
            : null;
    }

    /// <summary>Checks off the reminder - see Hemordna.Domain.Reminders.Reminder.Lapse. Idempotent,
    /// safe to call again on one already checked off.</summary>
    public async Task<ReminderResponse?> LapseReminderAsync(
        Guid householdId,
        Guid reminderId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/reminders/{reminderId}/lapse", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReminderResponse>(cancellationToken)
            : null;
    }

    /// <summary>Takes back a cancellation or a check-off. Fails (non-success status) if the
    /// reminder was already upcoming.</summary>
    public async Task<ReminderResponse?> RestoreReminderAsync(
        Guid householdId,
        Guid reminderId,
        CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/reminders/{reminderId}/restore", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReminderResponse>(cancellationToken)
            : null;
    }

    /// <summary>The public VAPID key a browser needs before it can create a push subscription.
    /// Never anyone else's; unused by the caller if not signed in with a household.</summary>
    public async Task<string?> GetPushVapidPublicKeyAsync(
        Guid householdId, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<VapidPublicKeyResponse>(
            $"api/households/{householdId}/push/vapid-public-key", cancellationToken);
        return response?.PublicKey;
    }

    /// <summary>Registers this device's push subscription for the signed-in member.</summary>
    public async Task<bool> SubscribeToPushAsync(
        Guid householdId, string endpoint, string p256dh, string auth, CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/push/subscribe", cancellationToken);
        request.Content = JsonContent.Create(new SubscribeToPushRequest(endpoint, p256dh, auth));

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Removes this device's push subscription for the signed-in member.</summary>
    public async Task<bool> UnsubscribeFromPushAsync(
        Guid householdId, string endpoint, CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/push/unsubscribe", cancellationToken);
        request.Content = JsonContent.Create(new UnsubscribeFromPushRequest(endpoint));

        var response = await _http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Sends a test notification to every one of the signed-in member's own devices.
    /// Returns how many actually received it, or <c>null</c> on failure.</summary>
    public async Task<int?> SendTestPushAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var request = await AuthorizedAsync(
            HttpMethod.Post, $"api/households/{householdId}/push/test", cancellationToken);

        var response = await _http.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<SendTestPushResponse>(cancellationToken))?.Sent
            : null;
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

/// <summary>Why <see cref="HemordnaApiClient.JoinHouseholdAsync"/> did not succeed.</summary>
public enum JoinHouseholdError
{
    InvalidCode,
    AlreadyInHousehold,
    Unknown
}

/// <summary>
/// Either the joined household, or the reason it failed - distinct from the other household
/// endpoints' plain-null-on-failure pattern because joining has two genuinely different
/// failure modes a person can act on (typo in the code vs. already belongs elsewhere), not
/// just "something went wrong".
/// </summary>
public sealed record JoinHouseholdOutcome(HouseholdResponse? Household, JoinHouseholdError? Error)
{
    public static JoinHouseholdOutcome ForSuccess(HouseholdResponse? household) => new(household, null);

    public static JoinHouseholdOutcome ForFailure(JoinHouseholdError error) => new(null, error);
}

/// <summary>See <see cref="HemordnaApiClient.GetPasskeyLoginOptionsAsync"/>.</summary>
public sealed record PasskeyLoginOptions(string FlowId, string OptionsJson);
