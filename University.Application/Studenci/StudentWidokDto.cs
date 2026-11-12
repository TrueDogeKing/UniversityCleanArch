namespace University.Application.Studenci;

/// <summary>Projekcja studenta na potrzeby prezentacji (zawiera wartość Shadow Property).</summary>
public sealed class StudentWidokDto
{
    public int Id { get; init; }
    public string Indeks { get; init; } = string.Empty;
    public string Imie { get; init; } = string.Empty;
    public string Nazwisko { get; init; } = string.Empty;
    public int RokStudiow { get; init; }
    public string Ulica { get; init; } = string.Empty;
    public string Miasto { get; init; } = string.Empty;
    public string KodPocztowy { get; init; } = string.Empty;
    public bool CzyMagistrant { get; init; }
    public string? TematPracy { get; init; }
    public int? PromotorId { get; init; }
    public string? Promotor { get; init; }
    public int LiczbaZapisow { get; init; }
    public DateTime OstatniaModyfikacja { get; init; }

    public string Adres => $"{Ulica}, {KodPocztowy} {Miasto}";
}
