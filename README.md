# MiniPay Payment Platform

MiniPay is a merchant-focused payment processing API built with ASP.NET Core. It demonstrates the main components normally required by a payment platform:

- Merchant administration
- Payment creation and confirmation
- Full and partial refunds
- Transaction history
- Settlement calculation
- Signed webhook delivery with retries
- Keycloak OAuth 2.0/OpenID Connect authentication
- JWT role and merchant isolation
- Idempotent payment creation
- Redis and local caching
- Rate limiting
- PostgreSQL persistence with Entity Framework Core
- Docker-based development
- Automated integration testing

> MiniPay is currently a development and learning implementation. It requires security review, load testing, failure testing, monitoring, and operational hardening before processing real money.

## System overview

```text
Merchant application
        |
        | 1. Request access token
        v
     Keycloak
        |
        | 2. JWT access token
        v
   MiniPay API
        |
        +----> PostgreSQL (source of truth)
        |
        +----> Redis (distributed cache)
        |
        +----> Merchant webhook endpoint
```

The merchant authenticates with Keycloak using the OAuth client-credentials flow. Keycloak issues a JWT containing the merchant role, API audience, and `merchant_id`. MiniPay validates the JWT before allowing access to merchant resources.

## Node.js mental model

For Node.js/NestJS developers:

| MiniPay component | Node.js/NestJS equivalent |
|---|---|
| `Program.cs` | `main.ts` plus the root module |
| Controllers | NestJS controllers or Express handlers |
| Services | NestJS injectable providers |
| Interfaces | TypeScript service contracts |
| DTOs | NestJS DTOs with `class-validator` |
| Entity Framework Core | Prisma or TypeORM |
| `AppDbContext` | Prisma client/database context |
| ASP.NET dependency injection | NestJS provider container |
| Middleware | Express/NestJS middleware |
| `ILogger<T>` | Pino or Winston |
| `Task<T>` | `Promise<T>` |
| xUnit | Jest |
| `WebApplicationFactory` | Supertest application fixture |
| HybridCache and Redis | `cache-manager` plus Redis |

## Technology stack

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL 17
- Redis 7
- Keycloak 26.7
- OpenAPI/Swagger
- Docker and Docker Compose
- xUnit and Testcontainers

## Main domain objects

### Merchant

Represents a business integrating with MiniPay. A merchant has an ID, name, email, status, webhook URL, payments, and settlements.

### Payment

Represents a request by a merchant to collect money. It contains a merchant reference, amount, currency, description, status, and timestamps.

### Transaction

Represents a financial movement associated with a payment. Transaction types include payment and refund.

### Refund

Represents a partial or full return of a completed payment. Successful refunds cannot exceed the original completed payment amount.

### Settlement

Groups eligible payment and refund transactions for a merchant and period.

```text
GrossAmount  = completed payment transactions
RefundAmount = completed refund transactions
FeeAmount    = GrossAmount x FeePercentage
NetAmount    = GrossAmount - RefundAmount - FeeAmount
```

### Idempotency record

Maps an `Idempotency-Key` to a request hash and created resource. This prevents retries from creating duplicate payments.

### Webhook event

Stores an outbound notification, delivery status, retry count, target URL, payload, and the latest response or error.

## Payment lifecycle

```text
CREATED
   |
   | Confirm
   v
PENDING
   |
   v
PROCESSING
   |
   +----------------+
   |                |
   v                v
COMPLETED          FAILED
```

A completed payment may be partially or fully refunded. Completed payment and refund transactions may later be included in a settlement.

## Project structure

```text
MiniPay/
├── Authentication/
├── Caching/
├── Controllers/
├── Data/
│   ├── AppDbContext.cs
│   ├── AppDbContextFactory.cs
│   └── Migrations/
├── DTOs/
├── Entities/
├── Enums/
├── Exceptions/
├── Helpers/
├── Interfaces/
├── Middleware/
├── Options/
├── RateLimiting/
├── Services/
├── keycloak/realm/minipay-realm.json
├── Tools/webhook-receiver.mjs
├── appsettings.json
├── docker-compose.yml
├── Dockerfile
├── MiniApy.Api.csproj
└── Program.cs
```

## Authentication and authorization

Keycloak issues access tokens for MiniPay clients. The API validates:

- Signature
- Issuer
- Audience (`minipay-api`)
- Expiration
- Realm roles
- Merchant claim

Roles include:

| Role | Purpose |
|---|---|
| `minipay-merchant` | Merchant payment and refund operations |
| `minipay-admin` | Merchant configuration and reporting |
| `minipay-settlement` | Settlement generation and retrieval |

A merchant token resembles:

```json
{
  "iss": "http://localhost:8081/realms/minipay",
  "aud": "minipay-api",
  "client_id": "merchant-demo",
  "merchant_id": "0293d7cb-87e2-4e19-b799-a6c34b834f2a",
  "realm_access": {
    "roles": ["minipay-merchant"]
  }
}
```

Merchant-facing endpoints should obtain `merchant_id` from the JWT instead of trusting a merchant ID supplied by the caller.

## Local startup

### Prerequisites

- Docker Desktop
- .NET 10 SDK
- EF Core command-line tools
- Node.js for the optional webhook receiver

Verify:

```powershell
docker --version
dotnet --version
dotnet ef --version
```

### Configure environment

```powershell
Copy-Item .env.example .env
```

Review all credentials in `.env`. Never commit `.env`.

### Start the platform

```powershell
docker compose up --build -d
docker compose ps
docker compose logs -f api
```

| Service | Address |
|---|---|
| MiniPay API | `http://localhost:8080` |
| Swagger | `http://localhost:8080/swagger` |
| Keycloak | `http://localhost:8081` |
| PostgreSQL | `localhost:5433` |
| Redis | `localhost:6379` |

