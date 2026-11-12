# UniversityCleanArch

System administracji uczelnią zrealizowany w ramach przedmiotu
**Programowanie Aplikacji Lokalnych** (rok akademicki 2026/27, zadanie projektowe nr 2 – Entity Framework Core).

Autor: **TrueDogeKing**

---

## 1. Cel projektu

Celem projektu jest zaprojektowanie i implementacja wielowarstwowej aplikacji (typu *greenfield*)
zarządzającej danymi uczelni – studentami, profesorami, wydziałami, kursami, wymaganiami wstępnymi
oraz zapisami na kursy – z zastosowaniem:

- architektury **Clean Architecture** oraz elementów **Domain-Driven Design**,
- mapowania obiektowo-relacyjnego **Entity Framework Core 8** konfigurowanego wyłącznie przez **Fluent API**,
- transakcyjnego nadawania numerów indeksów,
- generatora danych testowych opartego na bibliotece **Bogus**,
- zapytań analitycznych LINQ wykonywanych w całości po stronie serwera bazy danych.

## 2. Stos technologiczny

| Obszar | Technologia |
|---|---|
| Język / platforma | C# 12, .NET 8 LTS |
| ORM | Entity Framework Core 8 (SqlServer, Tools, Design) |
| Baza danych | SQL Server LocalDB (`(localdb)\MSSQLLocalDB`) |
| Dane testowe | Bogus (lokalizacja `pl`) |
| Kontener DI / host | Microsoft.Extensions.Hosting, Microsoft.Extensions.DependencyInjection |
| Interfejs użytkownika | aplikacja konsolowa – Spectre.Console |

## 3. Architektura

Solucja składa się z czterech projektów, a zależności między nimi są zgodne z **regułą zależności**
(zależności skierowane wyłącznie do wewnątrz – w stronę domeny):

```
University.UI ──────────────┐
   │                        │
   ▼                        ▼
University.Infrastructure ─► University.Application ─► University.Domain
   │                                                       ▲
   └───────────────────────────────────────────────────────┘
```

| Projekt | Odpowiedzialność | Zależności |
|---|---|---|
| `University.Domain` | encje POCO, obiekt wartości `Adres` | brak |
| `University.Application` | logika aplikacji: liczniki indeksów, CRUD, generator, zapytania; port `IUniversityDbContext` | Domain |
| `University.Infrastructure` | `UniversityDbContext`, konfiguracja Fluent API, migracje, rejestracja DI | Domain, Application |
| `University.UI` | korzeń kompozycji (Composition Root), menu konsolowe | Domain, Application, Infrastructure |

Warstwa aplikacji komunikuje się z bazą wyłącznie przez interfejs `IUniversityDbContext`
(**zasada odwrócenia zależności**), którego implementacją jest `UniversityDbContext` w warstwie infrastruktury.
Każda akcja menu wykonywana jest w osobnym zakresie DI, a więc z nową instancją kontekstu
(jednostka pracy odpowiada pojedynczej operacji użytkownika).

## 4. Model dziedziny

### 4.1. Encje

| Klasa | Najważniejsze właściwości |
|---|---|
| `Student` | `Id`, `Imie`, `Nazwisko`, `IndeksUczelniany`, `RokStudiow`, `AdresZamieszkania`, `Zapisy` |
| `StudentStudiowMagisterskich : Student` | `TematPracyDyplomowej`, `PromotorId` (`int?`), `Promotor` |
| `Profesor` | `Id`, `Imie`, `Nazwisko`, `IndeksUczelniany`, `TytulNaukowy`, `AdresZamieszkania`, `ProwadzoneKursy`, `Dyplomanci`, `Gabinet` |
| `Gabinet` | `Id`, `NumerPokoju`, `Budynek`, `ProfesorId`, `Profesor` |
| `Wydzial` | `Id`, `Nazwa`, `Kursy` |
| `Kurs` | `Id`, `Nazwa`, `KodKursu`, `PunktyECTS`, `WydzialId`, `ProwadzacyId` (`int?`), `Zapisy`, `WymaganiaWstepne`, `KursyZalezne` |
| `Enrollment` | `StudentId`, `KursId` (klucz złożony), `Ocena` (`double?`), `Semestr` |
| `LicznikIndeksow` | `Prefix` (klucz główny), `AktualnaWartosc` |

