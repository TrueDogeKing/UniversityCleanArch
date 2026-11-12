using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Application.Liczniki;
using University.Application.Studenci;
using University.Domain.Entities;
using University.Domain.ValueObjects;

namespace University.Application.Profesorowie;

/// <summary>Wynik usunięcia profesora – skutki relacji SetNull.</summary>
public sealed record WynikUsunieciaProfesora(bool CofnietoLicznik, int OsieroceniDyplomanci, int KursyBezProwadzacego);

/// <summary>Operacje CRUD dla profesorów wraz z gabinetem (relacja 1:1).</summary>
public sealed class ProfesorService
{
    private readonly IUniversityDbContext _context;
    private readonly LicznikIndeksowService _liczniki;

    public ProfesorService(IUniversityDbContext context, LicznikIndeksowService liczniki)
    {
        _context = context;
        _liczniki = liczniki;
    }

    public Task<List<ProfesorWidokDto>> PobierzWszystkichAsync(CancellationToken ct = default) =>
        _context.Profesorowie
            .AsNoTracking()
            .OrderBy(p => p.IndeksUczelniany)
            .Select(p => new ProfesorWidokDto
            {
                Id = p.Id,
                Indeks = p.IndeksUczelniany,
                TytulNaukowy = p.TytulNaukowy,
                Imie = p.Imie,
                Nazwisko = p.Nazwisko,
                Ulica = p.AdresZamieszkania.Ulica,
                Miasto = p.AdresZamieszkania.Miasto,
                KodPocztowy = p.AdresZamieszkania.KodPocztowy,
                Budynek = p.Gabinet != null ? p.Gabinet.Budynek : null,
                NumerPokoju = p.Gabinet != null ? p.Gabinet.NumerPokoju : null,
                LiczbaKursow = p.ProwadzoneKursy.Count,
                LiczbaDyplomantow = p.Dyplomanci.Count,
                OstatniaModyfikacja = EF.Property<DateTime>(p, ZnacznikModyfikacji.NazwaWlasciwosci),
            })
            .ToListAsync(ct);

    public async Task<Profesor> DodajAsync(
        string imie, string nazwisko, string tytulNaukowy, Adres adres,
        string? budynek, string? numerPokoju, CancellationToken ct = default)
    {
        var profesor = new Profesor();
        UstawDane(profesor, imie, nazwisko, tytulNaukowy, adres);
        UstawGabinet(profesor, budynek, numerPokoju);

        await _liczniki.UtworzZNowymIndeksemAsync(PrefiksyIndeksow.Profesor, indeks =>
        {
            profesor.IndeksUczelniany = indeks;
            _context.Profesorowie.Add(profesor);
            ZnacznikModyfikacji.Ustaw(_context, profesor);
        }, ct);

        return profesor;
    }

    public async Task AktualizujAsync(
        int id, string imie, string nazwisko, string tytulNaukowy, Adres adres,
        string? budynek, string? numerPokoju, CancellationToken ct = default)
    {
        var profesor = await _context.Profesorowie
                           .Include(p => p.Gabinet)
                           .SingleOrDefaultAsync(p => p.Id == id, ct)
                       ?? throw new KeyNotFoundException($"Nie znaleziono profesora o Id={id}.");

        UstawDane(profesor, imie, nazwisko, tytulNaukowy, adres);
        UstawGabinet(profesor, budynek, numerPokoju);
        ZnacznikModyfikacji.Ustaw(_context, profesor);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Usuwa profesora w jednej transakcji z ewentualnym cofnięciem licznika.
    /// Gabinet usuwany jest kaskadowo, natomiast magistranci i kursy pozostają
    /// w bazie z wartością <c>NULL</c> w kluczu obcym (DeleteBehavior.SetNull).
    /// </summary>
    public async Task<WynikUsunieciaProfesora?> UsunAsync(int id, CancellationToken ct = default)
    {
        await using var transakcja = await _context.Database.BeginTransactionAsync(ct);

        var profesor = await _context.Profesorowie.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (profesor is null)
        {
            return null;
        }

        var dyplomanci = await _context.StudenciMagisterscy.CountAsync(s => s.PromotorId == id, ct);
        var kursy = await _context.Kursy.CountAsync(k => k.ProwadzacyId == id, ct);

        _context.Profesorowie.Remove(profesor);
        var cofnieto = await _liczniki.CofnijJesliOstatniAsync(profesor.IndeksUczelniany, ct);

        await _context.SaveChangesAsync(ct);
        await transakcja.CommitAsync(ct);
        return new WynikUsunieciaProfesora(cofnieto, dyplomanci, kursy);
    }

    private static void UstawDane(Profesor profesor, string imie, string nazwisko, string tytulNaukowy, Adres adres)
    {
        profesor.Imie = StudentService.Wymagane(imie, "Imię");
        profesor.Nazwisko = StudentService.Wymagane(nazwisko, "Nazwisko");
        profesor.TytulNaukowy = StudentService.Wymagane(tytulNaukowy, "Tytuł naukowy");
        profesor.AdresZamieszkania = adres ?? throw new ArgumentNullException(nameof(adres));
    }

    private static void UstawGabinet(Profesor profesor, string? budynek, string? numerPokoju)
    {
        if (string.IsNullOrWhiteSpace(budynek) || string.IsNullOrWhiteSpace(numerPokoju))
        {
            profesor.Gabinet = null;
            return;
        }

        profesor.Gabinet ??= new Gabinet();
        profesor.Gabinet.Budynek = budynek.Trim();
        profesor.Gabinet.NumerPokoju = numerPokoju.Trim();
    }
}
