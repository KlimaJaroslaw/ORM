# 🗄️ Lightweight C# ORM

**Custom Object-Relational Mapper for .NET 9 & SQLite**

**Lightweight C# ORM** to autorski, wydajny system mapowania obiektowo-relacyjnego napisany od zera w środowisku .NET 9. Projekt powstał jako praktyczne studium zaawansowanych wzorców projektowych (GoF oraz POEAA). Zapewnia pełną kontrolę nad procesem generowania kodu SQL, zarządzaniem stanem obiektów oraz materializacją danych, rozwiązując klasyczny problem *impedance mismatch* pomiędzy światem obiektowym (C#) a relacyjnym (SQLite).

---

## 🚀 Kluczowe Funkcje
**Własny LINQ Provider:** Bezpieczne budowanie zapytań (filtrowanie, sortowanie, paginacja) prosto z kodu C#. Zapytania są leniwie ewaluowane (Deferred Execution) i tłumaczone na bezpieczny przed SQL Injection kod bazy danych.

**Wysoka Wydajność (Zero Runtime Reflection):** Refleksja używana jest tylko raz – podczas startu aplikacji. Materializacja obiektów (`ObjectMaterializer`) odbywa się za pomocą dynamicznie kompilowanych Drzew Wyrażeń (Expression Trees), co daje wydajność bliską natywnemu kodowi.

**Zaawansowane Mapowanie Dziedziczenia:** Pełne wsparcie dla polimorfizmu relacyjnego z automatycznym doborem strategii:
* **TPH** (Table Per Hierarchy) - jedna tabela z kolumną dyskryminatora (domyślnie).
* **TPC** (Table Per Concrete Class) - osobne tabele dla klas pochodnych.
* **TPT** (Table Per Type) - znormalizowane tabele łączone kluczami obcymi.



**Zarządzanie Stanem i Pamięć Podręczna:** Wbudowany `ChangeTracker` śledzący zmiany encji oraz First-Level Cache (Identity Map) zapobiegający wielokrotnemu pobieraniu tych samych danych.

**Podejście Code-First:** Automatyczne mapowanie w oparciu o konwencje nazewnictwa oraz atrybuty konfiguracyjne (`[Table]`, `[Column]`, `[Key]`, `[Ignore]`, `[ForeignKey]`).

---

## 🧩 Zastosowane Wzorce Projektowe

Jako że projekt skupia się na czystości architektury, pod maską zaimplementowano szereg wzorców ułatwiających rozbudowę i testowanie:

* **Unit of Work & Identity Map:** Scentralizowane zarządzanie transakcjami (`DbContext`) i tożsamością obiektów w pamięci.
* **Visitor:** Do przechodzenia po strukturze zapytań C# i translacji ich na zapytania SQL (`SqlExpressionVisitor`).
* **Builder & Director:** Do bezkolizyjnego, topologicznego konstruowania map obiektów z uwzględnieniem zależności (rodzic-dziecko).
* **Strategy:** Do dynamicznego rozwiązywania strategii nazewnictwa i obsługi dziedziczenia encji.

---

## ⚙️ Wymagania i Uruchomienie (Docker)

Aby zapewnić pełną powtarzalność i wyeliminować problem braku zależności na lokalnym komputerze, projekt został w 100% skonteneryzowany. Nie musisz instalować środowiska .NET.

**Wymagania:** Zainstalowany Docker.

1. Sklonuj repozytorium i przejdź do folderu z projektem.
2. Zbuduj obraz kontenera (pobierze on środowisko .NET 9 SDK oraz zależności):
```bash
docker build -t orm .

```


3. Uruchom kontener (zbuduje on projekt i automatycznie odpali wszystkie testy jednostkowe `xUnit`):
```bash
docker run orm

```



Jeśli proces zakończy się bez błędów, oznacza to, że silnik ORM, mapowanie relacji oraz generowanie SQL działają poprawnie!

---

## 🛠️ Architektura (Warstwy Systemu)

System podzielony jest na hermetyczne warstwy zgodnie z zasadą *Separation of Concerns*:

1. **API Layer (`DbContext`, `DbSet<T>`):** Prosty interfejs dla programisty.
2. **Core Runtime (`ChangeTracker`, `UnitOfWork`):** Serce systemu, monitorujące stan encji.
3. **Mapping Engine (`MetadataStore`):** Jednorazowo budowany graf metadanych, zoptymalizowany pod odczyty z czasem $O(1)$.
4. **Query Engine:** Silnik przetwarzający predykaty LINQ na pośredni model zapytań (`QueryModel`).
5. **SQL Implementation (`SqliteSqlGenerator`):** Niskopoziomowa warstwa ADO.NET dopasowana do dialektu SQLite.

---

## 🎓 Autorzy

Projekt stworzony w 2025 roku w ramach przedmiotu **Design Patterns**.

* **Zespół:**  Jarosław Klima, Kevin Stuka, Szymon Kowalski, Daniel Sterzel.

**Chcesz dowiedzieć się więcej o projekcie?**
Zapoznaj się z [Pełną Dokumentacją Techniczną](https://github.com/KlimaJaroslaw/ORM/blob/main/O_R_Mapping.pdf).