### 4.2. Obiekt wartości `Adres`

`Adres` jest niezmiennym rekordem (`sealed record`, właściwości `init`). Poprawność gwarantuje
konstruktor publiczny `Adres(string ulica, string miasto, string kodPocztowy)`:

- dane wejściowe są przycinane (`Trim`),
- puste pola powodują `ArgumentException`,
- kod pocztowy musi spełniać wzorzec `^\d{2}-\d{3}$` – w przeciwnym razie `ArgumentException`.

Prywatny konstruktor bezparametrowy służy wyłącznie materializacji przez EF Core.
Interfejs użytkownika nie powiela reguł walidacji – przechwytuje wyjątek domenowy i ponawia pytanie.

### 4.3. Relacje

| Typ relacji | Realizacja | Zachowanie przy usunięciu |
|---|---|---|
| 1:1 | `Profesor` – `Gabinet` (FK `Gabinet.ProfesorId`, indeks unikalny) | `Cascade` |
| 1:N | `Wydzial` – `Kurs` | `Cascade` |
| 1:N | `Profesor` – `Kurs` (prowadzący, opcjonalny) | `SetNull` |
| 1:N | `Profesor` – `StudentStudiowMagisterskich` (promotor, opcjonalny) | `SetNull` |
| N:M z danymi | `Student` – `Kurs` przez `Enrollment` (`Ocena`, `Semestr`) | `Cascade` (obie strony) |
| N:M samoodwołująca | `Kurs` – `Kurs` (wymagania wstępne), tabela `WymaganiaWstepneKursow` | `Cascade` / `Restrict` |
| dziedziczenie | `Student` ← `StudentStudiowMagisterskich`, strategia **TPH**, dyskryminator `TypStudenta` | – |

**Uzasadnienie `Restrict` w tabeli `WymaganiaWstepneKursow`.** Oba klucze obce tabeli pośredniej wskazują
na tabelę `Kursy`. Ustawienie kaskady po obu stronach tworzy wiele ścieżek kaskadowych do tej samej tabeli,
czego SQL Server nie dopuszcza. Strona `WymaganieId` ma więc ustawione `Restrict`, a serwisy usuwające kurs
lub wydział jawnie usuwają powiązania prerekwizytów przed usunięciem kursu.

## 5. Warstwa infrastruktury

Konfiguracja w `UniversityDbContext.OnModelCreating` (Fluent API) obejmuje:

- klucze główne (w tym złożony klucz `Enrollment`), ograniczenia `HasMaxLength` / `IsRequired`,
- **Shadow Property** `OstatniaModyfikacja` (`DateTime`) dla `Student` i `Profesor` – kolumna istnieje
  w tabeli, ale nie w klasie domenowej,
- **typ własny** `Adres` mapowany przez `OwnsOne` w encjach `Student` i `Profesor`
  (kolumny `Ulica`, `Miasto`, `KodPocztowy` w tabeli właściciela),
- indeksy unikalne na `IndeksUczelniany` (studenci i profesorowie) oraz `KodKursu`,
- `DeleteBehavior.SetNull` dla promotora i prowadzącego kursu,
- tabelę `WymaganiaWstepneKursow` (`UsingEntity` + `ToTable`),
- dane początkowe liczników (`HasData`): `S = 1000`, `P = 100`, `D = 0`,
- token współbieżności na `LicznikIndeksow.AktualnaWartosc`.

Migracje stosowane są automatycznie przy starcie aplikacji (`Database.MigrateAsync()`),
zatem po usunięciu bazy schemat i liczniki zostają odtworzone przy kolejnym uruchomieniu.

