using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Hemordna.Client.Services;

/// <summary>
/// The client side of PRODUCT.md §6: when someone else in the household changes something,
/// this fires so a page can re-fetch its own data. It never carries the changed data itself -
/// see IHouseholdNotifier in Application for why that stays a single coarse signal.
/// </summary>
public sealed class HouseholdRealtimeClient : IAsyncDisposable
{
    private readonly string _apiBaseAddress;
    private readonly HemordnaApiClient _api;
    private readonly TokenStore _tokens;
    private readonly ILogger<HouseholdRealtimeClient> _logger;

    private HubConnection? _connection;
    private Guid? _joinedHouseholdId;

    public HouseholdRealtimeClient(
        string apiBaseAddress, HemordnaApiClient api, TokenStore tokens, ILogger<HouseholdRealtimeClient> logger)
    {
        _apiBaseAddress = apiBaseAddress;
        _api = api;
        _tokens = tokens;
        _logger = logger;
    }

    public event Action? OccurrencesChanged;

    /// <summary>
    /// Connects and joins the household's group. Safe to call repeatedly - a second call for the
    /// same household is a no-op, and switching households rejoins.
    ///
    /// <para>
    /// Never throws because the connection is down. Realtime is an optional enhancement
    /// (PRODUCT.md §6), and the page that calls this (MinDag.razor's OnInitializedAsync) must
    /// still load and render even when the SignalR handshake fails - a bad mobile connection or a
    /// hub that is briefly unreachable must never keep the day's plan from showing. Any such
    /// failure is caught and logged, and the connection is disposed and forgotten so the NEXT
    /// call rebuilds it from scratch instead of reusing a connection that never finished starting.
    /// An <see cref="OperationCanceledException"/> from a shutdown in progress is the one
    /// exception this deliberately still lets through unchanged, since that is the caller's own
    /// cancellation, not a realtime failure.
    /// </para>
    /// </summary>
    public async Task ConnectAsync(Guid householdId)
    {
        if (_joinedHouseholdId == householdId && _connection is { State: HubConnectionState.Connected })
        {
            return;
        }

        try
        {
            if (_connection is null)
            {
                if (!await _api.EnsureAccessTokenAsync(CancellationToken.None))
                {
                    return;
                }

                var hubUrl = new Uri(new Uri(_apiBaseAddress), "hubs/household");

                _connection = new HubConnectionBuilder()
                    .WithUrl(hubUrl, options => options.AccessTokenProvider = async () =>
                    {
                        // Called fresh on the initial connection AND on every automatic
                        // reconnect attempt (see WithAutomaticReconnect below) - never a
                        // captured, possibly-stale value. The server closes the connection the
                        // moment the access token expires (CloseOnAuthenticationExpiration in
                        // Program.cs), so without ensuring a valid token here, a reconnect would
                        // keep presenting the very token that just got the connection closed and
                        // loop failing until some unrelated REST call happened to refresh it.
                        await _api.EnsureAccessTokenAsync(CancellationToken.None);
                        return _tokens.AccessToken;
                    })
                    .WithAutomaticReconnect()
                    .Build();

                _connection.On(
                    "OccurrencesChanged",
                    () => OccurrencesChanged?.Invoke());

                _connection.Reconnected += _ => JoinAsync(householdId);

                await _connection.StartAsync();
            }

            await JoinAsync(householdId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Household realtime connection failed to start or join; will rebuild it from scratch on the next attempt.");

            var failed = _connection;
            _connection = null;
            _joinedHouseholdId = null;

            if (failed is not null)
            {
                await failed.DisposeAsync();
            }
        }
    }

    private async Task JoinAsync(Guid householdId)
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.InvokeAsync("JoinHousehold", householdId);
        _joinedHouseholdId = householdId;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
