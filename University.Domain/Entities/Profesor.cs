using University.Domain.ValueObjects;

namespace University.Domain.Entities;

/// <summary>Pracownik naukowo-dydaktyczny uczelni.</summary>
public class Profesor
{
    public int Id { get; set; }

    public string Imie { get; set; } = string.Empty;

    public string Nazwisko { get; set; } = string.Empty;

    /// <summary>Numer indeksu nadawany wyłącznie przez licznik (prefiks "P").</summary>
    public string IndeksUczelniany { get; set; } = string.Empty;

    public string TytulNaukowy { get; set; } = string.Empty;

    public Adres AdresZamieszkania { get; set; } = null!;

    /// <summary>Kursy prowadzone przez profesora (relacja 1:N).</summary>
    public ICollection<Kurs> ProwadzoneKursy { get; set; } = new List<Kurs>();

    /// <summary>Magistranci, dla których profesor jest promotorem (relacja 1:N).</summary>
    public ICollection<StudentStudiowMagisterskich> Dyplomanci { get; set; } =
        new List<StudentStudiowMagisterskich>();

    /// <summary>Gabinet profesora (relacja 1:1).</summary>
    public Gabinet? Gabinet { get; set; }
}
