# MiniPay – Modern Payment Switch Platform

MiniPay is a production-ready payment processing system built for developers who need reliable, scalable payment infrastructure.

## Key Features
- **Payment Processing** – Create, confirm, refund, and settle payments
- **Idempotency** – Prevent duplicate payments with built-in idempotency keys
- **Webhooks** – Reliable event notifications with automatic retries and exponential backoff
- **Redis Caching** – High-performance caching for frequent data lookups
- **PostgreSQL** – Robust relational database with EF Core and migrations
- **Keycloak Authentication** – Secure OAuth2/OIDC authentication with role-based access control
- **Settlement Engine** – Automated end-of-day settlement and reconciliation
- **Audit Logging** – Complete transaction history for compliance

## Tech Stack
- Backend: .NET 8 / C#
- Database: PostgreSQL (via EF Core)
- Cache: Redis
- Auth: Keycloak (OAuth2 / OIDC)
- Webhooks: HMAC-SHA256 signed payloads with automatic retries
- Deployment: Docker / Docker Compose