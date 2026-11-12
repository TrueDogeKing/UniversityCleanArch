using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Application.Studenci;
using University.Domain.Entities;

namespace University.Application.Wydzialy;

/// <summary>Operacje CRUD dla wydziałów.</summary>
public sealed class WydzialService
{
    private readonly IUniversityDbContext _context;

    public WydzialService(IUniversityDbContext context)
    {
        _context = context;
    }

    public Task<List<WydzialWidokDto>> PobierzWszystkieAsync(CancellationToken ct = default) =>
        _context.Wydzialy
            .AsNoTracking()
            .OrderBy(w => w.Nazwa)
            .Select(w => new WydzialWidokDto { Id = w.Id, Nazwa = w.Nazwa, LiczbaKursow = w.Kursy.Count })
            .ToListAsync(ct);

    public async Task<Wydzial> DodajAsync(string nazwa, CancellationToken ct = default)
    {
        var wydzial = new Wydzial { Nazwa = StudentService.Wymagane(nazwa, "Nazwa wydziału") };
        _context.Wydzialy.Add(wydzial);
        await _context.SaveChangesAsync(ct);
        return wydzial;
    }

    public async Task ZmienNazweAsync(int id, string nazwa, CancellationToken ct = default)
    {
        var wydzial = await _context.Wydzialy.SingleOrDefaultAsync(w => w.Id == id, ct)
                      ?? throw new KeyNotFoundException($"Nie znaleziono wydziału o Id={id}.");
        wydzial.Nazwa = StudentService.Wymagane(nazwa, "Nazwa wydziału");
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Usuwa wydział wraz z kursami (kaskada). Wcześniej usuwane są powiązania
    /// wymagań wstępnych kursów wydziału, aby nie naruszyć ograniczenia Restrict.
    /// </summary>
    public async Task<bool> UsunAsync(int id, CancellationToken ct = default)
    {
        var wydzial = await _context.Wydzialy
            .Include(w => w.Kursy).ThenInclude(k => k.WymaganiaWstepne)
            .Include(w => w.Kursy).ThenInclude(k => k.KursyZalezne)
            .AsSplitQuery()
            .SingleOrDefaultAsync(w => w.Id == id, ct);

        if (wydzial is null)
        {
            return false;
        }

        foreach (var kurs in wydzial.Kursy)
        {
            kurs.WymaganiaWstepne.Clear();
            kurs.KursyZalezne.Clear();
        }

        _context.Wydzialy.Remove(wydzial);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
