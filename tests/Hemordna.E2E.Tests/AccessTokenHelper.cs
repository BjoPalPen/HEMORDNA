using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Reads the client's current access token, the same way every test that needs to call the API
/// directly (bypassing the UI) for setup or assertions has always done it. This is the one place
/// that knows WHERE the client keeps that token - currently <c>localStorage</c>, under the same
/// key the client itself uses (<c>hemordna.token</c>).
/// </summary>
/// <remarks>
/// A later commit moves the access token to in-memory storage in the client (see
/// docs/ARCHITECTURE.md "Beslut: Refresh-token med rotation") - the refresh token stays in
/// <c>localStorage</c>, but the access token will not be readable this way afterwards. Centralizing
/// the read here, before that change, means it only has to be taught the new way once, in this one
/// file, instead of in the 41 test files that previously called
/// <c>page.EvaluateAsync&lt;string&gt;("() =&gt; localStorage.getItem('hemordna.token')")</c>
/// directly. This commit is a pure refactor: the implementation below is unchanged from what
/// every call site did inline.
/// </remarks>
internal static class AccessTokenHelper
{
    internal static Task<string> GetAsync(IPage page)
        => page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.token')");
}
