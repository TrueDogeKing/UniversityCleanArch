using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using University.Application.Abstractions;
using University.Application.Liczniki;
using University.Domain.Entities;
using University.Domain.ValueObjects;

namespace University.Infrastructure.Persistence;

/// <summary>
/// Kontekst bazy danych systemu. Cała konfiguracja mapowania realizowana jest
/// przez Fluent API – klasy domenowe pozostają wolne od atrybutów EF Core (POCO).
/// </summary>
public class UniversityDbContext : DbContext, IUniversityDbContext
{
    public const string TabelaWymaganWstepnych = "WymaganiaWstepneKursow";
    public const string KolumnaDyskryminatora = "TypStudenta";
    public const string DyskryminatorInzynierski = "Inzynierski";
    public const string DyskryminatorMagisterski = "Magisterski";

    public UniversityDbContext(DbContextOptions<UniversityDbContext> options)
        : base(options)
    {
    }

    public DbSet<Student> Studenci => Set<Student>();

    public DbSet<StudentStudiowMagisterskich> StudenciMagisterscy => Set<StudentStudiowMagisterskich>();

    public DbSet<Profesor> Profesorowie => Set<Profesor>();

    public DbSet<Gabinet> Gabinety => Set<Gabinet>();

    public DbSet<Wydzial> Wydzialy => Set<Wydzial>();

    public DbSet<Kurs> Kursy => Set<Kurs>();

    public DbSet<Enrollment> Zapisy => Set<Enrollment>();

