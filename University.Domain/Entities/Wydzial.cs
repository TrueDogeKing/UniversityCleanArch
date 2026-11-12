namespace University.Domain.Entities;

/// <summary>Jednostka organizacyjna uczelni grupująca kursy.</summary>
public class Wydzial
{
    public int Id { get; set; }

    public string Nazwa { get; set; } = string.Empty;

    /// <summary>Kursy oferowane przez wydział (relacja 1:N).</summary>
    public ICollection<Kurs> Kursy { get; set; } = new List<Kurs>();
}
