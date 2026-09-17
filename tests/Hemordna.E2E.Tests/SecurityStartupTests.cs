using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

namespace Hemordna.E2E.Tests;

public class SecurityStartupTests
{
    private static Process StartApi(string emailKey, int port)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hemordna.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var project = Path.Combine(root.FullName, "src", "Hemordna.Api");
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = project,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(Path.Combine(project, "bin", configuration, "net10.0", "Hemordna.Api.dll"));
        info.ArgumentList.Add("--urls");
        info.ArgumentList.Add($"http://127.0.0.1:{port}");
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        info.Environment["DOTNET_ENVIRONMENT"] = "Production";
        info.Environment["ConnectionStrings__Hemordna"] = "Host=127.0.0.1;Database=unused;Username=unused;Password=unused";
        info.Environment["Jwt__SigningKey"] = "security-startup-test-key-not-for-any-deployed-environment";
        info.Environment["Resend__ApiKey"] = emailKey;
        info.Environment["AuthRateLimit__PermitLimit"] = "2";
        info.Environment["RunMigrationsOnStartup"] = "false";
        return Process.Start(info)!;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void Stop(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(10_000);
        }
    }

    [Fact]
    public async Task Production_without_email_key_fails_before_listening()
    {
        using var process = StartApi("", FreePort());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("Resend:ApiKey must be configured outside Development.", await stdout + await stderr);
        }
        finally
        {
            Stop(process);
        }
    }

    [Fact]
    public async Task Auth_limit_returns_429_with_retry_after_without_blocking_other_routes()
    {
        var port = FreePort();
        using var process = StartApi("test-only-unused-email-key", port);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            var ready = false;
            for (var attempt = 0; attempt < 100 && !process.HasExited; attempt++)
            {
                try
                {
                    using var response = await http.GetAsync("/api/me");
                    ready = response.StatusCode == HttpStatusCode.Unauthorized;
                    if (ready) break;
                }
                catch (HttpRequestException) { }
                await Task.Delay(100);
            }
            Assert.True(ready, "API did not start.");
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var response = await http.PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
            using var limited = await http.PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(60), limited.Headers.RetryAfter!.Delta);
            using var otherRoute = await http.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.Unauthorized, otherRoute.StatusCode);
        }
        finally
        {
            Stop(process);
            await stdout;
            await stderr;
        }
    }
}
