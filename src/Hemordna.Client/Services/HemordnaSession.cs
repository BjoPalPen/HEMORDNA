using Hemordna.Client.Contracts;

namespace Hemordna.Client.Services;

/// <summary>
/// Who is signed in, and which household they belong to. One place for that answer so pages
/// do not each re-derive it.
/// </summary>
public sealed class HemordnaSession
{
    private readonly HemordnaApiClient _api;

    public HemordnaSession(HemordnaApiClient api) => _api = api;

    public MeResponse? Me { get; private set; }

    /// <summary>True once <see cref="LoadAsync"/> has run, so pages can tell "loading" from "signed out".</summary>
    public bool IsLoaded { get; private set; }

    public bool IsSignedIn => Me is not null;

    /// <summary>True when signed in but no household has been created yet.</summary>
    public bool NeedsHousehold => Me is { HouseholdId: null };

    /// <summary>
    /// Whether the signed-in member can change rooms, tasks and other members - see
    /// docs/ARCHITECTURE.md "Beslut: Vem får ändra vad". Controls that nobody may use when this
    /// is false are hidden outright, never shown disabled - see docs/DESIGN.md.
    /// </summary>
    public bool CanManageHousehold => Me is { CanManageHousehold: true };

    public event Action? Changed;

    private Task? _loading;

    /// <summary>
    /// Loads the session once. Several components ask for it as the app starts, so concurrent
    /// callers share a single request and every one of them can await the answer - a component
    /// that renders before this completes would otherwise never learn the result.
    /// </summary>
    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
        {
            return Task.CompletedTask;
        }

        return _loading ??= LoadAsync(cancellationToken);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Me = await _api.GetMeAsync(cancellationToken);
        IsLoaded = true;
        Changed?.Invoke();
    }

    /// <summary>Re-reads the session after something that can change membership.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _loading = null;
        return LoadAsync(cancellationToken);
    }

    public async Task SignOutAsync()
    {
        await _api.SignOutAsync();
        Me = null;
        _loading = null;
        IsLoaded = true;
        Changed?.Invoke();
    }
}
