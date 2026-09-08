using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>"Ny form 2026" §4: BottomSheet.razor's two mobile heights (Components/SheetDetent.cs)
/// - a short choice/summary opens "half", a form that needs room to grow opens "full". Both
/// sheets can be open at once here (RoomSheet stays open behind TaskOptionsSheet, a drill-down),
/// which is also what lets this test check that Esc closes only the topmost one first.</summary>
[Collection(HemordnaAppCollection.Name)]
public class SheetDetentTests
{
    private readonly HemordnaAppFixture _app;

    public SheetDetentTests(HemordnaAppFixture app) => _app = app;

    private static ILocator Sheet(IPage page, string title) => page.GetByRole(AriaRole.Dialog, new() { Name = title });

    [Fact]
    public async Task TaskOptionsSheet_opens_half_and_RoomSheet_opens_full_and_Esc_closes_each()
    {
        var page = await _app.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await SignUpHelper.SignUpAsync(page, "Ines");

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Rum", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = Sheet(page, "Nytt rum");
        await page.GetByLabel("Rumstyp").SelectOptionAsync(new SelectOptionValue { Label = "Litet wc" });
        await page.GetByRole(AriaRole.Button, new() { Name = "Skapa", Exact = true }).ClickAsync();
        await page.GetByText("Skapat, uppskattad tid per rum:").WaitForAsync();
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Litet wc" }).First.ClickAsync();
        var roomSheet = Sheet(page, "Litet wc");
        await roomSheet.WaitForAsync();
        Assert.Equal("full", await roomSheet.GetAttributeAsync("data-detent"));

        await roomSheet.GetByRole(AriaRole.Button, new() { Name = "Rengör toalettstolen" }).ClickAsync();
        var taskSheet = Sheet(page, "Rengör toalettstolen");
        await taskSheet.WaitForAsync();
        Assert.Equal("half", await taskSheet.GetAttributeAsync("data-detent"));

        // Esc closes the topmost (focused) sheet first - TaskOptionsSheet - and leaves RoomSheet
        // open behind it, exactly like clicking its own "Stäng" would.
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(taskSheet).Not.ToBeVisibleAsync();
        await Assertions.Expect(roomSheet).ToBeVisibleAsync();

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(roomSheet).Not.ToBeVisibleAsync();
    }
}