## 6. Warstwa aplikacji

### 6.1. Nadawanie numerów indeksów

Przyjęto **wariant A**: `AktualnaWartosc` oznacza **ostatnio nadany** numer.
Pierwszy student otrzymuje zatem indeks `S1001`, a pierwszy profesor `P101`.
Prefiks `D` (doktoranci) jest zainicjalizowany wartością `0`.

**Utworzenie encji** (`LicznikIndeksowService.UtworzZNowymIndeksemAsync`) – w jednej jawnej transakcji:

1. pobranie licznika dla prefiksu,
2. inkrementacja licznika,
3. przypisanie numeru (np. `S1001`) i dodanie encji,
4. ustawienie Shadow Property `OstatniaModyfikacja`,
5. `SaveChanges` i zatwierdzenie transakcji (w razie błędu – wycofanie i wyczyszczenie kontekstu).

**Usunięcie encji** – usunięcie i korekta licznika w jednej transakcji:

- jeżeli numer usuwanego rekordu jest równy wartości licznika, licznik zmniejsza się o 1,
- usunięcie rekordu „ze środka” sekwencji nie zmienia licznika.

Dzięki temu nowo nadany numer nigdy nie koliduje z istniejącym rekordem. Dodatkową ochronę przed
równoczesnym nadaniem tego samego numeru stanowią token współbieżności licznika oraz indeks unikalny.

### 6.2. Operacje CRUD

Serwisy `StudentService`, `ProfesorService`, `KursService`, `WydzialService` oraz `LicznikIndeksowService`
udostępniają operacje tworzenia, odczytu, aktualizacji i usuwania. Każde utworzenie oraz każda aktualizacja
studenta lub profesora ustawia Shadow Property:

```csharp
_context.Entry(encja).Property("OstatniaModyfikacja").CurrentValue = DateTime.UtcNow;
```

Odczyty do prezentacji realizowane są przez `AsNoTracking()` i projekcje do DTO, w tym odczyt właściwości cienia
przez `EF.Property<DateTime>(s, "OstatniaModyfikacja")`.

### 6.3. Generator danych (Bogus)

`UniversityBogusGenerator` tworzy w **jednej transakcji** spójny zbiór danych: wydziały, profesorów z gabinetami,
kursy z wymaganiami wstępnymi, studentów inżynierskich oraz magistrantów z promotorami, a także zapisy z ocenami
(ok. 20% zapisów bez oceny). Założenia:

- numery indeksów **nie są losowane** – pochodzą z `LicznikIndeksowService.NadajKolejnyNumerAsync`,
  dlatego tworzą ciągłą sekwencję zgodną ze stanem licznika,
- adresy powstają wyłącznie przez walidujący konstruktor `Adres` (kod pocztowy w formacie `##-###`),
- wymagania wstępne wybierane są tylko spośród kursów wcześniejszych – graf zależności jest acykliczny,
- ziarno generatora jest stałe (`2026`), co zapewnia powtarzalność wyników.

## 7. Zapytania analityczne

Zapytania (`UniversityQueryService`) stosują `AsNoTracking()`, projekcję do DTO i są w całości tłumaczone na SQL.

| Nr | Opis | DTO |
|---|---|---|
| 1 | Profesor z największą łączną liczbą zapisów studentów na prowadzone kursy | `TopProfesorDto` |
| 2 | Dla wybranego wydziału: średnia ocen (tylko oceny niepuste, brak ocen → `0.0`) i liczba ocenionych studentów każdego kursu | `KursGpaDto` |
| 3 | Student o największym obciążeniu ECTS – kursy zapisane oraz ich **bezpośrednie** wymagania wstępne, każdy kurs liczony raz | `StudentTrudnoscDto` |

**Deduplikacja w zapytaniu 3.** Prosta suma ECTS kursów zapisanych i ich prerekwizytów zawyżałaby wynik,
gdy dwa kursy mają wspólne wymaganie lub wymaganie jest jednocześnie kursem zapisanym. Suma liczona jest więc
po *zbiorze* kursów spełniających warunek: „student jest zapisany na kurs” **lub** „kurs jest wymaganiem
kursu, na który student jest zapisany”:

