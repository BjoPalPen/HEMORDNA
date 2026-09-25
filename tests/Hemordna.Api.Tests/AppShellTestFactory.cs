using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Hemordna.Api.Tests;

/// <summary>
/// Boots the real API pipeline (see Program.cs) against a throwaway wwwroot, so
/// <see cref="AppShellCacheHeaderTests"/> observes exactly what production serves rather than a
/// re-implementation of the OnPrepareResponse logic under test. The webroot holds only empty
/// stand-ins for the app-shell files plus one fingerprinted _framework file - see
/// <see cref="AppShellCacheHeaderTests"/> for which files and why.
/// </summary>
/// <remarks>
/// The API requires a working connection string, a JWT signing key and (outside Development) a
/// Resend API key just to reach <c>app.Build()</c> (see <c>AddInfrastructure</c> and
/// <see cref="Authentication.JwtOptions.Validate"/>) - all three are supplied here as throwaway
/// values that are never actually used. Nothing under test queries the database: the
/// environment is pinned away from "Development" so <c>DevelopmentDataSeeder</c> (which does
/// touch the database, eagerly, at startup) never runs, and the two reminder background
/// services swallow-and-log any connection failure of their own instead of taking the host down
/// (see their <c>ExecuteAsync</c> try/catch). A real PostgreSQL instance is never needed -
/// CLAUDE.md §8 and the mission's own stop condition rule out rebuilding <c>Program.cs</c>'s
/// startup just to make it testable, and it turned out unnecessary here.
/// </remarks>
public sealed class AppShellTestFactory : WebApplicationFactory<Program>
{
    // Never resolved - UseNpgsql only parses this at startup and connects lazily on first
    // query, which nothing in these tests triggers.
    private const string FakeConnectionString =
        "Host=127.0.0.1;Port=1;Database=hemordna_test;Username=test;Password=test";

    // Never used to sign or verify anything real - only long enough to satisfy JwtOptions'
    // 32-byte minimum. Deliberately not the E2E fixture's TestSigningKey (HemordnaAppFixture.cs)
    // so nobody mistakes the two for something that has to stay in sync.
    private const string TestSigningKey = "api-tests-fake-signing-key-never-used-to-sign-anything-real";

    // Outside Development, AddInfrastructure throws unless a Resend API key is present - see
    // DependencyInjection.cs. Any non-empty value clears that check; nothing under test ever
    // sends an e-mail, so it is never actually used against the real Resend API.
    private const string FakeResendApiKey = "test-fake-resend-api-key";

    private readonly string _webRootPath = CreateWebRoot();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Anything but "Development" - see the class remarks above for why that matters here.
        builder.UseEnvironment("Testing");
        builder.UseWebRoot(_webRootPath);
        builder.UseSetting("ConnectionStrings:Hemordna", FakeConnectionString);
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Resend:ApiKey", FakeResendApiKey);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            try
            {
                Directory.Delete(_webRootPath, recursive: true);
            }
            catch (IOException)
            {
                // Best effort - a leftover temp directory is harmless.
            }
        }
    }

    private static string CreateWebRoot()
    {
        var root = Directory.CreateTempSubdirectory("hemordna-api-tests-").FullName;

        File.WriteAllText(Path.Combine(root, "service-worker.js"), string.Empty);
        File.WriteAllText(Path.Combine(root, "service-worker-assets.js"), string.Empty);
        File.WriteAllText(Path.Combine(root, "index.html"), string.Empty);
        File.WriteAllText(Path.Combine(root, "manifest.webmanifest"), string.Empty);

        // Stands in for a real Blazor build's fingerprinted output - same naming shape
        // (name.<hash>.wasm), but this file's own content is irrelevant to the tests here.
        var frameworkDirectory = Directory.CreateDirectory(Path.Combine(root, "_framework"));
        File.WriteAllText(Path.Combine(frameworkDirectory.FullName, "dummy.abc123.wasm"), string.Empty);

        return root;
    }
}
