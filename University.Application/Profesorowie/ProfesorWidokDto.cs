namespace University.Application.Profesorowie;

/// <summary>Projekcja profesora na potrzeby prezentacji.</summary>
public sealed class ProfesorWidokDto
{
    public int Id { get; init; }
    public string Indeks { get; init; } = string.Empty;
    public string TytulNaukowy { get; init; } = string.Empty;
    public string Imie { get; init; } = string.Empty;
    public string Nazwisko { get; init; } = string.Empty;
    public string Ulica { get; init; } = string.Empty;
    public string Miasto { get; init; } = string.Empty;
    public string KodPocztowy { get; init; } = string.Empty;
    public string? Budynek { get; init; }
    public string? NumerPokoju { get; init; }
    public int LiczbaKursow { get; init; }
    public int LiczbaDyplomantow { get; init; }
    public DateTime OstatniaModyfikacja { get; init; }

    public string PelneImieNazwisko => $"{TytulNaukowy} {Imie} {Nazwisko}";

    public string Adres => $"{Ulica}, {KodPocztowy} {Miasto}";

    public string Gabinet => Budynek is null ? "—" : $"{Budynek}, pok. {NumerPokoju}";
}