    public DbSet<LicznikIndeksow> LicznikiIndeksow => Set<LicznikIndeksow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        KonfigurujStudenta(modelBuilder);
        KonfigurujStudentaMagisterskiego(modelBuilder);
        KonfigurujProfesora(modelBuilder);
        KonfigurujGabinet(modelBuilder);
        KonfigurujWydzial(modelBuilder);
        KonfigurujKurs(modelBuilder);
        KonfigurujZapis(modelBuilder);
        KonfigurujLicznik(modelBuilder);
    }

    private static void KonfigurujStudenta(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(b =>
        {
            b.ToTable("Studenci");
            b.HasKey(s => s.Id);

            b.Property(s => s.Imie).HasMaxLength(100).IsRequired();
            b.Property(s => s.Nazwisko).HasMaxLength(100).IsRequired();
            b.Property(s => s.IndeksUczelniany).HasMaxLength(20).IsRequired();
            b.HasIndex(s => s.IndeksUczelniany).IsUnique();

            // Shadow Property – istnieje tylko w modelu i w tabeli.
            b.Property<DateTime>(ZnacznikModyfikacji.NazwaWlasciwosci);

            // Typ własny (Owned Entity) – kolumny adresu w tabeli Studenci.
            b.OwnsOne(s => s.AdresZamieszkania, a => KonfigurujAdres(a));
            b.Navigation(s => s.AdresZamieszkania).IsRequired();

            // Dziedziczenie TPH – jedna tabela z kolumną dyskryminatora.
            b.HasDiscriminator<string>(KolumnaDyskryminatora)
                .HasValue<Student>(DyskryminatorInzynierski)
                .HasValue<StudentStudiowMagisterskich>(DyskryminatorMagisterski);
            b.Property<string>(KolumnaDyskryminatora).HasMaxLength(20);
        });
    }

    private static void KonfigurujStudentaMagisterskiego(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudentStudiowMagisterskich>(b =>
        {
            b.Property(s => s.TematPracyDyplomowej).HasMaxLength(300);

            // Usunięcie promotora nie usuwa magistranta – klucz obcy przyjmuje NULL.
            b.HasOne(s => s.Promotor)
                .WithMany(p => p.Dyplomanci)
                .HasForeignKey(s => s.PromotorId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void KonfigurujProfesora(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profesor>(b =>
        {
            b.ToTable("Profesorowie");
            b.HasKey(p => p.Id);

            b.Property(p => p.Imie).HasMaxLength(100).IsRequired();
            b.Property(p => p.Nazwisko).HasMaxLength(100).IsRequired();
            b.Property(p => p.TytulNaukowy).HasMaxLength(50).IsRequired();
            b.Property(p => p.IndeksUczelniany).HasMaxLength(20).IsRequired();
            b.HasIndex(p => p.IndeksUczelniany).IsUnique();

            b.Property<DateTime>(ZnacznikModyfikacji.NazwaWlasciwosci);

            b.OwnsOne(p => p.AdresZamieszkania, a => KonfigurujAdres(a));
            b.Navigation(p => p.AdresZamieszkania).IsRequired();

            // Relacja 1:1 – klucz obcy po stronie gabinetu.
            b.HasOne(p => p.Gabinet)
                .WithOne(g => g.Profesor)
                .HasForeignKey<Gabinet>(g => g.ProfesorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void KonfigurujAdres<TWlasciciel>(OwnedNavigationBuilder<TWlasciciel, Adres> a)
        where TWlasciciel : class
    {
        a.Property(x => x.Ulica).HasColumnName("Ulica").HasMaxLength(200).IsRequired();
        a.Property(x => x.Miasto).HasColumnName("Miasto").HasMaxLength(100).IsRequired();
        a.Property(x => x.KodPocztowy).HasColumnName("KodPocztowy").HasMaxLength(6).IsRequired();
    }

    private static void KonfigurujGabinet(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Gabinet>(b =>
        {
            b.ToTable("Gabinety");
            b.HasKey(g => g.Id);
            b.Property(g => g.NumerPokoju).HasMaxLength(10).IsRequired();
            b.Property(g => g.Budynek).HasMaxLength(50).IsRequired();
        });
    }

    private static void KonfigurujWydzial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Wydzial>(b =>
        {
            b.ToTable("Wydzialy");
            b.HasKey(w => w.Id);
            b.Property(w => w.Nazwa).HasMaxLength(200).IsRequired();
        });
    }

    private static void KonfigurujKurs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Kurs>(b =>
        {
            b.ToTable("Kursy");
            b.HasKey(k => k.Id);

            b.Property(k => k.Nazwa).HasMaxLength(200).IsRequired();
            b.Property(k => k.KodKursu).HasMaxLength(20).IsRequired();
            b.HasIndex(k => k.KodKursu).IsUnique();

            b.HasOne(k => k.Wydzial)
                .WithMany(w => w.Kursy)
                .HasForeignKey(k => k.WydzialId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(k => k.Prowadzacy)
                .WithMany(p => p.ProwadzoneKursy)
                .HasForeignKey(k => k.ProwadzacyId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Samoodwołująca się relacja N:M. Jedna strona musi mieć Restrict,
            // ponieważ SQL Server nie dopuszcza wielu ścieżek kaskadowych
            // prowadzących do tej samej tabeli (cykl Kursy -> WymaganiaWstepneKursow).
            b.HasMany(k => k.WymaganiaWstepne)
                .WithMany(k => k.KursyZalezne)
                .UsingEntity<Dictionary<string, object>>(
                    TabelaWymaganWstepnych,
                    prawa => prawa.HasOne<Kurs>()
                        .WithMany()
                        .HasForeignKey("WymaganieId")
                        .OnDelete(DeleteBehavior.Restrict),
                    lewa => lewa.HasOne<Kurs>()
                        .WithMany()
                        .HasForeignKey("KursId")
                        .OnDelete(DeleteBehavior.Cascade),
                    laczaca =>
                    {
                        laczaca.ToTable(TabelaWymaganWstepnych);
                        laczaca.HasKey("KursId", "WymaganieId");
                    });
        });
    }

    private static void KonfigurujZapis(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Enrollment>(b =>
        {
            b.ToTable("Zapisy");
            b.HasKey(e => new { e.StudentId, e.KursId });

            b.Property(e => e.Semestr).HasMaxLength(20).IsRequired();

            b.HasOne(e => e.Student)
                .WithMany(s => s.Zapisy)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.Kurs)
                .WithMany(k => k.Zapisy)
                .HasForeignKey(e => e.KursId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void KonfigurujLicznik(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LicznikIndeksow>(b =>
        {
            b.ToTable("LicznikiIndeksow");
            b.HasKey(l => l.Prefix);
            b.Property(l => l.Prefix).HasMaxLength(5);

            // Token współbieżności – optymistyczna ochrona przed równoczesnym
            // nadaniem tego samego numeru przez dwie transakcje.
            b.Property(l => l.AktualnaWartosc).IsConcurrencyToken();

            b.HasData(PrefiksyIndeksow.WartosciPoczatkowe
                .Select(p => new LicznikIndeksow { Prefix = p.Key, AktualnaWartosc = p.Value })
                .ToArray());
        });
    }
}
