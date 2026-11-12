namespace University.Application.Wydzialy;

public sealed class WydzialWidokDto
{
    public int Id { get; init; }
    public string Nazwa { get; init; } = string.Empty;
    public int LiczbaKursow { get; init; }
}
