using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using University.Application.Generator;
using University.Application.Kursy;
using University.Application.Liczniki;
using University.Application.Profesorowie;
using University.Application.Studenci;
using University.Application.Wydzialy;
using University.Application.Zapytania;
using University.Domain.ValueObjects;
using static University.UI.Konsola.Wejscie;

namespace University.UI.Konsola;

/// <summary>
/// Konsolowy interfejs użytkownika (Spectre.Console). Każda akcja menu
/// wykonywana jest w osobnym zakresie DI, a więc z nową instancją DbContext
/// (jednostka pracy odpowiada jednej operacji użytkownika).
/// </summary>
public sealed class AplikacjaKonsolowa
{
    private const string Generator = "Generator danych (Bogus)";
    private const string Studenci = "Studenci – przegląd";
    private const string Profesorowie = "Profesorowie – przegląd";
    private const string Kursy = "Kursy – przegląd";
    private const string Wydzialy = "Wydziały – przegląd";
    private const string Liczniki = "Liczniki indeksów – przegląd / dodaj prefiks";
    private const string DodajStudenta = "Dodaj studenta / magistranta";
    private const string EdytujStudenta = "Edytuj studenta";
    private const string DodajProfesora = "Dodaj profesora";
    private const string EdytujProfesora = "Edytuj profesora";
    private const string ZarzadzajKursami = "Kursy – dodaj / edytuj";
    private const string ZarzadzajWydzialami = "Wydziały – dodaj / zmień nazwę";
    private const string Usun = "Usuń encję";
    private const string Raporty = "Raporty (zapytania analityczne)";
    private const string Wyjscie = "Wyjście";

    private readonly IServiceScopeFactory _scopeFactory;

    public AplikacjaKonsolowa(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task UruchomAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("UniversityCleanArch").Color(Color.SteelBlue));
            AnsiConsole.MarkupLine("[grey]Programowanie Aplikacji Lokalnych – EF Core, Clean Architecture[/]\n");

