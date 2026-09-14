using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Guards the user guides under <c>wwwroot/hjalp/</c>. They are static files built by
/// <c>assets/guide.js</c> from each page's own <c>window.GUIDE</c>, so nothing else in the test
/// suite would notice if the template stopped running, an image path broke, or a wide table
/// started dragging the page sideways on a phone.
/// </summary>
/// <remarks>
/// Everything here is asserted against the app's own static file serving, not the file system -
/// a guide that renders from disk but is swallowed by the SPA fallback is still broken.
/// </remarks>
[Collection(HemordnaAppCollection.Name)]
public class GuideRenderTests
{
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 844;

    private static readonly string[] GuidePaths =
    [
        "/hjalp/sv/familjen.html",
        "/hjalp/sv/hushallsansvarig.html"
    ];

    private readonly HemordnaAppFixture _app;

    public GuideRenderTests(HemordnaAppFixture app) => _app = app;

    /// <summary>
    /// Fetches every <c>img</c> source and returns the ones that do not resolve. Deliberately not
    /// <c>naturalWidth === 0</c>: every screenshot is <c>loading="lazy"</c>, so anything below the
    /// fold is legitimately unloaded on arrival and that check reports false failures.
    /// </summary>
    private static Task<string[]> UnresolvableImagesAsync(IPage page) => page.EvaluateAsync<string[]>(
        @"async () => {
            const missing = [];
            for (const src of Array.from(document.images).map(i => i.getAttribute('src'))) {
                const response = await fetch(src, { method: 'GET' });
                if (!response.ok) { missing.push(src + ' -> ' + response.status); }
            }
            return missing;
        }");

    [Fact]
    public async Task The_hub_lists_both_guides()
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);

        await page.GotoAsync("/hjalp/index.html");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Hjälp och guider" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "För alla i familjen" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "För dig som sköter hushållet" }))
            .ToBeVisibleAsync();
    }

    [Theory]
    [InlineData("/hjalp/sv/familjen.html")]
    [InlineData("/hjalp/sv/hushallsansvarig.html")]
    public async Task A_guide_builds_its_chapters_from_the_shared_template(string path)
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);

        await page.GotoAsync(path);

        // No magic chapter count: content is allowed to grow. What must hold is that the template
        // ran at all, and that it built one table-of-contents entry per chapter - if guide.js
        // failed, the shell's own heading would still be there while both of these are zero.
        var chapters = await page.Locator("section.kapitel").CountAsync();
        var tocLinks = await page.Locator(".toc a").CountAsync();

        Assert.True(chapters > 0, $"{path} byggde inga kapitel - körde guide.js?");
        Assert.Equal(chapters, tocLinks);
    }

    [Theory]
    [InlineData("/hjalp/sv/familjen.html")]
    [InlineData("/hjalp/sv/hushallsansvarig.html")]
    public async Task Every_screenshot_a_guide_points_at_resolves(string path)
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);

        await page.GotoAsync(path);
        await page.Locator("section.kapitel").First.WaitForAsync();

        Assert.Empty(await UnresolvableImagesAsync(page));
    }

    [Theory]
    [InlineData("/hjalp/index.html")]
    [InlineData("/hjalp/sv/familjen.html")]
    [InlineData("/hjalp/sv/hushallsansvarig.html")]
    public async Task A_guide_never_scrolls_sideways_on_a_phone(string path)
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);

        await page.GotoAsync(path);

        // One wide table is all it takes - guide.js wraps every table in .tabell-wrap so it
        // scrolls inside its own box instead of widening the document.
        var documentWidth = await page.EvaluateAsync<int>("() => document.body.scrollWidth");

        Assert.True(
            documentWidth <= PhoneWidth + 1,
            $"{path} är {documentWidth}px bred på en {PhoneWidth}px-skärm.");
    }
}
