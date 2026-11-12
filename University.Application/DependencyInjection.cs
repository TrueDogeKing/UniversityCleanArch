using Microsoft.Extensions.DependencyInjection;
using University.Application.Generator;
using University.Application.Kursy;
using University.Application.Liczniki;
using University.Application.Profesorowie;
using University.Application.Studenci;
using University.Application.Wydzialy;
using University.Application.Zapytania;

namespace University.Application;

/// <summary>Rejestracja usług warstwy Application w kontenerze DI.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LicznikIndeksowService>();
        services.AddScoped<StudentService>();
        services.AddScoped<ProfesorService>();
        services.AddScoped<KursService>();
        services.AddScoped<WydzialService>();
        services.AddScoped<UniversityQueryService>();
        services.AddScoped<UniversityBogusGenerator>();
        return services;
    }
}
