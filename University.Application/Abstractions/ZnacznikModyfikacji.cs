namespace University.Application.Abstractions;

/// <summary>
/// Obsługa właściwości cienia (Shadow Property) <c>OstatniaModyfikacja</c>.
/// Właściwość istnieje wyłącznie w modelu EF Core (kolumna w tabeli),
/// nie jest natomiast częścią klas domenowych.
/// </summary>
public static class ZnacznikModyfikacji
{
    public const string NazwaWlasciwosci = "OstatniaModyfikacja";

    /// <summary>Ustawia znacznik czasu modyfikacji (UTC) dla śledzonej encji.</summary>
    public static void Ustaw(IUniversityDbContext context, object encja)
    {
        context.Entry(encja).Property(NazwaWlasciwosci).CurrentValue = DateTime.UtcNow;
    }
}
