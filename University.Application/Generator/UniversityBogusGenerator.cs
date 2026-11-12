using Bogus;
using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Application.Liczniki;
using University.Domain.Entities;
using University.Domain.ValueObjects;

namespace University.Application.Generator;

/// <summary>Parametry generatora danych testowych.</summary>
public sealed record ParametryGeneratora(
    int LiczbaWydzialow = 4,
    int LiczbaProfesorow = 12,
    int LiczbaKursow = 24,
    int LiczbaStudentowInzynierskich = 30,
    int LiczbaMagistrantow = 12,
    int MinKursowNaStudenta = 3,
    int MaxKursowNaStudenta = 6,
    int Ziarno = 2026);

/// <summary>Podsumowanie wygenerowanego zbioru danych.</summary>
public sealed record RaportGeneratora(
    int Wydzialy,
    int Profesorowie,
    int Gabinety,
    int Kursy,
    int PowiazaniaWymaganWstepnych,
    int Studenci,
    int Magistranci,
    int Zapisy,
    string PierwszyIndeksStudenta,
    string OstatniIndeksStudenta,
    string PierwszyIndeksProfesora,
    string OstatniIndeksProfesora);

/// <summary>
/// Generator spójnego zbioru danych testowych oparty na bibliotece Bogus.
/// <para>
/// Numery indeksów nie są losowane – pobierane są z logiki warstwy aplikacji
/// (<see cref="LicznikIndeksowService.NadajKolejnyNumerAsync"/>), a adresy
/// tworzone są wyłącznie przez walidujący konstruktor <see cref="Adres"/>.
/// Całość wykonywana jest w jednej transakcji: błąd w dowolnym kroku
/// pozostawia bazę (i liczniki) w stanie sprzed generowania.
/// </para>
/// </summary>
public sealed class UniversityBogusGenerator
{
    private static readonly string[] NazwyWydzialow =
    {
        "Wydział Elektroniki, Telekomunikacji i Informatyki",
        "Wydział Fizyki Technicznej i Matematyki Stosowanej",
        "Wydział Zarządzania i Ekonomii",
        "Wydział Inżynierii Lądowej i Środowiska",
        "Wydział Chemiczny",
        "Wydział Mechaniczny",
    };

    private static readonly string[] NazwyKursow =
    {
        "Analiza matematyczna", "Algebra liniowa", "Matematyka dyskretna", "Podstawy programowania",
        "Programowanie obiektowe", "Algorytmy i struktury danych", "Bazy danych", "Systemy operacyjne",
        "Sieci komputerowe", "Inżynieria oprogramowania", "Programowanie aplikacji lokalnych",
        "Programowanie aplikacji internetowych", "Sztuczna inteligencja", "Uczenie maszynowe",
        "Grafika komputerowa", "Bezpieczeństwo systemów", "Architektura komputerów", "Fizyka",
        "Statystyka i rachunek prawdopodobieństwa", "Metody numeryczne", "Teoria obwodów",
        "Ekonomia", "Zarządzanie projektami", "Hurtownie danych", "Systemy rozproszone",
        "Kompilatory", "Języki formalne i automaty", "Przetwarzanie obrazów", "Robotyka",
        "Technologie chmurowe",
    };

    private static readonly string[] TytulyNaukowe =
        { "dr", "dr inż.", "dr hab.", "dr hab. inż.", "prof. dr hab.", "prof. dr hab. inż." };

    private static readonly string[] Budynki = { "EA", "ETI", "WZ", "NE", "GG" };

    private static readonly string[] Semestry = { "2025/26-Z", "2025/26-L", "2026/27-Z" };

    private static readonly double[] Oceny = { 2.0, 3.0, 3.5, 4.0, 4.5, 5.0 };

    private static readonly string[] PoczatkiTematow =
    {
        "Projekt i implementacja", "Analiza porównawcza", "Zastosowanie", "Optymalizacja",
        "Badanie wydajności", "Model i weryfikacja",
    };

    private static readonly string[] ObiektyTematow =
    {
        "systemu zarządzania danymi uczelni", "algorytmów uczenia maszynowego w diagnostyce",
        "mechanizmów ORM w aplikacjach .NET", "architektury mikroserwisowej",
        "metod wizualizacji danych przestrzennych", "protokołów komunikacji IoT",
        "systemu rekomendacyjnego", "rozproszonych baz danych",
    };

    private readonly IUniversityDbContext _context;
    private readonly LicznikIndeksowService _liczniki;

    public UniversityBogusGenerator(IUniversityDbContext context, LicznikIndeksowService liczniki)
    {
        _context = context;
        _liczniki = liczniki;
    }

    public Task<bool> CzyBazaZawieraDaneAsync(CancellationToken ct = default) =>
        _context.Studenci.AnyAsync(ct);

