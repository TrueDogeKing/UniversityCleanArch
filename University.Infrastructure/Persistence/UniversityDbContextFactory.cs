using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace University.Infrastructure.Persistence;

/// <summary>
/// Fabryka czasu projektowania używana przez narzędzia <c>dotnet ef</c>
/// (tworzenie migracji, <c>database drop</c>) bez uruchamiania aplikacji.
/// Łańcuch połączenia można nadpisać zmienną środowiskową
/// <c>UNIVERSITY_CONNECTION_STRING</c>.
/// </summary>
public class UniversityDbContextFactory : IDesignTimeDbContextFactory<UniversityDbContext>
{
    public const string DomyslnyLancuchPolaczenia =
        "Server=(localdb)\\MSSQLLocalDB;Database=UniversityCleanArch;Trusted_Connection=True;TrustServerCertificate=True";

    public UniversityDbContext CreateDbContext(string[] args)
    {
        var lancuch = Environment.GetEnvironmentVariable("UNIVERSITY_CONNECTION_STRING")
                      ?? DomyslnyLancuchPolaczenia;

        var options = new DbContextOptionsBuilder<UniversityDbContext>()
            .UseSqlServer(lancuch)
            .Options;

        return new UniversityDbContext(options);
    }
}
