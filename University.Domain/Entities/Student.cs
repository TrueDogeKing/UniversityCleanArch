using University.Domain.ValueObjects;

namespace University.Domain.Entities;

/// <summary>
/// Student studiów pierwszego stopnia. Klasa bazowa hierarchii dziedziczenia
/// (TPH – Table per Hierarchy) dla <see cref="StudentStudiowMagisterskich"/>.
/// </summary>
public class Student
{
    public int Id { get; set; }

    public string Imie { get; set; } = string.Empty;

    public string Nazwisko { get; set; } = string.Empty;

    /// <summary>Numer indeksu nadawany wyłącznie przez licznik (prefiks "S").</summary>
    public string IndeksUczelniany { get; set; } = string.Empty;

    public int RokStudiow { get; set; }

    public Adres AdresZamieszkania { get; set; } = null!;

    /// <summary>Zapisy na kursy (relacja N:M z danymi dodatkowymi).</summary>
    public ICollection<Enrollment> Zapisy { get; set; } = new List<Enrollment>();
}