    public async Task<RaportGeneratora> GenerujAsync(ParametryGeneratora? parametry = null, CancellationToken ct = default)
    {
        var p = parametry ?? new ParametryGeneratora();
        Waliduj(p);

        Randomizer.Seed = new Random(p.Ziarno);
        var faker = new Faker("pl");

        await using var transakcja = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var wydzialy = GenerujWydzialy(p);
            var profesorowie = await GenerujProfesorowAsync(faker, p, ct);
            var kursy = GenerujKursy(faker, p, wydzialy, profesorowie);
            var powiazania = GenerujWymaganiaWstepne(faker, kursy);
            var studenci = await GenerujStudentowAsync(faker, p, profesorowie, ct);
            var zapisy = GenerujZapisy(faker, p, studenci, kursy);

            await _context.SaveChangesAsync(ct);
            await transakcja.CommitAsync(ct);

            var indeksyS = studenci.Select(s => s.IndeksUczelniany).ToList();
            var indeksyP = profesorowie.Select(x => x.IndeksUczelniany).ToList();
            return new RaportGeneratora(
                wydzialy.Count,
                profesorowie.Count,
                profesorowie.Count(x => x.Gabinet is not null),
                kursy.Count,
                powiazania,
                studenci.Count(s => s is not StudentStudiowMagisterskich),
                studenci.Count(s => s is StudentStudiowMagisterskich),
                zapisy,
                indeksyS.FirstOrDefault() ?? "—",
                indeksyS.LastOrDefault() ?? "—",
                indeksyP.FirstOrDefault() ?? "—",
                indeksyP.LastOrDefault() ?? "—");
        }
        catch
        {
            await transakcja.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Usuwa wszystkie dane domenowe i przywraca wartości początkowe liczników
    /// S/P/D (jedna transakcja). Pozostałe, ręcznie dodane prefiksy zostają.
    /// </summary>
    public async Task WyczyscDaneAsync(CancellationToken ct = default)
    {
        await using var transakcja = await _context.Database.BeginTransactionAsync(ct);

        var kursy = await _context.Kursy.Include(k => k.WymaganiaWstepne).ToListAsync(ct);
        foreach (var kurs in kursy)
        {
            kurs.WymaganiaWstepne.Clear();
        }

        await _context.SaveChangesAsync(ct);

        _context.Zapisy.RemoveRange(await _context.Zapisy.ToListAsync(ct));
        _context.Studenci.RemoveRange(await _context.Studenci.ToListAsync(ct));
        _context.Kursy.RemoveRange(kursy);
        _context.Gabinety.RemoveRange(await _context.Gabinety.ToListAsync(ct));
        _context.Profesorowie.RemoveRange(await _context.Profesorowie.ToListAsync(ct));
        _context.Wydzialy.RemoveRange(await _context.Wydzialy.ToListAsync(ct));
        await _liczniki.PrzywrocWartosciPoczatkoweAsync(ct);

        await _context.SaveChangesAsync(ct);
        await transakcja.CommitAsync(ct);
        _context.ChangeTracker.Clear();
    }

    private List<Wydzial> GenerujWydzialy(ParametryGeneratora p)
    {
        var wydzialy = NazwyWydzialow
            .Take(p.LiczbaWydzialow)
            .Select(nazwa => new Wydzial { Nazwa = nazwa })
            .ToList();

        _context.Wydzialy.AddRange(wydzialy);
        return wydzialy;
    }

    private async Task<List<Profesor>> GenerujProfesorowAsync(Faker f, ParametryGeneratora p, CancellationToken ct)
    {
        var profesorowie = new List<Profesor>(p.LiczbaProfesorow);
        for (var i = 0; i < p.LiczbaProfesorow; i++)
        {
            var profesor = new Profesor
            {
                Imie = f.Name.FirstName(),
                Nazwisko = f.Name.LastName(),
                TytulNaukowy = f.PickRandom(TytulyNaukowe),
                AdresZamieszkania = LosujAdres(f),
                IndeksUczelniany = await _liczniki.NadajKolejnyNumerAsync(PrefiksyIndeksow.Profesor, ct),
                Gabinet = new Gabinet
                {
                    Budynek = f.PickRandom(Budynki),
                    NumerPokoju = $"{f.Random.Number(1, 4)}{i + 1:00}",
                },
            };

            _context.Profesorowie.Add(profesor);
            ZnacznikModyfikacji.Ustaw(_context, profesor);
            profesorowie.Add(profesor);
        }

        return profesorowie;
    }

    private List<Kurs> GenerujKursy(Faker f, ParametryGeneratora p, List<Wydzial> wydzialy, List<Profesor> profesorowie)
    {
        var kursy = new List<Kurs>(p.LiczbaKursow);
        for (var i = 0; i < p.LiczbaKursow; i++)
        {
            var wydzial = wydzialy[i % wydzialy.Count];
            var nazwaBazowa = NazwyKursow[i % NazwyKursow.Length];
            var kurs = new Kurs
            {
                Nazwa = i < NazwyKursow.Length ? nazwaBazowa : $"{nazwaBazowa} {i / NazwyKursow.Length + 1}",
                KodKursu = $"K{i % wydzialy.Count + 1}-{i + 1:000}",
                PunktyECTS = f.Random.Number(2, 8),
                Wydzial = wydzial,
                // ok. 10% kursów bez prowadzącego – pokazuje nullowalny klucz obcy.
                Prowadzacy = f.Random.Bool(0.9f) ? f.PickRandom(profesorowie) : null,
            };

            _context.Kursy.Add(kurs);
            kursy.Add(kurs);
        }

        return kursy;
    }

    /// <summary>
    /// Wymagania wstępne wybierane są wyłącznie spośród kursów o niższym
    /// numerze porządkowym, dzięki czemu graf zależności jest acykliczny (DAG).
    /// </summary>
    private static int GenerujWymaganiaWstepne(Faker f, List<Kurs> kursy)
    {
        var liczba = 0;
        for (var i = 3; i < kursy.Count; i++)
        {
            var ile = f.Random.Number(0, 2);
            foreach (var wymaganie in f.PickRandom(kursy.Take(i), ile))
            {
                kursy[i].WymaganiaWstepne.Add(wymaganie);
                liczba++;
            }
        }

        return liczba;
    }

    private async Task<List<Student>> GenerujStudentowAsync(
        Faker f, ParametryGeneratora p, List<Profesor> profesorowie, CancellationToken ct)
    {
        var studenci = new List<Student>(p.LiczbaStudentowInzynierskich + p.LiczbaMagistrantow);

        for (var i = 0; i < p.LiczbaStudentowInzynierskich + p.LiczbaMagistrantow; i++)
        {
            Student student = i < p.LiczbaStudentowInzynierskich
                ? new Student { RokStudiow = f.Random.Number(1, 4) }
                : new StudentStudiowMagisterskich
                {
                    RokStudiow = f.Random.Number(1, 2),
                    TematPracyDyplomowej = $"{f.PickRandom(PoczatkiTematow)} {f.PickRandom(ObiektyTematow)}",
                    Promotor = f.PickRandom(profesorowie),
                };

            student.Imie = f.Name.FirstName();
            student.Nazwisko = f.Name.LastName();
            student.AdresZamieszkania = LosujAdres(f);
            student.IndeksUczelniany = await _liczniki.NadajKolejnyNumerAsync(PrefiksyIndeksow.Student, ct);

            _context.Studenci.Add(student);
            ZnacznikModyfikacji.Ustaw(_context, student);
            studenci.Add(student);
        }

        return studenci;
    }

    private static int GenerujZapisy(Faker f, ParametryGeneratora p, List<Student> studenci, List<Kurs> kursy)
    {
        var liczba = 0;
        foreach (var student in studenci)
        {
            var ile = Math.Min(f.Random.Number(p.MinKursowNaStudenta, p.MaxKursowNaStudenta), kursy.Count);
            foreach (var kurs in f.PickRandom(kursy, ile))
            {
                // ok. 20% zapisów bez oceny – kurs w trakcie realizacji.
                double? ocena = f.Random.Bool(0.8f) ? f.PickRandom(Oceny) : null;
                student.Zapisy.Add(new Enrollment
                {
                    Student = student,
                    Kurs = kurs,
                    Ocena = ocena,
                    Semestr = f.PickRandom(Semestry),
                });
                liczba++;
            }
        }

        return liczba;
    }

    /// <summary>Adres powstaje przez konstruktor z walidacją (kod pocztowy XX-XXX).</summary>
    private static Adres LosujAdres(Faker f) =>
        new(
            $"{f.Address.StreetName()} {f.Random.Number(1, 120)}",
            f.Address.City(),
            f.Address.ZipCode("##-###"));

    private static void Waliduj(ParametryGeneratora p)
    {
        if (p.LiczbaWydzialow < 1 || p.LiczbaWydzialow > NazwyWydzialow.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(p), $"Liczba wydziałów musi mieścić się w zakresie 1–{NazwyWydzialow.Length}.");
        }

        if (p.LiczbaProfesorow < 1 || p.LiczbaKursow < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(p), "Wymagany jest co najmniej jeden profesor i jeden kurs.");
        }

        if (p.MinKursowNaStudenta < 0 || p.MaxKursowNaStudenta < p.MinKursowNaStudenta)
        {
            throw new ArgumentOutOfRangeException(nameof(p), "Niepoprawny zakres liczby kursów na studenta.");
        }
    }
}
