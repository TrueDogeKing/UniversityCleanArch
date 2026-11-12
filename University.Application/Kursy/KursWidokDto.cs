namespace University.Application.Kursy;

/// <summary>Projekcja kursu na potrzeby prezentacji.</summary>
public sealed class KursWidokDto
{
    public int Id { get; init; }
    public string KodKursu { get; init; } = string.Empty;
    public string Nazwa { get; init; } = string.Empty;
    public int PunktyECTS { get; init; }
    public int WydzialId { get; init; }
    public string Wydzial { get; init; } = string.Empty;
    public int? ProwadzacyId { get; init; }
    public string? Prowadzacy { get; init; }
    public int LiczbaZapisow { get; init; }
    public List<string> WymaganiaWstepne { get; init; } = new();
}
