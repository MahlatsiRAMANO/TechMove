# TechMove – Contract & Service Management (EAPD7111 POE, Part 2)

ASP.NET Core MVC (.NET 8) "monolith" prototype with EF Core + SQL Server, a contract workflow,
PDF agreement uploads, live USD→ZAR conversion, and an xUnit test project.

## Solution layout

```
TechMove.sln
├── src/TechMove.Web                MVC app
│   ├── Models/                     Client, Contract, ServiceRequest, enums, search criteria
│   ├── Data/                       AppDbContext (Fluent API), SeedData, LINQ filter
│   │   └── Repositories/           Repository pattern – the only code that touches EF Core
│   ├── Services/                   Business logic (service layer)
│   │   ├── Workflow/               Contract state machine (status rules)
│   │   ├── Currency/               Currency maths + ExchangeRate-API client (Strategy)
│   │   └── Files/                  PDF validation + simulated file server
│   ├── Controllers/                Thin controllers – call services only
│   ├── ViewModels/  Views/  wwwroot/
│   └── App_Data/agreements/        Uploaded PDFs land here (outside wwwroot)
├── tests/TechMove.Tests            xUnit + Moq + EF Core InMemory
└── .github/workflows/dotnet.yml    Builds and runs all tests on every push
```

## How to run

1. Open `TechMove.sln` in **Visual Studio 2022** (17.8+) with the **ASP.NET and web development** workload (.NET 8 SDK).
2. The connection string is in `src/TechMove.Web/appsettings.json` and uses **LocalDB**:
   `Server=(localdb)\mssqllocaldb;Database=TechMoveDb;...`
   Change it if you use a full SQL Server instance (e.g. `Server=localhost;Database=TechMoveDb;Trusted_Connection=True;TrustServerCertificate=True`).
3. Create the database – **Tools → NuGet Package Manager → Package Manager Console**, set *Default project* to `TechMove.Web`, then:
   ```powershell
   Add-Migration InitialCreate
   Update-Database
   ```
   (CLI alternative: `dotnet tool install --global dotnet-ef`, then
   `dotnet ef migrations add InitialCreate --project src/TechMove.Web` and
   `dotnet ef database update --project src/TechMove.Web`.)
4. Generate the **migration script** for submission:
   ```powershell
   Script-Migration -Output Database/TechMove_InitialCreate.sql
   ```
5. Press **F5**. Seed data gives you 3 clients and 4 contracts (one of each status).
6. **Test Explorer → Run All** for the unit tests (take your screenshot here).

## Requirement → where it is

| Requirement | Implementation |
|---|---|
| SQL Server via EF Core | `Data/AppDbContext.cs` – keys, lengths, `decimal(18,2)`, enum→string, check constraints, indexes, `Restrict` deletes, all in Fluent API |
| Client / Contract / ServiceRequest | `Models/` – Client 1→* Contract 1→* ServiceRequest |
| Connection string not hard-coded | `appsettings.json` → `Program.cs` |
| Signed agreement PDF for every contract | Required on *Create Contract*; replace on *Details*; download link everywhere |
| File saved to server | `Services/Files/LocalFileStorageService.cs` – GUID file names (no overwrites), path-traversal guard, stored outside `wwwroot` |
| PDF-only validation | `PdfFileValidator` – `.pdf` extension, not empty, ≤10 MB, `%PDF` signature (a renamed `.exe` fails). Also checked in the browser (`wwwroot/js/site.js`) |
| No requests on Expired / On Hold | `Services/Workflow/ContractWorkflow.cs` – also blocks contracts past their end date; the Create page only lists eligible contracts, and the server re-checks |
| Status workflow | Draft → Active / On Hold / Expired; Active ↔ On Hold; → Expired is final. Edit only offers legal next statuses |
| LINQ search by date range & status | `Data/ContractQueryExtensions.cs` (`ApplyFilter`) used by *Contracts* page; date range = contract in force at any time in the range; reversed range is swapped |
| Currency API with HttpClient | `Services/Currency/ExchangeRateProvider.cs` – typed `HttpClient`, async/await (no `.Result`), try/catch for timeouts, network and bad JSON, 30-min cache |
| Auto-calculate and save ZAR | `ServiceRequestService.CreateAsync` – saves USD, rate used and ZAR cost; live preview on the form |
| Unit tests | `tests/TechMove.Tests` – currency maths, file validation (.exe, double extension, fake PDF, empty, size limits), workflow, LINQ filter, HTTP client (fake handler), service layer (Moq), upload/download (InMemory DB + temp folder) |
| GitHub Action | `.github/workflows/dotnet.yml` |

## Design patterns (from Part 1)

- **Repository** – `IRepository<T>` + `ClientRepository`, `ContractRepository`, `ServiceRequestRepository`. Controllers never see `DbContext`.
- **Service Layer** – `ClientService`, `ContractService`, `ServiceRequestService` hold the business rules.
- **State (workflow)** – `ContractWorkflow` owns every status transition and the request-blocking rule.
- **Strategy** – `IExchangeRateProvider`; swap ExchangeRate-API for OpenExchangeRates (or a fake in tests) without touching business code.
- **Factory** – `ServiceRequestFactory` builds valid new service requests in one place.
- **Dependency Injection** – everything is registered in `Program.cs` against interfaces.

## TOGAF alignment

| TOGAF ADM phase | How this prototype reflects it |
|---|---|
| Phase B – Business Architecture | Business entities (Client, Contract, Service Request) and business rules (status workflow, ZAR reporting) |
| Phase C – Data Architecture | Normalised schema, relationships, constraints in `AppDbContext` |
| Phase C – Application Architecture | Layered building blocks: Presentation (MVC) → Service Layer → Repository → Database |
| Phase D – Technology Architecture | ASP.NET Core 8, SQL Server, EF Core, external REST API, simulated file server |
| Phase E/F – Opportunities & Migration | Monolith first (MVP); interfaces at every boundary so services can be split out later |
| Phase G – Implementation Governance | Unit tests + GitHub Actions CI as quality gates |

## Notes

- The currency API used is ExchangeRate-API's free open endpoint (`https://open.er-api.com/v6/latest/USD`, no key). Change it in `appsettings.json`.
- If the API is down, the user sees a clear message and nothing is saved.
