namespace University.Domain.Entities;

/// <summary>
/// Licznik numerów indeksów dla danego prefiksu ("S" – studenci,
/// "P" – profesorowie, "D" – doktoranci).
/// Przyjęto konwencję (wariant A): <see cref="AktualnaWartosc"/> to
/// <b>ostatnio nadany</b> numer.
/// </summary>
public class LicznikIndeksow
{
    /// <summary>Prefiks indeksu – klucz główny.</summary>
    public string Prefix { get; set; } = string.Empty;

    public int AktualnaWartosc { get; set; }
}
