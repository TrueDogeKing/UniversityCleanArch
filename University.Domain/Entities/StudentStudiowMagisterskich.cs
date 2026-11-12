namespace University.Domain.Entities;

/// <summary>
/// Student studiów drugiego stopnia realizujący pracę dyplomową
/// pod opieką (opcjonalnego) promotora.
/// </summary>
public class StudentStudiowMagisterskich : Student
{
    public string TematPracyDyplomowej { get; set; } = string.Empty;

    /// <summary>
    /// Klucz obcy promotora. Wartość <c>null</c> oznacza brak promotora –
    /// m.in. po usunięciu profesora (DeleteBehavior.SetNull).
    /// </summary>
    public int? PromotorId { get; set; }

    public Profesor? Promotor { get; set; }
}
