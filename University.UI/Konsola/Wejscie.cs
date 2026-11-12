using Spectre.Console;
using University.Domain.ValueObjects;

namespace University.UI.Konsola;

/// <summary>Opcja wyboru z etykietą – pozwala reprezentować także wybór „brak”.</summary>
public sealed record Opcja<T>(string Etykieta, T Wartosc);

/// <summary>Pomocnicze metody pobierania danych od użytkownika.</summary>
public static class Wejscie
{
    public static string Tekst(string etykieta, string? domyslna = null)
    {
        var prompt = new TextPrompt<string>(etykieta)
            .Validate(v => string.IsNullOrWhiteSpace(v)
                ? ValidationResult.Error("[red]Pole jest wymagane.[/]")
                : ValidationResult.Success());

        if (domyslna is not null)
        {
            prompt.DefaultValue(domyslna);
        }

        return AnsiConsole.Prompt(prompt).Trim();
    }

    public static string? TekstOpcjonalny(string etykieta, string? domyslna = null)
    {
        var prompt = new TextPrompt<string>(etykieta).AllowEmpty();
        if (!string.IsNullOrEmpty(domyslna))
        {
            prompt.DefaultValue(domyslna);
        }

        var wynik = AnsiConsole.Prompt(prompt);
        return string.IsNullOrWhiteSpace(wynik) ? null : wynik.Trim();
    }

    public static int Liczba(string etykieta, int min, int max, int? domyslna = null)
    {
        var prompt = new TextPrompt<int>(etykieta)
            .Validate(v => v >= min && v <= max
                ? ValidationResult.Success()
                : ValidationResult.Error($"[red]Wartość musi należeć do zakresu {min}–{max}.[/]"));

        if (domyslna is { } d)
        {
            prompt.DefaultValue(d);
        }

        return AnsiConsole.Prompt(prompt);
    }

    /// <summary>
    /// Pobiera adres, powtarzając pytanie do skutku. Walidację wykonuje
    /// wyłącznie konstruktor obiektu wartości <see cref="Adres"/>
    /// (UI nie powiela reguł domenowych).
    /// </summary>
    public static Adres PobierzAdres(Adres? obecny = null)
    {
        while (true)
        {
            var ulica = Tekst("Ulica i numer:", obecny?.Ulica);
            var miasto = Tekst("Miasto:", obecny?.Miasto);
            var kod = Tekst("Kod pocztowy (XX-XXX):", obecny?.KodPocztowy);

            try
            {
                return new Adres(ulica, miasto, kod);
            }
            catch (ArgumentException ex)
            {
                AnsiConsole.MarkupLine($"[red]Błąd walidacji adresu:[/] {Markup.Escape(ex.Message)}");
            }
        }
    }

    public static T? Wybierz<T>(string tytul, IReadOnlyCollection<T> elementy, Func<T, string> etykieta)
        where T : class
    {
        if (elementy.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Brak elementów do wyboru.[/]");
            return null;
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<T>()
                .Title(tytul)
                .PageSize(15)
                .MoreChoicesText("[grey](przewiń, aby zobaczyć więcej)[/]")
                .UseConverter(e => Markup.Escape(etykieta(e)))
                .AddChoices(elementy));
    }

    public static T WybierzOpcje<T>(string tytul, IEnumerable<Opcja<T>> opcje)
    {
        var wybor = AnsiConsole.Prompt(
            new SelectionPrompt<Opcja<T>>()
                .Title(tytul)
                .PageSize(15)
                .UseConverter(o => Markup.Escape(o.Etykieta))
                .AddChoices(opcje));
        return wybor.Wartosc;
    }

    public static List<T> WybierzWiele<T>(string tytul, IReadOnlyCollection<T> elementy, Func<T, string> etykieta)
        where T : class
    {
        if (elementy.Count == 0)
        {
            return new List<T>();
        }

        return AnsiConsole.Prompt(
            new MultiSelectionPrompt<T>()
                .Title(tytul)
                .NotRequired()
                .PageSize(15)
                .InstructionsText("[grey](spacja – zaznacz, enter – zatwierdź)[/]")
                .UseConverter(e => Markup.Escape(etykieta(e)))
                .AddChoices(elementy));
    }

    public static string E(string? tekst) => Markup.Escape(tekst ?? "—");

    public static string Czas(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public static void Pauza()
    {
        AnsiConsole.MarkupLine("[grey]Naciśnij dowolny klawisz, aby kontynuować...[/]");
        Console.ReadKey(intercept: true);
    }
}
