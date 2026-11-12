using Microsoft.EntityFrameworkCore;
using University.Application.Abstractions;
using University.Application.Studenci;
using University.Domain.Entities;

namespace University.Application.Kursy;

/// <summary>Operacje CRUD dla kursów, w tym zarządzanie wymaganiami wstępnymi.</summary>
public sealed class KursService
{
    private readonly IUniversityDbContext _context;

    public KursService(IUniversityDbContext context)
    {
        _context = context;
    }

    public Task<List<KursWidokDto>> PobierzWszystkieAsync(CancellationToken ct = default) =>
        _context.Kursy
            .AsNoTracking()
            .OrderBy(k => k.KodKursu)
            .Select(k => new KursWidokDto
            {
                Id = k.Id,
                KodKursu = k.KodKursu,
                Nazwa = k.Nazwa,
                PunktyECTS = k.PunktyECTS,
                WydzialId = k.WydzialId,
                Wydzial = k.Wydzial.Nazwa,
                ProwadzacyId = k.ProwadzacyId,
                Prowadzacy = k.Prowadzacy == null
                    ? null
                    : k.Prowadzacy.TytulNaukowy + " " + k.Prowadzacy.Imie + " " + k.Prowadzacy.Nazwisko,
                LiczbaZapisow = k.Zapisy.Count,
                WymaganiaWstepne = k.WymaganiaWstepne.OrderBy(w => w.KodKursu).Select(w => w.KodKursu).ToList(),
            })
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<Kurs> DodajAsync(
        string nazwa, string kodKursu, int punktyEcts, int wydzialId, int? prowadzacyId,
        IReadOnlyCollection<int> wymaganiaWstepneIds, CancellationToken ct = default)
    {
        var kurs = new Kurs();
        await UstawDaneAsync(kurs, nazwa, kodKursu, punktyEcts, wydzialId, prowadzacyId, ct);
        await UstawWymaganiaAsync(kurs, wymaganiaWstepneIds, ct);

        _context.Kursy.Add(kurs);
        await _context.SaveChangesAsync(ct);
        return kurs;
    }

    public async Task AktualizujAsync(
        int id, string nazwa, string kodKursu, int punktyEcts, int wydzialId, int? prowadzacyId,
        IReadOnlyCollection<int> wymaganiaWstepneIds, CancellationToken ct = default)
    {
        var kurs = await _context.Kursy
                       .Include(k => k.WymaganiaWstepne)
                       .SingleOrDefaultAsync(k => k.Id == id, ct)
                   ?? throw new KeyNotFoundException($"Nie znaleziono kursu o Id={id}.");

        if (wymaganiaWstepneIds.Contains(id))
        {
            throw new InvalidOperationException("Kurs nie może być wymaganiem wstępnym samego siebie.");
        }

        await UstawDaneAsync(kurs, nazwa, kodKursu, punktyEcts, wydzialId, prowadzacyId, ct);
        await UstawWymaganiaAsync(kurs, wymaganiaWstepneIds, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Usuwa kurs. Zapisy usuwane są kaskadowo; powiązania w tabeli
    /// WymaganiaWstepneKursow (po obu stronach) są usuwane jawnie, ponieważ
    /// jedna ze stron relacji ma ustawione <c>Restrict</c>.
    /// </summary>
    public async Task<bool> UsunAsync(int id, CancellationToken ct = default)
    {
        var kurs = await _context.Kursy
            .Include(k => k.WymaganiaWstepne)
            .Include(k => k.KursyZalezne)
            .AsSplitQuery()
            .SingleOrDefaultAsync(k => k.Id == id, ct);

        if (kurs is null)
        {
            return false;
        }

        kurs.WymaganiaWstepne.Clear();
        kurs.KursyZalezne.Clear();
        _context.Kursy.Remove(kurs);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private async Task UstawDaneAsync(
        Kurs kurs, string nazwa, string kodKursu, int punktyEcts, int wydzialId, int? prowadzacyId, CancellationToken ct)
    {
        if (punktyEcts is < 1 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(punktyEcts), "Liczba punktów ECTS musi mieścić się w zakresie 1–30.");
        }

        var kod = StudentService.Wymagane(kodKursu, "Kod kursu").ToUpperInvariant();
        if (await _context.Kursy.AnyAsync(k => k.KodKursu == kod && k.Id != kurs.Id, ct))
        {
            throw new InvalidOperationException($"Kurs o kodzie '{kod}' już istnieje.");
        }

        if (!await _context.Wydzialy.AnyAsync(w => w.Id == wydzialId, ct))
        {
            throw new KeyNotFoundException($"Nie znaleziono wydziału o Id={wydzialId}.");
        }

        if (prowadzacyId is { } pid && !await _context.Profesorowie.AnyAsync(p => p.Id == pid, ct))
        {
            throw new KeyNotFoundException($"Nie znaleziono profesora o Id={pid}.");
        }

        kurs.Nazwa = StudentService.Wymagane(nazwa, "Nazwa kursu");
        kurs.KodKursu = kod;
        kurs.PunktyECTS = punktyEcts;
        kurs.WydzialId = wydzialId;
        kurs.ProwadzacyId = prowadzacyId;
    }

    private async Task UstawWymaganiaAsync(Kurs kurs, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var wymagania = await _context.Kursy.Where(k => ids.Contains(k.Id)).ToListAsync(ct);
        if (wymagania.Count != ids.Distinct().Count())
        {
            throw new KeyNotFoundException("Co najmniej jeden ze wskazanych kursów wymaganych nie istnieje.");
        }

        kurs.WymaganiaWstepne.Clear();
        foreach (var wymaganie in wymagania)
        {
            kurs.WymaganiaWstepne.Add(wymaganie);
        }
    }
}
