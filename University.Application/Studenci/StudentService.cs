using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Application.Liczniki;
using University.Domain.Entities;
using University.Domain.ValueObjects;

namespace University.Application.Studenci;

/// <summary>
/// Operacje CRUD dla studentów (<see cref="Student"/>) i magistrantów
/// (<see cref="StudentStudiowMagisterskich"/>). Numer indeksu nie jest
/// przyjmowany z zewnątrz – nadaje go <see cref="LicznikIndeksowService"/>.
/// </summary>
public sealed class StudentService
{
    private readonly IUniversityDbContext _context;
    private readonly LicznikIndeksowService _liczniki;

    public StudentService(IUniversityDbContext context, LicznikIndeksowService liczniki)
    {
        _context = context;
        _liczniki = liczniki;
    }

    public Task<List<StudentWidokDto>> PobierzWszystkichAsync(CancellationToken ct = default) =>
        _context.Studenci
            .AsNoTracking()
            .OrderBy(s => s.IndeksUczelniany)
            .Select(s => new StudentWidokDto
            {
                Id = s.Id,
                Indeks = s.IndeksUczelniany,
                Imie = s.Imie,
                Nazwisko = s.Nazwisko,
                RokStudiow = s.RokStudiow,
                Ulica = s.AdresZamieszkania.Ulica,
                Miasto = s.AdresZamieszkania.Miasto,
                KodPocztowy = s.AdresZamieszkania.KodPocztowy,
                CzyMagistrant = s is StudentStudiowMagisterskich,
                TematPracy = ((StudentStudiowMagisterskich)s).TematPracyDyplomowej,
                PromotorId = ((StudentStudiowMagisterskich)s).PromotorId,
                Promotor = ((StudentStudiowMagisterskich)s).Promotor == null
                    ? null
                    : ((StudentStudiowMagisterskich)s).Promotor!.TytulNaukowy + " "
                      + ((StudentStudiowMagisterskich)s).Promotor!.Imie + " "
                      + ((StudentStudiowMagisterskich)s).Promotor!.Nazwisko,
                LiczbaZapisow = s.Zapisy.Count,
                OstatniaModyfikacja = EF.Property<DateTime>(s, ZnacznikModyfikacji.NazwaWlasciwosci),
            })
            .ToListAsync(ct);

    public async Task<Student> DodajStudentaAsync(
        string imie, string nazwisko, int rokStudiow, Adres adres, CancellationToken ct = default)
    {
        var student = new Student();
        UstawDanePodstawowe(student, imie, nazwisko, rokStudiow, adres);
        await DodajZIndeksemAsync(student, ct);
        return student;
    }

    public async Task<StudentStudiowMagisterskich> DodajMagistrantaAsync(
        string imie, string nazwisko, int rokStudiow, Adres adres,
        string tematPracy, int? promotorId, CancellationToken ct = default)
    {
        await SprawdzPromotoraAsync(promotorId, ct);

        var magistrant = new StudentStudiowMagisterskich
        {
            TematPracyDyplomowej = Wymagane(tematPracy, "Temat pracy dyplomowej"),
            PromotorId = promotorId,
        };
        UstawDanePodstawowe(magistrant, imie, nazwisko, rokStudiow, adres);
        await DodajZIndeksemAsync(magistrant, ct);
        return magistrant;
    }

    /// <summary>
    /// Aktualizuje dane studenta. Pola specyficzne dla magistranta są
    /// uwzględniane tylko dla <see cref="StudentStudiowMagisterskich"/>.
    /// Aktualizacja odświeża Shadow Property <c>OstatniaModyfikacja</c>.
    /// </summary>
    public async Task AktualizujAsync(
        int id, string imie, string nazwisko, int rokStudiow, Adres adres,
        string? tematPracy = null, int? promotorId = null, CancellationToken ct = default)
    {
        var student = await _context.Studenci.SingleOrDefaultAsync(s => s.Id == id, ct)
                      ?? throw new KeyNotFoundException($"Nie znaleziono studenta o Id={id}.");

        UstawDanePodstawowe(student, imie, nazwisko, rokStudiow, adres);

        if (student is StudentStudiowMagisterskich magistrant)
        {
            await SprawdzPromotoraAsync(promotorId, ct);
            if (tematPracy is not null)
            {
                magistrant.TematPracyDyplomowej = Wymagane(tematPracy, "Temat pracy dyplomowej");
            }

            magistrant.PromotorId = promotorId;
        }

        ZnacznikModyfikacji.Ustaw(_context, student);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Usuwa studenta (wraz z zapisami – kaskada) i w tej samej transakcji
    /// cofa licznik, jeżeli był to ostatnio nadany numer.
    /// </summary>
    /// <returns>Informacja, czy licznik został cofnięty; <c>null</c> gdy studenta nie znaleziono.</returns>
    public async Task<bool?> UsunAsync(int id, CancellationToken ct = default)
    {
        await using var transakcja = await _context.Database.BeginTransactionAsync(ct);

        var student = await _context.Studenci.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (student is null)
        {
            return null;
        }

        _context.Studenci.Remove(student);
        var cofnieto = await _liczniki.CofnijJesliOstatniAsync(student.IndeksUczelniany, ct);

        await _context.SaveChangesAsync(ct);
        await transakcja.CommitAsync(ct);
        return cofnieto;
    }

    private async Task DodajZIndeksemAsync(Student student, CancellationToken ct)
    {
        await _liczniki.UtworzZNowymIndeksemAsync(PrefiksyIndeksow.Student, indeks =>
        {
            student.IndeksUczelniany = indeks;
            _context.Studenci.Add(student);
            ZnacznikModyfikacji.Ustaw(_context, student);
        }, ct);
    }

    private async Task SprawdzPromotoraAsync(int? promotorId, CancellationToken ct)
    {
        if (promotorId is { } pid && !await _context.Profesorowie.AnyAsync(p => p.Id == pid, ct))
        {
            throw new KeyNotFoundException($"Nie znaleziono profesora (promotora) o Id={pid}.");
        }
    }

    private static void UstawDanePodstawowe(Student student, string imie, string nazwisko, int rokStudiow, Adres adres)
    {
        if (rokStudiow is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(rokStudiow), "Rok studiów musi mieścić się w zakresie 1–6.");
        }

        student.Imie = Wymagane(imie, "Imię");
        student.Nazwisko = Wymagane(nazwisko, "Nazwisko");
        student.RokStudiow = rokStudiow;
        student.AdresZamieszkania = adres ?? throw new ArgumentNullException(nameof(adres));
    }

    internal static string Wymagane(string? wartosc, string nazwaPola)
    {
        if (string.IsNullOrWhiteSpace(wartosc))
        {
            throw new ArgumentException($"Pole '{nazwaPola}' jest wymagane.");
        }

        return wartosc.Trim();
    }
}
