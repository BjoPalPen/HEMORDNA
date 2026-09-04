using System.Net;
using System.Text.Json;

namespace Hemordna.E2E.Tests;

/// <summary>
/// The manifest and the service worker registration are what make Hemordna installable.
/// They are easy to drop by accident, because nothing in the running app fails without them.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class PwaTests
{
    private readonly HemordnaAppFixture _app;

    public PwaTests(HemordnaAppFixture app) => _app = app;

    [Fact]
    public async Task The_manifest_is_served_and_describes_an_installable_app()
    {
        using var http = new HttpClient { BaseAddress = new Uri(_app.ClientUrl) };

        var response = await http.GetAsync("manifest.webmanifest");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("Hemordna", manifest.GetProperty("name").GetString());
        Assert.Equal("standalone", manifest.GetProperty("display").GetString());
        Assert.NotEmpty(manifest.GetProperty("start_url").GetString()!);

        // Chrome will not offer to install without an icon of at least 192px.
        var hasLargeIcon = manifest.GetProperty("icons").EnumerateArray()
            .Any(icon => icon.GetProperty("sizes").GetString() is "192x192" or "any");

        Assert.True(hasLargeIcon, "The manifest needs an icon of 192px or larger.");
    }

    [Fact]
    public async Task The_page_links_the_manifest_and_registers_a_service_worker()
    {
        using var http = new HttpClient { BaseAddress = new Uri(_app.ClientUrl) };

        var html = await http.GetStringAsync("/");

        Assert.Contains("rel=\"manifest\"", html);
        Assert.Contains("serviceWorker", html);
    }

    [Fact]
    public async Task The_icons_the_manifest_names_actually_exist()
    {
        using var http = new HttpClient { BaseAddress = new Uri(_app.ClientUrl) };

        var manifest = JsonDocument
            .Parse(await http.GetStringAsync("manifest.webmanifest"))
            .RootElement;

        foreach (var icon in manifest.GetProperty("icons").EnumerateArray())
        {
            var source = icon.GetProperty("src").GetString()!;
            var response = await http.GetAsync(source);

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"The manifest names '{source}', which the app does not serve.");
        }
    }
}
