# TechMove – Contract & Service Management (EAPD7111 POE, Part 3)

Part 3 decouples the Part 2 monolith into a **Service-Oriented Architecture**:

| Project | Role | Talks to |
|---|---|---|
| `src/TechMove.Api` | ASP.NET Core **Web API** – the service layer. Owns the database, business rules, file storage and currency API. Swagger + JWT. | SQL Server, ExchangeRate-API |
| `src/TechMove.Web` | ASP.NET Core **MVC** frontend – presentation only. **No database packages.** | TechMove.Api over HTTP (`HttpClient`) |
| `tests/TechMove.Tests` | xUnit **unit tests** + **API integration tests** | TechMove.Api (in-process or the running container) |

```
 Browser ──► glms-frontend-web (MVC, :5000) ──HTTP + JWT──► glms-backend-api (Web API, :5001) ──► sql-server-db (:1433)
                                                                   │
                                                                   └──► open.er-api.com (USD→ZAR)
```

## Run everything with Docker (recommended)

Requires **Docker Desktop** (with WSL 2 on Windows).

```bash
docker compose up --build
```

| What | URL |
|---|---|
| Web app | http://localhost:5000 – sign in as **admin / Admin@123** (or staff / Staff@123) |
| API + Swagger | http://localhost:5001/swagger |
| SQL Server | `localhost,1433` – user `sa`, password `TechMove!Passw0rd` |

The API applies the EF Core migration (tables + seed data) automatically on start-up.
Stop with `docker compose down` (add `-v` to wipe the database and uploaded files).
To change the SA password or JWT key, copy `.env.example` to `.env` and edit it.

## Run from Visual Studio

1. Start only the database container: `docker compose up -d sql-server-db`
   (the API's `appsettings.json` points at `localhost,1433`).
2. Right-click the solution → **Configure Startup Projects** → **Multiple startup projects** →
   set **TechMove.Api** and **TechMove.Web** to *Start*.
3. Press **F5**. API: https://localhost:7181/swagger · Web: https://localhost:7180

## API endpoints

All endpoints except `/api/auth/login` and `/health` need `Authorization: Bearer <token>`.
In Swagger: call **POST /api/auth/login**, copy the token, click **Authorize**.

| Verb | Route | Purpose | Success |
|---|---|---|---|
| POST | `/api/auth/login` | Username/password → JWT | 200 (401 if wrong) |
| GET | `/api/contracts?status=&fromDate=&toDate=&clientName=` | Search/filter (LINQ) | 200 |
| GET | `/api/contracts/{id}` | One contract + client + service requests | 200 / 404 |
| GET | `/api/contracts/open` | Contracts that can take service requests | 200 |
| POST | `/api/contracts` (multipart, incl. PDF) | Create contract + signed agreement | **201** + Location / 400 |
| PUT | `/api/contracts/{id}` | Update client, dates, service level | 204 / 400 / 404 |
| PATCH | `/api/contracts/{id}/status` | Approve / decline (Admin only) – workflow enforced | 200 / 400 / 403 / 404 |
| POST | `/api/contracts/{id}/agreement` | Replace signed agreement PDF | 204 / 400 |
| GET | `/api/contracts/{id}/agreement` | Download PDF | 200 / 404 |
| GET/POST/PUT/DELETE | `/api/clients[/{id}]` | Client CRUD | 200 / 201 / 204 / 400 / 404 |
| GET | `/api/service-requests[/{id}]` | List / one | 200 / 404 |
| POST | `/api/service-requests` | Create – converts USD→ZAR with live rate | **201** / 400 / 404 / **503** |
| PATCH | `/api/service-requests/{id}/status` | Pending → In Progress → Completed / Cancelled | 200 / 400 |
| GET | `/api/exchange-rates/usd-zar` | Current rate | 200 / 503 |
| GET | `/api/dashboard` | Counts for the home page | 200 |
| GET | `/health` | Liveness check (used by CI) | 200 |

Errors are returned as RFC 7807 `application/problem+json` (see `Infrastructure/ApiExceptionHandler.cs`).

## Architecture & patterns (carried over from Parts 1–2)

**API (`TechMove.Api`)**
- **Repository** – `Data/Repositories` – the only code that uses `AppDbContext`.
- **Service Layer** – `Services/*Service.cs` – all business logic (moved unchanged from the Part 2 MVC app).
- **State (workflow)** – `Services/Workflow/ContractWorkflow.cs`.
- **Strategy** – `IExchangeRateProvider` (real ExchangeRate-API in production, a fake in tests).
- **Factory** – `ServiceRequestFactory`.
- **DTO + Mapper** – `Dtos/` – entities never leave the API; workflow answers (`canCreateServiceRequest`, `allowedNextStatuses`) are calculated here.
- **Dependency Injection** throughout (`Program.cs`); controllers are thin and contain no SQL.

**Frontend (`TechMove.Web`)**
- **Typed HttpClient service layer** – `Services/Api/ApiClients.cs` (`IClientsApi`, `IContractsApi`, …).
- **BearerTokenHandler** (DelegatingHandler) adds the JWT to every call.
- **Error handling** – `ApiClientBase` turns HTTP failures into `ApiValidationException` / `ApiNotFoundException` /
  `ApiUnauthorizedException` / `ApiUnavailableException`; `ApiExceptionFilter` shows a friendly page if the API is down
  and sends the user to sign in if the token expired.
- **Authentication** – cookie sign-in that stores the API's JWT; every page requires login; only Admins can approve/decline.

## Tests

```bash
dotnet test                                   # unit + in-process integration tests
TECHMOVE_API_URL=http://localhost:5001 dotnet test --filter Category=Integration   # against the running container
```

- `tests/TechMove.Tests/Unit` – currency maths, PDF validation (.exe, renamed .exe, empty, size), workflow, LINQ filter, HttpClient parsing, services (Moq).
- `tests/TechMove.Tests/Integration` – calls the real API endpoints and asserts status codes **and** data:
  create-then-read for contracts, clients and service requests, PDF byte-for-byte round trip,
  201/204/400/401/403/404/503 cases, Swagger document, currency API outage.

GitHub Actions (`.github/workflows/dotnet.yml`) runs two jobs on every push:
1. **build-and-test** – build + all tests.
2. **docker-compose-integration** – `docker compose up --build`, waits for `/health`, then runs the integration tests
   against the **running API container**.

## Database

- Migration: `src/TechMove.Api/Migrations` (applied automatically on API start-up).
- SQL script: `Database/TechMove_InitialCreate.sql`.
