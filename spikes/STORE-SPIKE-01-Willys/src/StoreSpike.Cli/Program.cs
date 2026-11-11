using StoreSpike.Cli;
using StoreSpike.Core;
using StoreSpike.Infrastructure;

// STORE-SPIKE-01 CLI. Runs a shopping list against the invented mock catalog only: no Willys
// client exists in this spike, by decision (robots.txt disallows automated search, see
// docs/findings.md).
//
//   dotnet run --project src/StoreSpike.Cli [-- --list <fil>] [--catalog <mock-katalog.json>]

Console.OutputEncoding = System.Text.Encoding.UTF8;

var listPath = Path.Combine(AppContext.BaseDirectory, "testdata", "inkopslista.txt");
var catalogPath = Path.Combine(AppContext.BaseDirectory, MockProductCatalog.DefaultFixtureFileName);

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--list" when i + 1 < args.Length:
            listPath = args[++i];
            break;
        case "--catalog" when i + 1 < args.Length:
            catalogPath = args[++i];
            break;
        default:
            Console.Error.WriteLine($"Okänt argument: {args[i]}");
            Console.Error.WriteLine("Användning: StoreSpike.Cli [--list <fil>] [--catalog <mock-katalog.json>]");
            return 1;
    }
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var catalog = await MockProductCatalog.LoadAsync(catalogPath, cancellation.Token);
var lines = await File.ReadAllLinesAsync(listPath, cancellation.Token);
var result = await new ShoppingListRun(catalog).ExecuteAsync(lines, cancellation.Token);

Console.Write(ReportFormatter.Format(result, "mock (påhittad)"));
return 0;
