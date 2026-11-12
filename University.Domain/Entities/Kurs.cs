namespace University.Domain.Entities;

/// <summary>Przedmiot (kurs) prowadzony na wydziale.</summary>
public class Kurs
{
    public int Id { get; set; }

    public string Nazwa { get; set; } = string.Empty;

    public string KodKursu { get; set; } = string.Empty;

    public int PunktyECTS { get; set; }

    public int WydzialId { get; set; }

    public Wydzial Wydzial { get; set; } = null!;

    /// <summary>Opcjonalny prowadzący – po usunięciu profesora przyjmuje wartość <c>null</c>.</summary>
    public int? ProwadzacyId { get; set; }

    public Profesor? Prowadzacy { get; set; }

    public ICollection<Enrollment> Zapisy { get; set; } = new List<Enrollment>();

    /// <summary>
    /// Bezpośrednie wymagania wstępne (prerekwizyty) – kursy, które należy
    /// zaliczyć przed tym kursem. Samoodwołująca się relacja N:M.
    /// </summary>
    public ICollection<Kurs> WymaganiaWstepne { get; set; } = new List<Kurs>();

    /// <summary>Kursy, dla których bieżący kurs stanowi wymaganie wstępne (strona odwrotna).</summary>
    public ICollection<Kurs> KursyZalezne { get; set; } = new List<Kurs>();
}
