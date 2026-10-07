# Build With AI: Customer API

A .NET 10 Web API using EF Core migrations and SQL Server. One controller handles
the four customer operations, with separate request/response DTOs and entities.

## Prerequisites

- .NET SDK 10.0.401 (or a newer 10.0.4xx patch) for local development.
- Docker with Docker Compose.
- Database image: `mcr.microsoft.com/mssql/server:2022-latest` (Developer edition).

```sh
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
```

## 1. Configure development

```sh
cd /Users/mack/diskd/personal/build-with-ai
cp .env.example .env
```

Keep `.env` if it already exists. Set `MSSQL_SA_PASSWORD` to a strong development
password with uppercase, lowercase, digits, and punctuation. Avoid `;` and `$`
because it is interpolated into a connection string. `.env` is ignored by Git
and excluded from Docker builds.

`API_PORT` defaults to `8080`. This Mac's prepared `.env` uses `5081` because
another application occupies `8080`. SQL Server uses loopback port `14330`.
Changing `.env` does not change the SQL password in an existing database volume;
retain the original value.

## 2. Run with Docker

```sh
docker compose up --build
```

Compose waits for SQL Server to become healthy, then starts the API and applies
committed migrations. `Database__ApplyMigrations=true` explicitly enables this
single-instance assessment behavior; it is off by default outside Compose.

Using the default port:

- Customers: `http://localhost:8080/api/customers`
- OpenAPI JSON: `http://localhost:8080/openapi/v1.json`
- Liveness: `http://localhost:8080/health`

Use `5081` with this Mac's `.env`. Compose runs in Development, where OpenAPI is
enabled. No Swagger UI dependency is needed. `/health` tests application liveness,
not database readiness.

Stop the stack while retaining its database:

```sh
docker compose down
```

The named volume preserves data across container recreation. Adding `--volumes`
deletes assessment data; use it only for an intentional reset.

## 3. Run locally

From the project root, start SQL Server, load the local settings, restore tools
and dependencies, and apply migrations:

```sh
docker compose up -d --wait sqlserver
set -a
source .env
set +a
export ConnectionStrings__Customers="Server=localhost,14330;Database=BuildWithAi;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True"
dotnet restore
dotnet tool restore
dotnet ef database update --project src/BuildWithAi.Api
dotnet run --project src/BuildWithAi.Api
```

The local API runs at `http://localhost:5080`. For hot reload, use
`dotnet watch --project src/BuildWithAi.Api`. Local runs apply migrations explicitly.
`TrustServerCertificate=True` is for the local SQL Server development certificate.

After a future model change:

```sh
dotnet ef migrations add DescribeTheChange --project src/BuildWithAi.Api
dotnet ef database update --project src/BuildWithAi.Api
```

## 4. Use the API

Set the address for your run mode:

```sh
API_URL=http://localhost:5080
curl "$API_URL/api/customers"
curl -i -X POST "$API_URL/api/customers" \
  -H 'Content-Type: application/json' \
  -d '{"firstName":"John","lastName":"Kamau","email":"john.kamau@example.com","phoneNumber":"0712345678"}'
```

Use the returned customer ID in subsequent requests:

```sh
CUSTOMER_ID=1
curl "$API_URL/api/customers/$CUSTOMER_ID"
curl -i -X PUT "$API_URL/api/customers/$CUSTOMER_ID" \
  -H 'Content-Type: application/json' \
  -d '{"firstName":"John","lastName":"Kamau","email":"john.kamau@example.com","phoneNumber":"+254722334455"}'
```

- POST: `201 Created`, the customer, and a `Location` header.
- List: `200 OK`, an array ordered by ID, or `[]`.
- Get by ID: `200 OK`, or `404` when absent.
- PUT: all four editable fields are required; returns `200 OK` or `404`. ID and creation time are preserved.
- Invalid input: `400` with field-level errors.
- Duplicate email: `409` with Problem Details, including concurrent writes.
- Unexpected failures: `500` with generic Problem Details; diagnostics stay in server logs.

All input fields are required and trimmed. Names allow 100 characters, email 254,
and phone 32. Email format is validated and uniqueness is case-insensitive,
enforced by a SQL Server index. Phone strings preserve leading zeros and `+`.
SQL Server generates creation times in UTC; responses include the UTC suffix.

`src/BuildWithAi.Api/BuildWithAi.Api.http` includes success and error examples.
Set its `baseUrl` and `customerId` to match your environment.

## 5. Verify changes

```sh
dotnet build --configuration Release
dotnet format --verify-no-changes
```

See `docs/implementation-plan.md` for scope and acceptance checks. Authentication,
deletion, pagination, and search are intentionally outside this assessment.

## Apple Silicon

SQL Server uses `platform: linux/amd64`; the API uses the host's native architecture.
Microsoft supports SQL Server containers on x86-64 Linux hosts and does not support
emulation environments; see [Microsoft's guidance](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment?view=sql-server-ver17).
If emulation fails, use a supported x64 Docker host or connect the local API to
a reachable SQL Server. The database provider remains SQL Server.

Compose is for local assessment use. A deployed service should use appropriate
credentials, trusted TLS, and a separate migration deployment step.