            var wybor = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Wybierz operację:[/]")
                    .PageSize(20)
                    .AddChoiceGroup("Dane", Generator)
                    .AddChoiceGroup("Przegląd", Studenci, Profesorowie, Kursy, Wydzialy, Liczniki)
                    .AddChoiceGroup("Zarządzanie", DodajStudenta, EdytujStudenta, DodajProfesora, EdytujProfesora,
                        ZarzadzajKursami, ZarzadzajWydzialami, Usun)
                    .AddChoiceGroup("Analiza", Raporty)
                    .AddChoices(Wyjscie));

            if (wybor == Wyjscie)
            {
                return;
            }

            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(wybor)}[/]").LeftJustified());

            await using var scope = _scopeFactory.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            try
            {
                switch (wybor)
                {
                    case Generator: await GeneratorAsync(sp); break;
                    case Studenci: await PokazStudentowAsync(sp); break;
                    case Profesorowie: await PokazProfesorowAsync(sp); break;
                    case Kursy: await PokazKursyAsync(sp); break;
                    case Wydzialy: await PokazWydzialyAsync(sp); break;
                    case Liczniki: await LicznikiAsync(sp); break;
                    case DodajStudenta: await DodajStudentaAsync(sp); break;
                    case EdytujStudenta: await EdytujStudentaAsync(sp); break;
                    case DodajProfesora: await DodajProfesoraAsync(sp); break;
                    case EdytujProfesora: await EdytujProfesoraAsync(sp); break;
                    case ZarzadzajKursami: await KursyZarzadzanieAsync(sp); break;
                    case ZarzadzajWydzialami: await WydzialyZarzadzanieAsync(sp); break;
                    case Usun: await UsunAsync(sp); break;
                    case Raporty: await RaportyAsync(sp); break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                AnsiConsole.MarkupLine($"[red]Operacja przerwana:[/] {E(ex.Message)}");
            }
            catch (DbUpdateException ex)
            {
                AnsiConsole.MarkupLine($"[red]Błąd zapisu do bazy:[/] {E(ex.InnerException?.Message ?? ex.Message)}");
            }

            Pauza();
        }
    }

    // ───────────────────────────── Generator ─────────────────────────────

    private static async Task GeneratorAsync(IServiceProvider sp)
    {
        var generator = sp.GetRequiredService<UniversityBogusGenerator>();

        if (await generator.CzyBazaZawieraDaneAsync())
        {
            if (!AnsiConsole.Confirm("Baza zawiera już dane. Wyczyścić ją (liczniki S/P/D wrócą do wartości początkowych) i wygenerować od nowa?", false))
            {
                return;
            }

            await AnsiConsole.Status().StartAsync("Czyszczenie danych...", async _ => await generator.WyczyscDaneAsync());
        }

        var domyslne = new ParametryGeneratora();
        var parametry = domyslne with
        {
            LiczbaProfesorow = Liczba("Liczba profesorów:", 1, 200, domyslne.LiczbaProfesorow),
            LiczbaKursow = Liczba("Liczba kursów:", 1, 300, domyslne.LiczbaKursow),
            LiczbaStudentowInzynierskich = Liczba("Liczba studentów inżynierskich:", 0, 2000, domyslne.LiczbaStudentowInzynierskich),
            LiczbaMagistrantow = Liczba("Liczba magistrantów:", 0, 2000, domyslne.LiczbaMagistrantow),
        };

        var raport = await AnsiConsole.Status()
            .StartAsync("Generowanie danych...", async _ => await generator.GenerujAsync(parametry));

        var tabela = new Table().Border(TableBorder.Rounded).AddColumn("Element").AddColumn(new TableColumn("Liczba / zakres").RightAligned());
        tabela.AddRow("Wydziały", raport.Wydzialy.ToString());
        tabela.AddRow("Profesorowie (z gabinetami)", $"{raport.Profesorowie} ({raport.Gabinety})");
        tabela.AddRow("Kursy", raport.Kursy.ToString());
        tabela.AddRow("Powiązania wymagań wstępnych", raport.PowiazaniaWymaganWstepnych.ToString());
        tabela.AddRow("Studenci inżynierscy", raport.Studenci.ToString());
        tabela.AddRow("Magistranci", raport.Magistranci.ToString());
        tabela.AddRow("Zapisy (Enrollment)", raport.Zapisy.ToString());
        tabela.AddRow("Indeksy studentów", $"{E(raport.PierwszyIndeksStudenta)} … {E(raport.OstatniIndeksStudenta)}");
        tabela.AddRow("Indeksy profesorów", $"{E(raport.PierwszyIndeksProfesora)} … {E(raport.OstatniIndeksProfesora)}");
        AnsiConsole.Write(tabela);

        await PokazTabeleLicznikowAsync(sp);
    }

    // ───────────────────────────── Przegląd ─────────────────────────────

    private static async Task PokazStudentowAsync(IServiceProvider sp)
    {
        var studenci = await sp.GetRequiredService<StudentService>().PobierzWszystkichAsync();
        var tabela = new Table().Border(TableBorder.Rounded)
            .AddColumns("Id", "Indeks", "Imię i nazwisko", "Rok", "Typ", "Adres", "Temat pracy / promotor", "Zapisy", "OstatniaModyfikacja");

        foreach (var s in studenci)
        {
            tabela.AddRow(
                s.Id.ToString(),
                E(s.Indeks),
                E($"{s.Imie} {s.Nazwisko}"),
                s.RokStudiow.ToString(),
                s.CzyMagistrant ? "[blue]mgr[/]" : "inż.",
                E(s.Adres),
                s.CzyMagistrant ? $"{E(s.TematPracy)}\n[grey]{E(s.Promotor ?? "(brak promotora – NULL)")}[/]" : "—",
                s.LiczbaZapisow.ToString(),
                E(Czas(s.OstatniaModyfikacja)));
        }

        AnsiConsole.Write(tabela);
        AnsiConsole.MarkupLine($"Łącznie: [bold]{studenci.Count}[/]");
    }

    private static async Task PokazProfesorowAsync(IServiceProvider sp)
    {
        var profesorowie = await sp.GetRequiredService<ProfesorService>().PobierzWszystkichAsync();
        var tabela = new Table().Border(TableBorder.Rounded)
            .AddColumns("Id", "Indeks", "Profesor", "Adres", "Gabinet", "Kursy", "Dyplomanci", "OstatniaModyfikacja");

        foreach (var p in profesorowie)
        {
            tabela.AddRow(
                p.Id.ToString(), E(p.Indeks), E(p.PelneImieNazwisko), E(p.Adres), E(p.Gabinet),
                p.LiczbaKursow.ToString(), p.LiczbaDyplomantow.ToString(), E(Czas(p.OstatniaModyfikacja)));
        }

        AnsiConsole.Write(tabela);
        AnsiConsole.MarkupLine($"Łącznie: [bold]{profesorowie.Count}[/]");
    }

    private static async Task PokazKursyAsync(IServiceProvider sp)
    {
        var kursy = await sp.GetRequiredService<KursService>().PobierzWszystkieAsync();
        var tabela = new Table().Border(TableBorder.Rounded)
            .AddColumns("Id", "Kod", "Nazwa", "ECTS", "Wydział", "Prowadzący", "Wymagania wstępne", "Zapisy");

        foreach (var k in kursy)
        {
            tabela.AddRow(
                k.Id.ToString(), E(k.KodKursu), E(k.Nazwa), k.PunktyECTS.ToString(), E(k.Wydzial),
                k.Prowadzacy is null ? "[grey](NULL)[/]" : E(k.Prowadzacy),
                k.WymaganiaWstepne.Count == 0 ? "—" : E(string.Join(", ", k.WymaganiaWstepne)),
                k.LiczbaZapisow.ToString());
        }

        AnsiConsole.Write(tabela);
        AnsiConsole.MarkupLine($"Łącznie: [bold]{kursy.Count}[/]");
    }

    private static async Task PokazWydzialyAsync(IServiceProvider sp)
    {
        var wydzialy = await sp.GetRequiredService<WydzialService>().PobierzWszystkieAsync();
        var tabela = new Table().Border(TableBorder.Rounded).AddColumns("Id", "Nazwa", "Liczba kursów");
        foreach (var w in wydzialy)
        {
            tabela.AddRow(w.Id.ToString(), E(w.Nazwa), w.LiczbaKursow.ToString());
        }

        AnsiConsole.Write(tabela);
    }

    private static async Task LicznikiAsync(IServiceProvider sp)
    {
        await PokazTabeleLicznikowAsync(sp);

        if (!AnsiConsole.Confirm("Dodać nowy prefiks?", false))
        {
            return;
        }

        var prefiks = Tekst("Prefiks (1–5 liter A–Z):").ToUpperInvariant();
        var wartosc = Liczba("Wartość początkowa (ostatnio nadany numer):", 0, 1_000_000, 0);
        await sp.GetRequiredService<LicznikIndeksowService>().DodajPrefiksAsync(prefiks, wartosc);
        AnsiConsole.MarkupLine($"[green]Dodano prefiks {E(prefiks)}.[/]");
        await PokazTabeleLicznikowAsync(sp);
    }

    private static async Task PokazTabeleLicznikowAsync(IServiceProvider sp)
    {
        var liczniki = await sp.GetRequiredService<LicznikIndeksowService>().PobierzWszystkieAsync();
        var tabela = new Table().Border(TableBorder.Rounded).Title("LicznikiIndeksow")
            .AddColumns("Prefix", "AktualnaWartosc (ostatnio nadany)", "Następny indeks");
        foreach (var l in liczniki)
        {
            tabela.AddRow(E(l.Prefix), l.AktualnaWartosc.ToString(),
                E(LicznikIndeksowService.Formatuj(l.Prefix, l.AktualnaWartosc + 1)));
        }

        AnsiConsole.Write(tabela);
    }

    // ───────────────────────────── Studenci ─────────────────────────────

    private static async Task DodajStudentaAsync(IServiceProvider sp)
    {
        var serwis = sp.GetRequiredService<StudentService>();
        var magistrant = WybierzOpcje("Typ studenta:", new[]
        {
            new Opcja<bool>("Student studiów inżynierskich", false),
            new Opcja<bool>("Student studiów magisterskich", true),
        });

        var imie = Tekst("Imię:");
        var nazwisko = Tekst("Nazwisko:");
        var rok = Liczba("Rok studiów:", 1, magistrant ? 2 : 4);
        var adres = PobierzAdres();

        if (!magistrant)
        {
            var student = await serwis.DodajStudentaAsync(imie, nazwisko, rok, adres);
            AnsiConsole.MarkupLine($"[green]Dodano studenta – nadany indeks: [bold]{E(student.IndeksUczelniany)}[/][/]");
            return;
        }

        var temat = Tekst("Temat pracy dyplomowej:");
        var promotorId = await WybierzProfesoraOpcjonalnieAsync(sp, "Promotor:", null);
        var mgr = await serwis.DodajMagistrantaAsync(imie, nazwisko, rok, adres, temat, promotorId);
        AnsiConsole.MarkupLine($"[green]Dodano magistranta – nadany indeks: [bold]{E(mgr.IndeksUczelniany)}[/][/]");
    }

    private static async Task EdytujStudentaAsync(IServiceProvider sp)
    {
        var serwis = sp.GetRequiredService<StudentService>();
        var s = Wybierz("Wybierz studenta:", await serwis.PobierzWszystkichAsync(),
            x => $"{x.Indeks}  {x.Imie} {x.Nazwisko}{(x.CzyMagistrant ? " (mgr)" : string.Empty)}");
        if (s is null)
        {
            return;
        }

        AnsiConsole.MarkupLine($"OstatniaModyfikacja przed zmianą: [yellow]{E(Czas(s.OstatniaModyfikacja))}[/]");

        var imie = Tekst("Imię:", s.Imie);
        var nazwisko = Tekst("Nazwisko:", s.Nazwisko);
        var rok = Liczba("Rok studiów:", 1, 6, s.RokStudiow);
        var adres = PobierzAdres(new Adres(s.Ulica, s.Miasto, s.KodPocztowy));

        string? temat = null;
        int? promotorId = null;
        if (s.CzyMagistrant)
        {
            temat = Tekst("Temat pracy dyplomowej:", s.TematPracy);
            promotorId = await WybierzProfesoraOpcjonalnieAsync(sp, $"Promotor (obecnie: {E(s.Promotor ?? "brak")}):", s.PromotorId);
        }

        await serwis.AktualizujAsync(s.Id, imie, nazwisko, rok, adres, temat, promotorId);

        var poZmianie = (await serwis.PobierzWszystkichAsync()).Single(x => x.Id == s.Id);
        AnsiConsole.MarkupLine($"[green]Zapisano.[/] OstatniaModyfikacja po zmianie: [yellow]{E(Czas(poZmianie.OstatniaModyfikacja))}[/]");
    }

    // ───────────────────────────── Profesorowie ─────────────────────────────

    private static async Task DodajProfesoraAsync(IServiceProvider sp)
    {
        var imie = Tekst("Imię:");
        var nazwisko = Tekst("Nazwisko:");
        var tytul = Tekst("Tytuł naukowy:", "dr inż.");
        var adres = PobierzAdres();
        var budynek = TekstOpcjonalny("Gabinet – budynek (enter = brak gabinetu):");
        var pokoj = budynek is null ? null : Tekst("Gabinet – numer pokoju:");

        var profesor = await sp.GetRequiredService<ProfesorService>()
            .DodajAsync(imie, nazwisko, tytul, adres, budynek, pokoj);
        AnsiConsole.MarkupLine($"[green]Dodano profesora – nadany indeks: [bold]{E(profesor.IndeksUczelniany)}[/][/]");
    }

    private static async Task EdytujProfesoraAsync(IServiceProvider sp)
    {
        var serwis = sp.GetRequiredService<ProfesorService>();
        var p = Wybierz("Wybierz profesora:", await serwis.PobierzWszystkichAsync(), x => $"{x.Indeks}  {x.PelneImieNazwisko}");
        if (p is null)
        {
            return;
        }

        AnsiConsole.MarkupLine($"OstatniaModyfikacja przed zmianą: [yellow]{E(Czas(p.OstatniaModyfikacja))}[/]");

        var imie = Tekst("Imię:", p.Imie);
        var nazwisko = Tekst("Nazwisko:", p.Nazwisko);
        var tytul = Tekst("Tytuł naukowy:", p.TytulNaukowy);
        var adres = PobierzAdres(new Adres(p.Ulica, p.Miasto, p.KodPocztowy));
        var budynek = TekstOpcjonalny("Gabinet – budynek (enter = brak gabinetu):", p.Budynek);
        var pokoj = budynek is null ? null : Tekst("Gabinet – numer pokoju:", p.NumerPokoju);

        await serwis.AktualizujAsync(p.Id, imie, nazwisko, tytul, adres, budynek, pokoj);

        var poZmianie = (await serwis.PobierzWszystkichAsync()).Single(x => x.Id == p.Id);
        AnsiConsole.MarkupLine($"[green]Zapisano.[/] OstatniaModyfikacja po zmianie: [yellow]{E(Czas(poZmianie.OstatniaModyfikacja))}[/]");
    }

    private static async Task<int?> WybierzProfesoraOpcjonalnieAsync(IServiceProvider sp, string tytul, int? obecny)
    {
        var profesorowie = await sp.GetRequiredService<ProfesorService>().PobierzWszystkichAsync();
        var opcje = new List<Opcja<int?>> { new("(brak)", null) };
        opcje.AddRange(profesorowie
            .OrderByDescending(p => p.Id == obecny)
            .Select(p => new Opcja<int?>($"{p.Indeks}  {p.PelneImieNazwisko}", p.Id)));
        return WybierzOpcje(tytul, opcje);
    }

    // ───────────────────────────── Kursy i wydziały ─────────────────────────────

    private static async Task KursyZarzadzanieAsync(IServiceProvider sp)
    {
        var serwis = sp.GetRequiredService<KursService>();
        var kursy = await serwis.PobierzWszystkieAsync();
        var edycja = kursy.Count > 0 && WybierzOpcje("Operacja:", new[]
        {
            new Opcja<bool>("Dodaj nowy kurs", false),
            new Opcja<bool>("Edytuj istniejący kurs", true),
        });

        KursWidokDto? edytowany = null;
        if (edycja)
        {
            edytowany = Wybierz("Wybierz kurs:", kursy, k => $"{k.KodKursu}  {k.Nazwa}");
            if (edytowany is null)
            {
                return;
            }
        }

        var wydzialy = await sp.GetRequiredService<WydzialService>().PobierzWszystkieAsync();
        if (wydzialy.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Najpierw dodaj wydział.[/]");
            return;
        }

        var nazwa = Tekst("Nazwa kursu:", edytowany?.Nazwa);
        var kod = Tekst("Kod kursu:", edytowany?.KodKursu);
        var ects = Liczba("Punkty ECTS:", 1, 30, edytowany?.PunktyECTS);
        var wydzialId = WybierzOpcje("Wydział:", wydzialy
            .OrderByDescending(w => w.Id == edytowany?.WydzialId)
            .Select(w => new Opcja<int>(w.Nazwa, w.Id)));
        var prowadzacyId = await WybierzProfesoraOpcjonalnieAsync(sp, "Prowadzący:", edytowany?.ProwadzacyId);

        var kandydaci = kursy.Where(k => k.Id != edytowany?.Id).ToList();
        if (edytowany is not null)
        {
            AnsiConsole.MarkupLine($"Obecne wymagania wstępne: [yellow]{E(edytowany.WymaganiaWstepne.Count == 0 ? "brak" : string.Join(", ", edytowany.WymaganiaWstepne))}[/]");
        }

        var wymagania = WybierzWiele("Wymagania wstępne (bezpośrednie):", kandydaci, k => $"{k.KodKursu}  {k.Nazwa}")
            .Select(k => k.Id)
            .ToList();

        if (edytowany is null)
        {
            await serwis.DodajAsync(nazwa, kod, ects, wydzialId, prowadzacyId, wymagania);
            AnsiConsole.MarkupLine("[green]Dodano kurs.[/]");
        }
        else
        {
            await serwis.AktualizujAsync(edytowany.Id, nazwa, kod, ects, wydzialId, prowadzacyId, wymagania);
            AnsiConsole.MarkupLine("[green]Zapisano zmiany kursu.[/]");
        }
    }

    private static async Task WydzialyZarzadzanieAsync(IServiceProvider sp)
    {
        var serwis = sp.GetRequiredService<WydzialService>();
        var wydzialy = await serwis.PobierzWszystkieAsync();
        var edycja = wydzialy.Count > 0 && WybierzOpcje("Operacja:", new[]
        {
            new Opcja<bool>("Dodaj wydział", false),
            new Opcja<bool>("Zmień nazwę wydziału", true),
        });

        if (!edycja)
        {
            await serwis.DodajAsync(Tekst("Nazwa wydziału:"));
            AnsiConsole.MarkupLine("[green]Dodano wydział.[/]");
            return;
        }

        var w = Wybierz("Wybierz wydział:", wydzialy, x => x.Nazwa);
        if (w is null)
        {
            return;
        }

        await serwis.ZmienNazweAsync(w.Id, Tekst("Nowa nazwa:", w.Nazwa));
        AnsiConsole.MarkupLine("[green]Zmieniono nazwę.[/]");
    }

    // ───────────────────────────── Usuwanie ─────────────────────────────

    private static async Task UsunAsync(IServiceProvider sp)
    {
        var rodzaj = WybierzOpcje("Co usunąć?", new[]
        {
            new Opcja<string>("Studenta", "S"),
            new Opcja<string>("Profesora", "P"),
            new Opcja<string>("Kurs", "K"),
            new Opcja<string>("Wydział (wraz z kursami)", "W"),
        });

        switch (rodzaj)
        {
            case "S":
            {
                var serwis = sp.GetRequiredService<StudentService>();
                var s = Wybierz("Wybierz studenta:", await serwis.PobierzWszystkichAsync(), x => $"{x.Indeks}  {x.Imie} {x.Nazwisko}");
                if (s is null || !AnsiConsole.Confirm($"Usunąć {s.Indeks}?", false))
                {
                    return;
                }

                await PokazTabeleLicznikowAsync(sp);
                var cofnieto = await serwis.UsunAsync(s.Id);
                AnsiConsole.MarkupLine(cofnieto == true
                    ? $"[green]Usunięto {E(s.Indeks)}.[/] Był to ostatnio nadany numer – [bold]licznik S cofnięto o 1[/]."
                    : $"[green]Usunięto {E(s.Indeks)}.[/] Numer ze środka sekwencji – licznik bez zmian.");
                await PokazTabeleLicznikowAsync(sp);
                break;
            }

            case "P":
            {
                var serwis = sp.GetRequiredService<ProfesorService>();
                var p = Wybierz("Wybierz profesora:", await serwis.PobierzWszystkichAsync(),
                    x => $"{x.Indeks}  {x.PelneImieNazwisko} (dyplomanci: {x.LiczbaDyplomantow}, kursy: {x.LiczbaKursow})");
                if (p is null || !AnsiConsole.Confirm($"Usunąć {p.Indeks}?", false))
                {
                    return;
                }

                var wynik = await serwis.UsunAsync(p.Id);
                if (wynik is null)
                {
                    return;
                }

                AnsiConsole.MarkupLine($"[green]Usunięto {E(p.Indeks)}[/] (gabinet usunięty kaskadowo).");
                AnsiConsole.MarkupLine($"SetNull: magistranci bez promotora: [bold]{wynik.OsieroceniDyplomanci}[/], kursy bez prowadzącego: [bold]{wynik.KursyBezProwadzacego}[/].");
                AnsiConsole.MarkupLine(wynik.CofnietoLicznik ? "Licznik P cofnięto o 1." : "Licznik P bez zmian.");
                await PokazTabeleLicznikowAsync(sp);
                break;
            }

            case "K":
            {
                var serwis = sp.GetRequiredService<KursService>();
                var k = Wybierz("Wybierz kurs:", await serwis.PobierzWszystkieAsync(), x => $"{x.KodKursu}  {x.Nazwa} (zapisy: {x.LiczbaZapisow})");
                if (k is not null && AnsiConsole.Confirm($"Usunąć kurs {k.KodKursu} wraz z zapisami?", false))
                {
                    await serwis.UsunAsync(k.Id);
                    AnsiConsole.MarkupLine("[green]Usunięto kurs.[/]");
                }

                break;
            }

            case "W":
            {
                var serwis = sp.GetRequiredService<WydzialService>();
                var w = Wybierz("Wybierz wydział:", await serwis.PobierzWszystkieAsync(), x => $"{x.Nazwa} (kursy: {x.LiczbaKursow})");
                if (w is not null && AnsiConsole.Confirm($"Usunąć wydział i jego {w.LiczbaKursow} kursów?", false))
                {
                    await serwis.UsunAsync(w.Id);
                    AnsiConsole.MarkupLine("[green]Usunięto wydział.[/]");
                }

                break;
            }
        }
    }

    // ───────────────────────────── Raporty ─────────────────────────────

    private static async Task RaportyAsync(IServiceProvider sp)
    {
        var zapytania = sp.GetRequiredService<UniversityQueryService>();

        AnsiConsole.Write(new Rule("[bold]Zapytanie 1 – profesor z największą liczbą studentów[/]").LeftJustified());
        var top = await zapytania.ProfesorZNajwiekszaLiczbaStudentowAsync();
        if (top is null)
        {
            AnsiConsole.MarkupLine("[yellow]Brak profesorów.[/]");
        }
        else
        {
            AnsiConsole.Write(new Table().Border(TableBorder.Rounded)
                .AddColumns("Indeks", "Profesor", "Liczba kursów", "Łączna liczba zapisów")
                .AddRow(E(top.Indeks), E(top.PelneImieNazwisko), top.LiczbaProwadzonychKursow.ToString(), top.LacznaLiczbaStudentow.ToString()));
        }

        AnsiConsole.Write(new Rule("[bold]Zapytanie 2 – średnia ocen kursów wydziału[/]").LeftJustified());
        var wydzialy = await sp.GetRequiredService<WydzialService>().PobierzWszystkieAsync();
        var wydzial = Wybierz("Wybierz wydział:", wydzialy, w => w.Nazwa);
        if (wydzial is not null)
        {
            var gpa = await zapytania.SredniaOcenKursowWydzialuAsync(wydzial.Id);
            var tabela = new Table().Border(TableBorder.Rounded).Title(E(wydzial.Nazwa))
                .AddColumns("Kod", "Kurs", "Ocenieni studenci", "Średnia (GPA)");
            foreach (var g in gpa)
            {
                tabela.AddRow(E(g.KodKursu), E(g.NazwaKursu), g.LiczbaOcenionychStudentow.ToString(), g.SredniaOcen.ToString("0.00"));
            }

            AnsiConsole.Write(tabela);
        }

        AnsiConsole.Write(new Rule("[bold]Zapytanie 3 – największe obciążenie ECTS (z wymaganiami wstępnymi)[/]").LeftJustified());
        var trudnosc = await zapytania.StudenciONajwiekszymObciazeniuEctsAsync(5);
        var t3 = new Table().Border(TableBorder.Rounded).AddColumns("Miejsce", "Indeks", "Student", "Zapisane kursy", "Łączny koszt ECTS");
        var miejsce = 1;
        foreach (var s in trudnosc)
        {
            t3.AddRow(miejsce == 1 ? "[bold gold1]1[/]" : miejsce.ToString(), E(s.Indeks), E(s.ImieNazwisko),
                s.LiczbaZapisanychKursow.ToString(), s.LacznyKosztECTS.ToString());
            miejsce++;
        }

        AnsiConsole.Write(t3);
    }
}
