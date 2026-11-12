using System.Text.RegularExpressions;

namespace University.Domain.ValueObjects;

/// <summary>
/// Niezmienny obiekt wartości (Value Object) reprezentujący adres zamieszkania.
/// Równość wyznaczana jest strukturalnie (semantyka <c>record</c>), a poprawność
/// danych gwarantuje konstruktor publiczny – nie istnieje możliwość utworzenia
/// niepoprawnego adresu z poziomu kodu aplikacji.
/// </summary>
public sealed record Adres
{
    /// <summary>Wzorzec polskiego kodu pocztowego w formacie XX-XXX.</summary>
    public const string WzorzecKoduPocztowego = @"^\d{2}-\d{3}$";

    private static readonly Regex KodPocztowyRegex =
        new(WzorzecKoduPocztowego, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Ulica { get; init; } = string.Empty;

    public string Miasto { get; init; } = string.Empty;

    public string KodPocztowy { get; init; } = string.Empty;

    /// <summary>
    /// Konstruktor bezparametrowy wymagany przez Entity Framework Core
    /// do materializacji obiektu z bazy danych. Niedostępny dla kodu aplikacji.
    /// </summary>
    private Adres()
    {
    }

    /// <summary>
    /// Tworzy zwalidowany adres. Dane wejściowe są przycinane (Trim).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Gdy którekolwiek pole jest puste lub kod pocztowy nie spełnia formatu XX-XXX.
    /// </exception>
    public Adres(string ulica, string miasto, string kodPocztowy)
    {
        var ulicaPrzycieta = ulica?.Trim();
        var miastoPrzyciete = miasto?.Trim();
        var kodPrzyciety = kodPocztowy?.Trim();

        if (string.IsNullOrWhiteSpace(ulicaPrzycieta))
        {
            throw new ArgumentException("Ulica nie może być pusta.", nameof(ulica));
        }

        if (string.IsNullOrWhiteSpace(miastoPrzyciete))
        {
            throw new ArgumentException("Miasto nie może być puste.", nameof(miasto));
        }

        if (string.IsNullOrWhiteSpace(kodPrzyciety) || !KodPocztowyRegex.IsMatch(kodPrzyciety))
        {
            throw new ArgumentException(
                $"Niepoprawny kod pocztowy '{kodPocztowy}'. Oczekiwany format: XX-XXX.",
                nameof(kodPocztowy));
        }

        Ulica = ulicaPrzycieta;
        Miasto = miastoPrzyciete;
        KodPocztowy = kodPrzyciety;
    }

    public override string ToString() => $"{Ulica}, {KodPocztowy} {Miasto}";
}
