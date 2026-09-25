namespace Hemordna.Client.Tests;

/// <summary>Text lock on the service worker registration in index.html (see the PWA update-
/// cache fix in docs/ARCHITECTURE.md) - a plain text check, no rendered page or browser
/// involved (CLAUDE.md §8). This is exactly what the E2E suite cannot cover instead: there the
/// client runs as its own host, never behind the API's pipeline (see Hemordna.Api.Tests for
/// that side of the fix).</summary>
public class ServiceWorkerRegistrationTests
{
    [Fact]
    public void Index_html_registers_the_service_worker_with_updateViaCache_none()
    {
        var indexHtmlPath = Path.Combine(FindRepositoryRoot(), "src", "Hemordna.Client", "wwwroot", "index.html");
        var indexHtml = File.ReadAllText(indexHtmlPath);

        // Without this option, the browser is free to serve the imported
        // service-worker-assets.js out of its own HTTP cache when checking for a service
        // worker update - see index.html's own comment and src/Hemordna.Api/Program.cs's
        // SetNoCacheForAppShellFiles for the rest of the fix.
        Assert.Contains("register('service-worker.js', { updateViaCache: 'none' })", indexHtml);
    }

    // Mirrors HemordnaAppFixture.FindRepositoryRoot (tests/Hemordna.E2E.Tests) - the test's
    // working directory is the test assembly's own bin/ output, not the repository root, and
    // that holds regardless of where "dotnet test" itself is invoked from.
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hemordna.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find the repository root (Hemordna.slnx).");
    }
}
