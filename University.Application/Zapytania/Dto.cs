namespace University.Application.Zapytania;

/// <summary>Wynik zapytania 1 – profesor o największej liczbie zapisów na prowadzone kursy.</summary>
public sealed class TopProfesorDto
{
    public string Indeks { get; init; } = string.Empty;
    public string PelneImieNazwisko { get; init; } = string.Empty;
    public int LiczbaProwadzonychKursow { get; init; }

    /// <summary>Łączna liczba zapisów (par student–kurs) na kursach profesora.</summary>
    public int LacznaLiczbaStudentow { get; init; }
}

/// <summary>Wynik zapytania 2 – średnia ocen (GPA) kursu wybranego wydziału.</summary>
public sealed class KursGpaDto
{
    public string KodKursu { get; init; } = string.Empty;
    public string NazwaKursu { get; init; } = string.Empty;
    public int LiczbaOcenionychStudentow { get; init; }

    /// <summary>Średnia z ocen niepustych; 0.0, gdy kurs nie ma żadnej oceny.</summary>
    public double SredniaOcen { get; init; }
}

/// <summary>Wynik zapytania 3 – student o najwyższym obciążeniu ECTS.</summary>
public sealed class StudentTrudnoscDto
{
    public string Indeks { get; init; } = string.Empty;
    public string ImieNazwisko { get; init; } = string.Empty;
    public int LiczbaZapisanychKursow { get; init; }

    /// <summary>Suma ECTS kursów zapisanych i ich bezpośrednich wymagań wstępnych (bez powtórzeń).</summary>
    public int LacznyKosztECTS { get; init; }
}
