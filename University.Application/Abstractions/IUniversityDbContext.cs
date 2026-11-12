using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using University.Domain.Entities;

namespace University.Application.Abstractions;

/// <summary>
/// Port persystencji warstwy aplikacji. Implementacja (adapter) znajduje się
/// w warstwie Infrastructure, dzięki czemu Application nie zależy od
/// konkretnego dostawcy bazy danych (zasada odwrócenia zależności).
/// </summary>
public interface IUniversityDbContext
{
    DbSet<Student> Studenci { get; }

    DbSet<StudentStudiowMagisterskich> StudenciMagisterscy { get; }

    DbSet<Profesor> Profesorowie { get; }

    DbSet<Gabinet> Gabinety { get; }

    DbSet<Wydzial> Wydzialy { get; }

    DbSet<Kurs> Kursy { get; }

    DbSet<Enrollment> Zapisy { get; }

    DbSet<LicznikIndeksow> LicznikiIndeksow { get; }

    DatabaseFacade Database { get; }

    ChangeTracker ChangeTracker { get; }

    EntityEntry Entry(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
