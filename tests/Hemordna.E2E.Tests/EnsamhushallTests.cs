using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Ett ensamhushåll ska aldrig mötas av frågan om VEM som gör något - se docs/ARCHITECTURE.md
/// "Beslut: Ingen vem-fråga i ensamhushåll". Bakgrunden är en testperson som bor ensam och
/// reagerade på "Roterar mellan alla": med en enda medlem står valet mellan det alternativet och
/// ens eget namn, vilket betyder exakt samma sak. Det är inte bara ett tondövt ordval utan ett
/// val som inte finns.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class EnsamhushallTests
{
    private readonly HemordnaAppFixture _app;

    public EnsamhushallTests(HemordnaAppFixture app) => _app = app;

    private static async Task<(ILocator Room, IPage Page)> ArrangeRoomWithTaskAsync(
        IPage page, string memberName, string roomName, string taskName)
    {
        await SignUpHelper.SignUpAsync(page, memberName);

        await page.GotoAsync("/rum");
        await page.GetByRole(AriaRole.Button, new() { Name = "Nytt rum" }).ClickAsync();
        var newRoomSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Nytt rum" });
        await page.GetByText("Lägg till ett tomt rum i stället").ClickAsync();
        await page.GetByLabel("Rummets namn").FillAsync(roomName);
        await page.GetByRole(AriaRole.Button, new() { Name = "Lägg till rum" }).ClickAsync();
        await newRoomSheet.GetByRole(AriaRole.Button, new() { Name = "Stäng" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = roomName }).First.ClickAsync();
        var room = page.GetByRole(AriaRole.Dialog, new() { Name = roomName });
        await room.WaitForAsync();

        await room.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        var addSheet = page.GetByRole(AriaRole.Dialog, new() { Name = $"Lägg till uppgift i {roomName}" });
        await addSheet.GetByLabel("Namn").FillAsync(taskName);
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lite tid" }).ClickAsync();
        await addSheet.GetByRole(AriaRole.Button, new() { Name = "Lägg till uppgift" }).ClickAsync();
        await room.GetByRole(AriaRole.Button, new() { Name = taskName }).WaitForAsync();

        return (room, page);
    }

    /// <summary>Raden ska inte ens erbjudas, inte bara vara inaktiv - samma krav som
    /// AlwaysOnWeekdayTests ställer på "Alltid på" för en daglig uppgift.</summary>
    [Fact]
    public async Task A_single_member_household_is_never_asked_who_does_the_task()
    {
        var page = await _app.NewPageAsync();
        var (room, _) = await ArrangeRoomWithTaskAsync(page, "Ensam", "Kök", "Diska");

        await room.GetByRole(AriaRole.Button, new() { Name = "Diska" }).ClickAsync();
        var taskSheet = page.GetByRole(AriaRole.Dialog, new() { Name = "Diska" });
        await taskSheet.WaitForAsync();

        await Assertions.Expect(taskSheet.GetByText("Vem gör det")).Not.ToBeVisibleAsync();
        await Assertions.Expect(taskSheet.GetByText("Turas om")).Not.ToBeVisibleAsync();
    }

    /// <summary>Chippet säger vem uppgiften tillhör. Bor man ensam är svaret alltid "du", så det
    /// bär ingen information - och "turas om" bär dessutom ett underförstått sällskap.</summary>
    [Fact]
    public async Task A_single_member_household_sees_no_rotation_chip_on_a_task()
    {
        var page = await _app.NewPageAsync();
        var (room, _) = await ArrangeRoomWithTaskAsync(page, "Ensammare", "Hall", "Sopsortera");

        await Assertions.Expect(room.GetByText("turas om")).Not.ToBeVisibleAsync();
    }

    /// <summary>Att balansera om förutsätter någon att balansera mot.</summary>
    [Fact]
    public async Task A_single_member_household_is_not_offered_to_rebalance()
    {
        var page = await _app.NewPageAsync();
        await SignUpHelper.SignUpAsync(page, "Ensammast");

        await page.GotoAsync("/hushall");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Familjen" }).First.WaitForAsync();

        await Assertions.Expect(page.GetByText("Balansera om vem som gör vad")).Not.ToBeVisibleAsync();
    }
}
