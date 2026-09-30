namespace Hemordna.Application.Authentication;

/// <summary>
/// Deletes refresh tokens that have passed their own <c>ExpiresAt</c>. Rotation writes a new row
/// every time a token is exchanged, so without this the table grows for as long as the app is
/// used - data minimisation (CLAUDE.md §10), the same reason
/// <see cref="Reminders.PurgeOldReminders"/> exists.
/// </summary>
/// <remarks>
/// <para>
/// <b>Expiry is the only safe thing to delete on, and consumed is not.</b> A consumed token that
/// has NOT yet expired is exactly what reuse detection needs: presenting it again is how
/// <see cref="RotateRefreshToken"/> learns that a token was replayed and revokes the whole chain.
/// Deleting consumed rows early would turn a replay into an <i>unknown</i> token, which is
/// rejected on its own but revokes nothing - so the stolen chain would keep working. That is a
/// silent weakening of the one property rotation exists to provide.
/// </para>
/// <para>
/// Deleting <i>expired</i> rows costs nothing, because the two outcomes are already identical: an
/// expired token is rejected without revoking its chain (see
/// <c>RotateRefreshTokenTests.An_expired_token_is_rejected_without_revoking_the_chain</c>), and so
/// is an unknown one. After expiry there is no behaviour left to preserve, only a row.
/// </para>
/// <para>
/// Deliberately takes <c>now</c> as a parameter rather than reading a clock (CLAUDE.md §5) -
/// same division of responsibility as <see cref="Reminders.PurgeOldReminders"/> and
/// <c>SendDueReminderNotifications</c>. Only the background service that calls this reads
/// <see cref="TimeProvider"/>.
/// </para>
/// </remarks>
public sealed class PurgeExpiredRefreshTokens
{
    private readonly IRefreshTokenRepository _tokens;

    public PurgeExpiredRefreshTokens(IRefreshTokenRepository tokens) => _tokens = tokens;

    /// <summary>
    /// Deletes every token whose <c>ExpiresAt</c> is strictly before <paramref name="now"/>, and
    /// returns how many were removed. A token expiring exactly at <paramref name="now"/> is kept -
    /// it is not yet expired.
    /// </summary>
    public async Task<int> HandleAsync(DateTimeOffset now, CancellationToken cancellationToken)
        => await _tokens.DeleteExpiredBeforeAsync(now, cancellationToken);
}
