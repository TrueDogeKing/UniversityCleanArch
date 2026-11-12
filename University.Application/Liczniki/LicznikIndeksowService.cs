using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Domain.Entities;

namespace University.Application.Liczniki;

/// <summary>
/// Logika nadawania i cofania numerów indeksów.
/// <para>
/// Konwencja (wariant A): <see cref="LicznikIndeksow.AktualnaWartosc"/> przechowuje
/// <b>ostatnio nadany</b> numer. Nadanie: licznik + 1. Usunięcie rekordu
/// o numerze równym licznikowi: licznik − 1. Usunięcie rekordu ze środka
/// sekwencji nie zmienia licznika.
/// </para>
/// </summary>
public sealed class LicznikIndeksowService
{
    private static readonly Regex FormatIndeksu =
        new(@"^(?<prefiks>[A-Z]+)(?<numer>\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FormatPrefiksu =
        new(@"^[A-Z]{1,5}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IUniversityDbContext _context;

    public LicznikIndeksowService(IUniversityDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Wykonuje operację tworzenia encji z nowym numerem indeksu w jednej jawnej
    /// transakcji: pobranie licznika, inkrementacja, przypisanie numeru,
    /// (wywołujący dodaje encję i ustawia Shadow Property), zapis i zatwierdzenie.
    /// W razie błędu transakcja jest wycofywana, a stan kontekstu czyszczony.
    /// </summary>
    public async Task<string> UtworzZNowymIndeksemAsync(
        string prefiks,
        Action<string> dodajEncjeZIndeksem,
        CancellationToken ct = default)
    {
        await using var transakcja = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var indeks = await NadajKolejnyNumerAsync(prefiks, ct);
            dodajEncjeZIndeksem(indeks);
            await _context.SaveChangesAsync(ct);
            await transakcja.CommitAsync(ct);
            return indeks;
        }
        catch
        {
            await transakcja.RollbackAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Zwiększa licznik dla prefiksu i zwraca nowy numer indeksu (np. "S1001").
    /// Metoda nie zapisuje zmian – musi być wywoływana w ramach transakcji
    /// otwartej przez wywołującego (np. <see cref="UtworzZNowymIndeksemAsync"/>
    /// lub generator danych). Kolejne wywołania w obrębie jednego kontekstu
    /// operują na tej samej śledzonej instancji licznika, dzięki czemu numery
    /// tworzą ciągłą sekwencję.
    /// </summary>
    public async Task<string> NadajKolejnyNumerAsync(string prefiks, CancellationToken ct = default)
    {
        var licznik = await PobierzLicznikAsync(prefiks, ct);
        licznik.AktualnaWartosc++;
        return Formatuj(licznik.Prefix, licznik.AktualnaWartosc);
    }

    /// <summary>
    /// Cofa licznik o 1, jeżeli usuwany indeks jest ostatnio nadanym numerem
    /// swojego prefiksu. Metoda nie zapisuje zmian – wywołujący wykonuje ją
    /// w tej samej transakcji co usunięcie encji.
    /// </summary>
    /// <returns><c>true</c>, gdy licznik został cofnięty.</returns>
    public async Task<bool> CofnijJesliOstatniAsync(string indeksUczelniany, CancellationToken ct = default)
    {
        var dopasowanie = FormatIndeksu.Match(indeksUczelniany);
        if (!dopasowanie.Success)
        {
            return false;
        }

        var prefiks = dopasowanie.Groups["prefiks"].Value;
        var numer = int.Parse(dopasowanie.Groups["numer"].Value, CultureInfo.InvariantCulture);

        var licznik = await _context.LicznikiIndeksow.SingleOrDefaultAsync(l => l.Prefix == prefiks, ct);
        if (licznik is null || licznik.AktualnaWartosc != numer)
        {
            return false;
        }

        licznik.AktualnaWartosc--;
        return true;
    }

    public Task<List<LicznikIndeksow>> PobierzWszystkieAsync(CancellationToken ct = default) =>
        _context.LicznikiIndeksow.AsNoTracking().OrderBy(l => l.Prefix).ToListAsync(ct);

    /// <summary>Dodaje nowy prefiks indeksów z zadaną wartością początkową.</summary>
    /// <exception cref="ArgumentException">Niepoprawny prefiks lub ujemna wartość.</exception>
    /// <exception cref="InvalidOperationException">Prefiks już istnieje.</exception>
    public async Task DodajPrefiksAsync(string prefiks, int wartoscPoczatkowa, CancellationToken ct = default)
    {
        var znormalizowany = prefiks?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!FormatPrefiksu.IsMatch(znormalizowany))
        {
            throw new ArgumentException("Prefiks musi składać się z 1–5 liter A–Z.", nameof(prefiks));
        }

        if (wartoscPoczatkowa < 0)
        {
            throw new ArgumentException("Wartość początkowa nie może być ujemna.", nameof(wartoscPoczatkowa));
        }

        if (await _context.LicznikiIndeksow.AnyAsync(l => l.Prefix == znormalizowany, ct))
        {
            throw new InvalidOperationException($"Prefiks '{znormalizowany}' już istnieje.");
        }

        _context.LicznikiIndeksow.Add(new LicznikIndeksow
        {
            Prefix = znormalizowany,
            AktualnaWartosc = wartoscPoczatkowa,
        });
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Przywraca wartości początkowe liczników S/P/D (wykorzystywane przy
    /// czyszczeniu bazy przed ponownym wygenerowaniem danych). Bez zapisu.
    /// </summary>
    public async Task PrzywrocWartosciPoczatkoweAsync(CancellationToken ct = default)
    {
        var liczniki = await _context.LicznikiIndeksow.ToListAsync(ct);
        foreach (var (prefiks, wartosc) in PrefiksyIndeksow.WartosciPoczatkowe)
        {
            var licznik = liczniki.SingleOrDefault(l => l.Prefix == prefiks);
            if (licznik is null)
            {
                _context.LicznikiIndeksow.Add(new LicznikIndeksow { Prefix = prefiks, AktualnaWartosc = wartosc });
            }
            else
            {
                licznik.AktualnaWartosc = wartosc;
            }
        }
    }

    public static string Formatuj(string prefiks, int numer) =>
        string.Create(CultureInfo.InvariantCulture, $"{prefiks}{numer}");

    private async Task<LicznikIndeksow> PobierzLicznikAsync(string prefiks, CancellationToken ct)
    {
        return await _context.LicznikiIndeksow.SingleOrDefaultAsync(l => l.Prefix == prefiks, ct)
               ?? throw new InvalidOperationException(
                   $"Brak licznika indeksów dla prefiksu '{prefiks}'. Dodaj prefiks w module liczników.");
    }
}
