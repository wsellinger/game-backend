# Didakt API
###### Didaktos - That Which Can Be Taught
A multi-service game backend implementing player authentication and leaderboards. Built with microservice architecture in .NET 10 with minimal APIs and orchestrated by Docker Compose.

.NET 10 · PostgreSQL · Redis · Docker · Azure · xUnit

## Services

### Auth Service
##### `Didakt.Api.Auth`
Handles user registration, login, and session management using signed JWTs with refresh token rotation for cross-service authentication.

#### Stack
.NET 10, PostgreSQL, EF Core, Npgsql, JWT Bearer, FluentValidation, Scalar UI

#### Endpoints
| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/auth/register` | — | Register a new player |
| POST | `/auth/login` | — | Authenticate and receive a JWT + refresh token |
| POST | `/auth/renew` | — | Exchange a refresh token for a new JWT |
| POST | `/auth/logout` | Required | Revoke the current refresh token |

### Leaderboard Service
##### `Didakt.Api.Leaderboard`
Stores and ranks player scores by category using Redis.

#### Stack
.NET 10, Redis, JWT Bearer, Scalar UI

#### Endpoints
| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/leaderboard/{category}/score` | Required | Submit a score |
| GET | `/leaderboard/{category}/score?player=` | — | Retrieve a player's score |
| GET | `/leaderboard/{category}/top?count=` | — | Retrieve top N ranked players |

---

## Live Deployment

Both services are deployed to Azure Container Apps (Central US):

- Auth API: `https://didakt-auth.happyground-350d73a8.centralus.azurecontainerapps.io`
- Leaderboard API: `https://didakt-leaderboard.happyground-350d73a8.centralus.azurecontainerapps.io`

Backed by Azure Database for PostgreSQL (Flexible Server) and Azure Managed Redis. Secrets are centralized in Azure Key Vault, accessed by each service via system-assigned managed identity. Deployments are automated via GitHub Actions on push to `main` using OIDC federated credentials (no stored secrets).

---

## Local Setup

**Prerequisites:** Docker Desktop, .NET 10 SDK

```bash
# Clone
git clone https://github.com/wsellinger/didakt-api.git
cd didakt-api

# Create shared secrets file
cp .env.shared.example .env.shared   # then fill in Jwt__Secret, Jwt__Issuer, Jwt__Audience, Jwt__ExpiryMinutes

# Start all services
docker compose up --build
```

Service URLs:

- Auth API: `http://localhost:5224`
- Leaderboard API: `http://localhost:5223`

Each service also has an `.http` file for manual endpoint testing from within Visual Studio.

## Run Tests

```bash
dotnet test
```

Test projects mirror service structure using one test file per method. Tests use xUnit, Moq, Testcontainers, and WebApplicationFactory for integration tests.

---

## Design Notes

### Endpoints
Endpoints are implemented in Minimal API style to reduce boilerplate and improve performance. Encapsulation is maintained via dependency injection and separate files for service logic, endpoint handlers, validators, and models.

### Authentication
Cross-service authentication uses JWTs signed with a shared secret. Short-lived access tokens (15 min) are paired with long-lived refresh tokens (7 days) stored in PostgreSQL with SHA-256 hashing. On renew, the old token is revoked and a new one issued (rotation), so reused refresh tokens are rejected.

### Relational Database
User identity and session data are handled by PostgreSQL via EF Core with snake_case naming conventions. Schema is managed through EF Core migrations.

### Non-Relational Database
Leaderboard score data is stored in Redis sorted sets, which natively support ranked retrieval by score without a separate sort step.