## Database migrations

The API container connects to `payment_db:5432`. Host-side EF commands connect to `127.0.0.1:5433`.

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5433;Database=minipay;Username=minipay;Password=minipay_dev_password"

dotnet ef database update

Remove-Item Env:ConnectionStrings__DefaultConnection
```

Create a migration:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5433;Database=minipay;Username=minipay;Password=minipay_dev_password"

dotnet ef migrations add MigrationName --output-dir Data\Migrations
dotnet ef database update

Remove-Item Env:ConnectionStrings__DefaultConnection
```

## Obtain a merchant access token

```http
POST http://localhost:8081/realms/minipay/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded
```

Form body:

```text
grant_type=client_credentials
client_id=merchant-demo
client_secret=merchant-demo-secret-change-me
```

Use the returned token as:

```http
Authorization: Bearer <access-token>
```

## API operations

### Merchants

```http
POST /api/merchants
GET  /api/merchants/{id}
GET  /api/merchants/list
```

Merchant administration requires the `minipay-admin` role.

### Payments

```http
POST /api/payments
GET  /api/payments
GET  /api/payments/{id}
POST /api/payments/{id}/confirm
```

Payment creation requires an idempotency key:

```http
POST /api/payments
Authorization: Bearer <merchant-token>
Idempotency-Key: order-10001
Content-Type: application/json
```

```json
{
  "reference": "ORDER-10001",
  "amount": 100.00,
  "currency": "USD",
  "description": "Customer order 10001"
}
```

Repeating the same key and payload returns the same payment. Reusing the key with a different payload returns `409 Conflict`.

### Payment confirmation

```http
POST /api/payments/{paymentId}/confirm
```

```json
{
  "simulateFailure": false,
  "failureReason": null
}
```

This development endpoint simulates payment-provider processing. It is not a real bank, card-network, or mobile-money integration.

### Refunds

```http
POST /api/payments/{paymentId}/refund
GET  /api/refunds/{refundId}
```

```json
{
  "amount": 25.00,
  "reason": "Customer returned one item"
}
```

### Transactions

```http
GET /api/transactions
```

Transaction reporting is intended for administrators.

### Settlements

```http
POST /api/settlements/generate
GET  /api/settlements/{id}
GET  /api/settlements
```

```json
{
  "merchantId": "0293d7cb-87e2-4e19-b799-a6c34b834f2a",
  "currency": "USD",
  "periodStart": "2026-08-01T00:00:00Z",
  "periodEnd": "2026-08-31T23:59:59Z"
}
```

Only completed, eligible, previously unsettled transactions are included.

## Caching

MiniPay uses HybridCache:

```text
Local memory -> Redis -> PostgreSQL
```

Payment-list cache entries are scoped by merchant, short-lived, and invalidated after relevant payment writes. PostgreSQL remains the source of truth for payments, refundable balances, transactions, and settlements.

## Rate limiting

MiniPay applies separate limits to merchant reads, merchant writes, settlements, and webhooks.

Exceeded limits return:

```http
429 Too Many Requests
Retry-After: <seconds>
```

The built-in limiter is local to an API instance. A multi-replica production deployment should also apply distributed rate limiting at an API gateway or ingress.

## Webhooks

Payment completion or failure creates an event such as:

```text
payment.completed
payment.failed
```

The webhook worker signs payloads using HMAC-SHA256, records delivery results, and retries failures.

Start the development receiver:

```powershell
node Tools/webhook-receiver.mjs
```

## Error responses

| Status | Meaning |
|---:|---|
| `400` | Validation or business-rule failure |
| `401` | Missing, invalid, or expired JWT |
| `403` | Valid JWT without the required role |
| `404` | Resource not found |
| `409` | Duplicate resource, invalid state, or idempotency conflict |
| `429` | Rate limit exceeded |
| `500` | Unexpected server error |

## Automated tests

Integration tests use `WebApplicationFactory`, real PostgreSQL and Redis Testcontainers, a test authentication scheme, and EF migrations.

Docker Desktop must be running:

```powershell
dotnet test
```

Detailed output:

```powershell
dotnet test --logger "console;verbosity=detailed"
```

Important scenarios include:

- Unauthenticated requests return `401`
- Authenticated merchant reads succeed
- Identical idempotent requests return the same payment
- Reusing an idempotency key with another payload returns `409`
- Payment creation invalidates cached payment lists
- Refund totals cannot exceed the completed amount
- Transactions cannot be settled twice

## Configuration workflow

After changing only `.env`:

```powershell
docker compose up -d --force-recreate --no-deps api
```

After changing C# code, dependencies, or the Dockerfile:

```powershell
docker compose up --build --force-recreate -d api
```

## Production checklist

Before processing real payments:

- Require authentication on every non-public endpoint.
- Validate merchant ownership on every payment and refund lookup.
- Remove merchant IDs from merchant-facing request bodies when they come from JWT claims.
- Use HTTPS everywhere.
- Replace every development password and client secret.
- Use a production Keycloak database.
- Store secrets in a managed secrets service.
- Encrypt and persist ASP.NET Data Protection keys.
- Add idempotency to refunds and settlements.
- Verify webhook signatures and prevent replay attacks.
- Put the API behind a WAF or API gateway.
- Use distributed rate limiting for multiple replicas.
- Add security, load, concurrency, and failure testing.
- Add tracing, metrics, alerts, backups, and disaster recovery.
- Never log access tokens, passwords, client secrets, or signing secrets.

## License

No license has currently been assigned. Add an appropriate license before publishing or distributing MiniPay.
