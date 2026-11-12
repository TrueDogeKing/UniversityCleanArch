using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;

namespace University.Application.Zapytania;

/// <summary>
/// Zapytania analityczne. Wszystkie zapytania:
/// <list type="bullet">
/// <item>działają w trybie tylko do odczytu (<c>AsNoTracking</c>),</item>
/// <item>projektują wynik bezpośrednio do DTO (<c>Select</c>),</item>
/// <item>są w całości tłumaczone na SQL i wykonywane po stronie serwera –
/// agregacje (<c>COUNT</c>, <c>AVG</c>, <c>SUM</c>) nie są liczone w pamięci.</item>
/// </list>
/// </summary>
public sealed class UniversityQueryService
{
    private readonly IUniversityDbContext _context;

    public UniversityQueryService(IUniversityDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Zapytanie 1: profesor z największą łączną liczbą zapisów studentów
    /// na wszystkich prowadzonych przez niego kursach.
    /// Remis rozstrzygany jest numerem indeksu (wynik deterministyczny).
    /// </summary>
    public Task<TopProfesorDto?> ProfesorZNajwiekszaLiczbaStudentowAsync(CancellationToken ct = default) =>
        _context.Profesorowie
            .AsNoTracking()
            .Select(p => new TopProfesorDto
            {
                Indeks = p.IndeksUczelniany,
                PelneImieNazwisko = p.TytulNaukowy + " " + p.Imie + " " + p.Nazwisko,
                LiczbaProwadzonychKursow = p.ProwadzoneKursy.Count(),
                LacznaLiczbaStudentow = p.ProwadzoneKursy.SelectMany(k => k.Zapisy).Count(),
            })
            .OrderByDescending(d => d.LacznaLiczbaStudentow)
            .ThenBy(d => d.Indeks)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Zapytanie 2: dla każdego kursu wskazanego wydziału – średnia ocen
    /// (wyłącznie zapisy z oceną) oraz liczba ocenionych studentów.
    /// Kursy bez ocen otrzymują średnią 0.0 (<c>AVG</c> zwraca <c>NULL</c> → <c>COALESCE</c>).
    /// </summary>
    public Task<List<KursGpaDto>> SredniaOcenKursowWydzialuAsync(int wydzialId, CancellationToken ct = default) =>
        _context.Kursy
            .AsNoTracking()
            .Where(k => k.WydzialId == wydzialId)
            .Select(k => new KursGpaDto
            {
                KodKursu = k.KodKursu,
                NazwaKursu = k.Nazwa,
                LiczbaOcenionychStudentow = k.Zapisy.Count(z => z.Ocena != null),
                SredniaOcen = k.Zapisy.Where(z => z.Ocena != null).Average(z => z.Ocena) ?? 0.0,
            })
            .OrderByDescending(d => d.SredniaOcen)
            .ThenBy(d => d.KodKursu)
            .ToListAsync(ct);

    /// <summary>
    /// Zapytanie 3: student o najwyższym „koszcie” ECTS, rozumianym jako suma
    /// punktów kursów, na które jest zapisany, oraz ich <b>bezpośrednich</b>
    /// wymagań wstępnych.
    /// <para>
    /// Deduplikacja: zamiast sumować dwie listy (co zawyżałoby wynik, gdy dwa
    /// kursy mają wspólny prerekwizyt lub prerekwizyt jest jednocześnie kursem
    /// zapisanym), suma liczona jest po <i>zbiorze</i> kursów spełniających
    /// warunek „zapisany LUB jest wymaganiem kursu zapisanego”. Każdy kurs
    /// występuje w tym zbiorze co najwyżej raz – w SQL odpowiada to
    /// <c>SUM</c> po tabeli Kursy z predykatem <c>EXISTS … OR EXISTS …</c>,
    /// co jest semantycznie równoważne <c>UNION</c> zbiorów identyfikatorów.
    /// </para>
    /// </summary>
    public Task<List<StudentTrudnoscDto>> StudenciONajwiekszymObciazeniuEctsAsync(int ilu = 1, CancellationToken ct = default)
    {
        var kursy = _context.Kursy.AsNoTracking();

        return _context.Studenci
            .AsNoTracking()
            .Select(s => new StudentTrudnoscDto
            {
                Indeks = s.IndeksUczelniany,
                ImieNazwisko = s.Imie + " " + s.Nazwisko,
                LiczbaZapisanychKursow = s.Zapisy.Count(),
                LacznyKosztECTS = kursy
                    .Where(k => k.Zapisy.Any(z => z.StudentId == s.Id)
                                || k.KursyZalezne.Any(zalezny => zalezny.Zapisy.Any(z => z.StudentId == s.Id)))
                    .Sum(k => (int?)k.PunktyECTS) ?? 0,
            })
            .OrderByDescending(d => d.LacznyKosztECTS)
            .ThenBy(d => d.Indeks)
            .Take(ilu)
            .ToListAsync(ct);
    }
}
