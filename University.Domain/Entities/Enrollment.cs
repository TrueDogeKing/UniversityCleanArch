namespace University.Domain.Entities;

/// <summary>
/// Encja asocjacyjna relacji N:M Student–Kurs z danymi dodatkowymi (payload).
/// Klucz główny złożony: (<see cref="StudentId"/>, <see cref="KursId"/>).
/// </summary>
public class Enrollment
{
    public int StudentId { get; set; }

    public int KursId { get; set; }

    /// <summary>Ocena końcowa; <c>null</c> oznacza brak oceny.</summary>
    public double? Ocena { get; set; }

    /// <summary>Semestr realizacji, np. "2026/27-Z".</summary>
    public string Semestr { get; set; } = string.Empty;

    public Student Student { get; set; } = null!;

    public Kurs Kurs { get; set; } = null!;
}
