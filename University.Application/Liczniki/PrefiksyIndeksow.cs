namespace University.Application.Liczniki;

/// <summary>
/// Prefiksy numerów indeksów oraz wartości początkowe liczników.
/// Konwencja (wariant A): licznik przechowuje ostatnio nadany numer,
/// zatem pierwszy student otrzyma "S1001", a pierwszy profesor "P101".
/// </summary>
public static class PrefiksyIndeksow
{
    public const string Student = "S";

    public const string Profesor = "P";

    public const string Doktorant = "D";

    public static readonly IReadOnlyDictionary<string, int> WartosciPoczatkowe =
        new Dictionary<string, int>
        {
            [Student] = 1000,
            [Profesor] = 100,
            [Doktorant] = 0,
        };
}
