using System.Diagnostics;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Starts the API and the Blazor client, then hands out browser pages against them.
/// </summary>
/// <remarks>
/// The tests own the whole stack so they can be run with a single command. If something is
/// already listening on a port the fixture uses it instead of starting a second copy, which
/// keeps a local dev session usable while tests run.
/// </remarks>
public sealed class HemordnaAppFixture : IAsyncLifetime
{
    // These match the committed development configuration on purpose. A Blazor WebAssembly
    // app reads its settings from wwwroot/appsettings.json in the browser, not from the host
    // process environment, so the client cannot be pointed elsewhere just by setting a
    // variable here - and the tests should not rewrite a checked-in file to work around it.
    private const string ApiBaseUrl = "http://localhost:5199";
    private const string ClientBaseUrl = "http://localhost:5200";

    // Test-only signing key. Production keys come from the environment; see docs/ARCHITECTURE.md.
    private const string TestSigningKey = "e2e-test-signing-key-not-for-any-deployed-environment";

    private readonly List<Process> _started = [];

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string ClientUrl => ClientBaseUrl;

    /// <summary>The API the tests drive directly, without going through a browser.</summary>
    public string ApiUrl => ApiBaseUrl;

    public async Task InitializeAsync()
    {
        await StartIfNeededAsync(
            ApiBaseUrl,
            "src/Hemordna.Api",
            new Dictionary<string, string>
            {
                ["ASPNETCORE_URLS"] = ApiBaseUrl,
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["Jwt__SigningKey"] = TestSigningKey,
                ["Cors__AllowedOrigins__0"] = ClientBaseUrl
            },
            path: "/health");

        await StartIfNeededAsync(
            ClientBaseUrl,
            "src/Hemordna.Client",
            new Dictionary<string, string>
            {
                ["ASPNETCORE_URLS"] = ClientBaseUrl
            },
            path: "/");

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,

            // Containers give Chromium a tiny /dev/shm and no user namespaces, which crashes
            // the renderer on startup. These two flags are what make it run in a devcontainer.
            Args = ["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu"]
        });
    }

    /// <summary>
    /// A fresh browser context, so no test inherits another's stored token. Pass
    /// <paramref name="locale"/> to pin the browser language - Blazor picks its globalization
    /// data from the browser, so the language is part of what a test can need to control.
    /// </summary>
    public async Task<IPage> NewPageAsync(string? locale = null)
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = ClientBaseUrl,
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = locale
        });

        return await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();

        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(10_000);
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            process.Dispose();
        }
    }

    private async Task StartIfNeededAsync(
        string baseUrl,
        string projectPath,
        Dictionary<string, string> environment,
        string path)
    {
        if (await IsReachableAsync(baseUrl + path))
        {
            return;
        }

        var repositoryRoot = FindRepositoryRoot();

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-launch-profile");

        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {projectPath}.");

        _started.Add(process);

        // Drain the pipes so a chatty process cannot block on a full buffer.
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();

        if (!await WaitUntilReachableAsync(baseUrl + path, TimeSpan.FromSeconds(90)))
        {
            throw new InvalidOperationException($"{projectPath} did not become reachable at {baseUrl}.");
        }
    }

    private static async Task<bool> WaitUntilReachableAsync(string url, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (await IsReachableAsync(url))
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }

    private static async Task<bool> IsReachableAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await http.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

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

[CollectionDefinition(Name)]
public sealed class HemordnaAppCollection : ICollectionFixture<HemordnaAppFixture>
{
    public const string Name = "Hemordna app";
}
