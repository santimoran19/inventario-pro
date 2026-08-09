<div align="center">

# InventarioPro

**Inventory management REST API with full stock traceability, role-based access control and audit trail.**

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=flat-square&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF_Core-9.0-512BD4?style=flat-square)](https://learn.microsoft.com/ef/core/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=flat-square&logo=docker&logoColor=white)](https://docs.docker.com/compose/)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)

</div>

---

## What this is

A complete rewrite of a first-year university project — originally a WinForms desktop app on .NET Framework 4.8 backed by a Microsoft Access `.mdb` file, with no authentication, no tests and prices stored as integers.

This version keeps the same domain (a small shop managing its stock) and rebuilds it as a production-shaped REST API. The point of the rewrite was to solve the problems the original ignored: **who changed the stock, when, and why** — and what happens when two people change it at the same time.

---

## The core design decision

In the original app, `Stock` was a column anyone could overwrite. Here it is a **derived value**.

Every change goes through a `StockMovement` record — an immutable, append-only log of every entry, exit and adjustment. `Product.Stock` is a cached projection of that log, and the two are written inside a single database transaction. If the transaction fails, neither is written.

```
StockMovement (source of truth)          Product.Stock (projection)
├─ In        +100  → StockAfter: 100
├─ Out        -30  → StockAfter:  70   ────────►   70
└─ Adjustment  42  → StockAfter:  42   ────────►   42
```

Consequences that fall out of this:

- Stock can be **rebuilt from scratch** by replaying the movements
- Every unit is traceable to a user, a timestamp and a reason
- A physical count that disagrees with the system is recorded as an `Adjustment`, not silently overwritten
- Stock can never go negative — the domain method rejects it before the database is touched

---

## Concurrency

Two operators shipping from the same product at the same time is the classic way inventory systems corrupt themselves. Both read `Stock = 10`, both subtract 6, and the second write silently wins: the system says 4 when it should say -2.

This is handled with **optimistic concurrency** using PostgreSQL's `xmin` system column as a row version. If the row changed between read and write, EF Core raises `DbUpdateConcurrencyException` and the service retries up to three times with fresh data before giving up.

```csharp
e.Property(p => p.Version).IsRowVersion().HasColumnName("xmin");
```

---

## Security

| Concern | How it's handled |
|---|---|
| SQL injection | EF Core parameterizes everything; sorting uses a whitelist, never string concatenation |
| Password storage | ASP.NET Core Identity (PBKDF2), 10-char minimum with mixed character classes |
| Brute force | Account lockout after 5 failed attempts + rate limiting of 8 req/min on auth endpoints |
| User enumeration | Login returns an identical response whether the email exists or the password is wrong |
| Token theft | Access tokens live 15 minutes; refresh tokens are rotated on every use |
| Refresh token reuse | Stored as SHA-256 hashes. A replayed token revokes the user's entire session family |
| Mass assignment | Separate input DTOs — `Stock` and audit fields are not bindable from the request body |
| Secret leakage | No secrets in `appsettings.json`; the app refuses to start if the JWT key is missing or under 32 chars |
| Information disclosure | Unhandled exceptions return a generic `ProblemDetails`; stack traces are logged server-side only |
| CORS | Explicit origin allowlist, never `AllowAnyOrigin` combined with credentials |
| Container | Runs as a non-root user (UID 10001) |

---

## Stack

- **.NET 9** — Web API with controllers
- **PostgreSQL 16** via **EF Core 9** (Npgsql)
- **ASP.NET Core Identity** + JWT bearer with refresh token rotation
- **FluentValidation** for request validation
- **Serilog** for structured logging
- **xUnit** + **FluentAssertions** for tests
- **Docker Compose** for local development

---

## Running it

The fastest path — API and database, one command:

```bash
docker compose up --build
```

Then open **http://localhost:8080/swagger**. The database is migrated and seeded automatically in Development.

### Running locally without Docker

```bash
# 1. Start PostgreSQL only
docker compose up db -d

# 2. Configure secrets (never commit these)
cd src/InventarioPro.Api
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "Seed:AdminEmail" "admin@inventariopro.local"
dotnet user-secrets set "Seed:AdminPassword" "Admin#Local2026"

# 3. Apply migrations and run
dotnet ef database update
dotnet run
```

### Tests

```bash
dotnet test
```

---

## API

All endpoints require a bearer token except `/api/auth/*` and `/health`.

### Auth

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/auth/register` | Create an account (assigned `Viewer`) |
| `POST` | `/api/auth/login` | Returns access + refresh token |
| `POST` | `/api/auth/refresh` | Rotate tokens |
| `POST` | `/api/auth/logout` | Revoke all sessions |
| `GET` | `/api/auth/me` | Current user profile |

### Products

| Method | Endpoint | Role |
|---|---|---|
| `GET` | `/api/products` | any |
| `GET` | `/api/products/{id}` | any |
| `POST` | `/api/products` | Admin, Manager |
| `PUT` | `/api/products/{id}` | Admin, Manager |
| `DELETE` | `/api/products/{id}` | Admin |

Supports `?search=`, `?categoryId=`, `?lowStockOnly=true`, `?minPrice=`, `?sortBy=price&desc=true`, `?page=1&pageSize=20`.

### Stock

| Method | Endpoint | Role |
|---|---|---|
| `GET` | `/api/stock/movements` | any |
| `POST` | `/api/stock/movements` | Admin, Manager |

### Reports

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/reports/valuation` | Units, cost value, sale value, potential margin |
| `GET` | `/api/reports/valuation/by-category` | Same, broken down by category |
| `GET` | `/api/reports/low-stock` | Products at or below their reorder threshold |

### Example — register a stock exit

```bash
curl -X POST http://localhost:8080/api/stock/movements \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "productId": 1,
    "type": 2,
    "quantity": 5,
    "reason": "Counter sale",
    "reference": "TICKET-4471"
  }'
```

```json
{
  "id": 12,
  "productId": 1,
  "productSku": "BEB-001",
  "productName": "Agua mineral 500ml",
  "type": "Out",
  "quantity": 5,
  "stockAfter": 115,
  "reason": "Counter sale",
  "reference": "TICKET-4471",
  "createdAt": "2026-08-09T14:22:10.441Z"
}
```

---

## Roles

| Role | Permissions |
|---|---|
| `Viewer` | Read products, movements and reports |
| `Manager` | Everything above + create/edit products, register stock movements |
| `Admin` | Everything above + delete products and categories |

---

## Project structure

```
src/InventarioPro.Api/
├── Domain/
│   ├── Entities/       Product, Category, Supplier, StockMovement, RefreshToken
│   └── Enums/          MovementType
├── Data/
│   ├── AppDbContext    EF configuration, soft delete filters, audit interceptor
│   └── DbInitializer   Migrations, roles, seed data
├── Services/
│   ├── TokenService    JWT issuing, refresh rotation, reuse detection
│   ├── ProductService  Search, filtering, pagination
│   └── StockService    The only component allowed to mutate stock
├── Controllers/        Auth, Products, Categories, Suppliers, Stock, Reports
├── Dtos/               Input/output contracts
├── Validation/         FluentValidation rules
└── Common/             Paging, domain exceptions, global exception handler

tests/InventarioPro.Tests/
└── StockServiceTests   Stock invariants, adjustments, insufficient stock, history integrity
```

---

## What changed from the original

| | Before | Now |
|---|---|---|
| Platform | WinForms, .NET Framework 4.8 | REST API, .NET 9 |
| Database | Access `.mdb` via OleDb | PostgreSQL via EF Core |
| Data access | `DataSet` + `CommandBuilder` | Typed entities, LINQ, migrations |
| Money | `int` | `decimal(18,2)` |
| Stock changes | Direct column overwrite | Immutable movement log + transaction |
| Concurrency | None | Optimistic locking with retry |
| Auth | None | Identity + JWT + roles |
| Deletion | Physical `DELETE` | Soft delete, history preserved |
| Audit | None | Who/when on every entity |
| Tests | None | xUnit suite over stock invariants |
| Deployment | Manual `.exe` | Docker Compose |

---

## License

MIT — see [LICENSE](LICENSE).
