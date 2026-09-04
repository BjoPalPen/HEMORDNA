using Microsoft.AspNetCore.SignalR.Client;

namespace Hemordna.Client.Services;

/// <summary>
/// The client side of PRODUCT.md §6: when someone else in the household changes something,
/// this fires so a page can re-fetch its own data. It never carries the changed data itself -
/// see IHouseholdNotifier in Application for why that stays a single coarse signal.
/// </summary>
public sealed class HouseholdRealtimeClient : IAsyncDisposable
{
    private readonly string _apiBaseAddress;
    private readonly TokenStore _tokens;

    private HubConnection? _connection;
    private Guid? _joinedHouseholdId;

    public HouseholdRealtimeClient(string apiBaseAddress, TokenStore tokens)
    {
        _apiBaseAddress = apiBaseAddress;
        _tokens = tokens;
    }

    public event Action? OccurrencesChanged;

    /// <summary>Connects and joins the household's group. Safe to call repeatedly - a second
    /// call for the same household is a no-op, and switching households rejoins.</summary>
    public async Task ConnectAsync(Guid householdId)
    {
        if (_joinedHouseholdId == householdId && _connection is { State: HubConnectionState.Connected })
        {
            return;
        }

        if (_connection is null)
        {
            var token = await _tokens.GetAsync();

            if (token is null)
            {
                return;
            }

            // The WebSocket handshake cannot carry an Authorization header, so the token
            // travels as a query string parameter instead - see Program.cs on the API side.
            var hubUrl = new Uri(new Uri(_apiBaseAddress), $"hubs/household?access_token={token}");

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
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
