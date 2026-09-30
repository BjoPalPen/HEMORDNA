using Hemordna.Application.Authentication;

namespace Hemordna.Api.Services;

/// <summary>
/// Periodically deletes refresh tokens that have passed their own expiry - see
/// <see cref="PurgeExpiredRefreshTokens"/>, which does the actual work and explains why expiry is
/// the only safe thing to delete on. This class's only job is the two things that use case
/// deliberately cannot do itself (CLAUDE.md §5): read the clock, and own the loop.
/// </summary>
/// <remarks>
/// Same shape as <see cref="ReminderCleanupBackgroundService"/>, including the 24-hour interval
/// and the first sweep running immediately at startup: nothing here is time-critical, since an
/// expired token is already rejected whether or not its row is still there. The sweep only
/// reclaims space.
/// </remarks>
public sealed class RefreshTokenCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefreshTokenCleanupBackgroundService> _logger;

    public RefreshTokenCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<RefreshTokenCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Refresh token cleanup background service started; running every {PollInterval}.", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deletedCount = await RunOnceAsync(stoppingToken);

                if (deletedCount > 0)
                {
                    // Count only - never a hash, a user id or a chain id. A count is enough to see
                    // that the job is alive, and anything more would put authentication material
                    // in the log (CLAUDE.md §10).
                    _logger.LogInformation("Purged {Count} expired refresh token(s).", deletedCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Refresh token cleanup sweep failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown - the while-condition above ends the loop next iteration.
            }
        }
    }

    internal async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var purge = scope.ServiceProvider.GetRequiredService<PurgeExpiredRefreshTokens>();

        return await purge.HandleAsync(_timeProvider.GetUtcNow(), cancellationToken);
    }
}