```csharp
LacznyKosztECTS = kursy
    .Where(k => k.Zapisy.Any(z => z.StudentId == s.Id)
             || k.KursyZalezne.Any(zalezny => zalezny.Zapisy.Any(z => z.StudentId == s.Id)))
    .Sum(k => (int?)k.PunktyECTS) ?? 0
```

W SQL odpowiada to agregacji `SUM` po tabeli `Kursy` z predykatem `EXISTS … OR EXISTS …`, co jest
semantycznie równoważne sumie po `UNION` zbiorów identyfikatorów kursów.

## 8. Interfejs użytkownika

Aplikacja konsolowa (Spectre.Console) udostępnia:

- uruchomienie generatora danych (z opcjonalnym wyczyszczeniem bazy),
- przegląd studentów, profesorów, kursów, wydziałów i liczników – wraz z adresami
  i wartościami Shadow Property `OstatniaModyfikacja`,
- dodawanie nowego prefiksu indeksów,
- dodawanie i edycję studentów, magistrantów, profesorów (z gabinetem), kursów (z wymaganiami) i wydziałów,
- usuwanie encji z prezentacją stanu licznika przed i po operacji oraz skutków `SetNull`,
- uruchomienie trzech zapytań analitycznych (z wyborem wydziału dla zapytania 2).

## 9. Uruchomienie

Wymagania: .NET 8 SDK oraz SQL Server LocalDB (instalowany z Visual Studio).

```bash
dotnet restore
dotnet run --project University.UI
```

Łańcuch połączenia znajduje się w `University.UI/appsettings.json` (klucz `ConnectionStrings:University`).

Operacje na migracjach (narzędzie `dotnet-ef`):

```bash
dotnet tool install --global dotnet-ef
dotnet ef database drop --project University.Infrastructure --startup-project University.UI
dotnet ef migrations add <Nazwa> --project University.Infrastructure --startup-project University.UI
```

## 10. Scenariusze weryfikacji

| Nr | Scenariusz | Oczekiwany wynik |
|---|---|---|
| 1 | `dotnet ef database drop`, ponowne uruchomienie aplikacji | baza odtworzona z migracji, liczniki `S = 1000`, `P = 100` |
| 2 | Edycja adresu z kodem `12345` | `ArgumentException`, ponowne pytanie o adres |
| 3 | Edycja studenta | zmiana wartości `OstatniaModyfikacja` (wyświetlana przed i po zapisie) |
| 4 | Generator danych | indeksy `S1001…Sn` tworzą ciąg ciągły, `AktualnaWartosc` = ostatni numer |
| 5 | Usunięcie studenta ze środka sekwencji | licznik bez zmian |
| 6 | Usunięcie ostatniego studenta (np. licznik 1015, usunięcie `S1015`) | licznik 1014, nowy student otrzymuje `S1015` |
| 7 | Usunięcie profesora będącego promotorem | magistranci pozostają w bazie z `PromotorId = NULL` |
| 8 | Uruchomienie raportów | brak błędów translacji LINQ; wspólne prerekwizyty nie zawyżają sumy ECTS |

## 11. Odwzorowanie wymagań na implementację

| Część | Zakres | Lokalizacja |
|---|---|---|
| 1. Domain (25 pkt) | encje, `Adres`, relacje, dziedziczenie | `University.Domain/` |
| 2. Application (35 pkt) | CRUD + Shadow Property, liczniki, generator Bogus | `University.Application/` |
| 3. Infrastructure (20 pkt) | `UniversityDbContext`, Fluent API, migracje | `University.Infrastructure/` |
| 4. Zapytania (20 pkt) | 3 zapytania analityczne | `University.Application/Zapytania/` |
| 5. UI (20 pkt) | menu konsolowe | `University.UI/` |
