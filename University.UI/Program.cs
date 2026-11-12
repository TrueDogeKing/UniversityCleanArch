using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using University.Application;
using University.Infrastructure;
using University.Infrastructure.Persistence;
using University.UI.Konsola;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Korzeń kompozycji (Composition Root): jedyne miejsce, w którym łączone są warstwy.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Logging.ClearProviders();

var lancuchPolaczenia = builder.Configuration.GetConnectionString("University")
                        ?? UniversityDbContextFactory.DomyslnyLancuchPolaczenia;

builder.Services
    .AddInfrastructure(lancuchPolaczenia)
    .AddApplication()
    .AddSingleton<AplikacjaKonsolowa>();

using var host = builder.Build();

try
{
    await AnsiConsole.Status().StartAsync("Stosowanie migracji bazy danych...", async _ =>
        await host.Services.ZastosujMigracjeAsync());
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine("[red]Nie udało się połączyć z bazą danych lub zastosować migracji.[/]");
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return 1;
}

await host.Services.GetRequiredService<AplikacjaKonsolowa>().UruchomAsync();
return 0;
