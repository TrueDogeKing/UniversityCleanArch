namespace University.Domain.Entities;

/// <summary>Gabinet przypisany do dokładnie jednego profesora (relacja 1:1).</summary>
public class Gabinet
{
    public int Id { get; set; }

    public string NumerPokoju { get; set; } = string.Empty;

    public string Budynek { get; set; } = string.Empty;

    /// <summary>Klucz obcy po stronie zależnej relacji 1:1 (indeks unikalny).</summary>
    public int ProfesorId { get; set; }

    public Profesor Profesor { get; set; } = null!;
}
