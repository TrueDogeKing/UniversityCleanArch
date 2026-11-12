using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using University.Application.Abstractions;
using University.Infrastructure.Persistence;

namespace University.Infrastructure;

/// <summary>Rejestracja usług warstwy Infrastructure w kontenerze DI.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string lancuchPolaczenia)
    {
        services.AddDbContext<UniversityDbContext>(options => options.UseSqlServer(lancuchPolaczenia));
        services.AddScoped<IUniversityDbContext>(sp => sp.GetRequiredService<UniversityDbContext>());
        return services;
    }

    /// <summary>
    /// Stosuje oczekujące migracje przy starcie aplikacji. Po usunięciu bazy
    /// (<c>dotnet ef database drop</c>) schemat i liczniki S/P/D są odtwarzane.
    /// </summary>
    public static async Task ZastosujMigracjeAsync(this IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UniversityDbContext>();
        await context.Database.MigrateAsync(ct);
    }
}
