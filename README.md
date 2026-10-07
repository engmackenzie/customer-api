# Customer Management API

A small .NET 10 API for creating, listing, reading, and updating customers. It stores them in SQL Server and applies EF Core migrations on startup when you run it with Docker.

After you clone the repo and set a database password, one command starts everything. Docker builds the API, starts SQL Server, waits until the database is ready, then starts the API.

## What you need

- Git
- Docker with Docker Compose

To run the API on your machine instead of in a container, you also need the [.NET SDK 10.0.401](https://dotnet.microsoft.com/download/dotnet/10.0) or a newer 10.0.4xx patch.

## Clone and configure

```sh
git clone https://github.com/engmackenzie/customer-api.git
cd customer-api
cp .env.example .env
```

`.env` is not in the repository. Open the copy and replace `MSSQL_SA_PASSWORD` with a strong development password: 16 or more characters, with uppercase, lowercase, digits, and punctuation. Skip `;` and `$` — the password is placed into a connection string, and those characters get in the way. Git ignores `.env`, and Docker builds leave it out of the image.

`API_PORT` is the port on your machine. The example uses `8080`. Change it if that port is already taken. SQL Server is published only on `127.0.0.1:14330`.

## Run it with Docker

```sh
docker compose up --build
```

That builds the API image, starts SQL Server, and starts the API once the database health check passes. The API applies the committed migrations and listens on port 8080 inside the container, mapped to `API_PORT` on your machine.

Leave that terminal running. In another one, open:

| What | Address |
| --- | --- |
| Customers | `http://localhost:8080/api/customers` |
| OpenAPI JSON | `http://localhost:8080/openapi/v1.json` |
| Liveness | `http://localhost:8080/health` |

Those URLs follow `API_PORT` in `.env`. Compose runs in Development, so OpenAPI is on. There is no Swagger UI. `/health` only checks that the process is up; it does not check the database.

A quick smoke test:

```sh
curl http://localhost:8080/api/customers
```

An empty database answers with `data` as `[]` and pagination totals of zero.

### Stop and start again

Stop the containers and keep the data:

```sh
docker compose down
```

The next `docker compose up --build` brings the same database back. The SQL password is stored in that volume. If you change `MSSQL_SA_PASSWORD` later, the existing database still expects the old one. Keep the original password, or reset the volume on purpose:

```sh
docker compose down --volumes
```

That deletes the customer data in the local database.

## Run the API on your machine

Use this when you want `dotnet watch` or a debugger. SQL Server still runs in Docker.

```sh
docker compose up -d --wait sqlserver
set -a
source .env
set +a
export ConnectionStrings__Customers="Server=localhost,14330;Database=CustomerManagement;User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True"
dotnet restore
dotnet tool restore
dotnet ef database update --project src/CustomerManagement.Api
dotnet run --project src/CustomerManagement.Api
```

The local API listens at `http://localhost:5080`. For hot reload, use `dotnet watch --project src/CustomerManagement.Api`. You apply migrations yourself with `dotnet ef database update`; the Docker path does that on startup.

After you change the model:

```sh
dotnet ef migrations add DescribeTheChange --project src/CustomerManagement.Api
dotnet ef database update --project src/CustomerManagement.Api
```

## Try the API

Point `API_URL` at whichever process you started. The Docker default is `8080`. A local `dotnet run` is `5080`.

```sh
API_URL=http://localhost:8080
curl "$API_URL/api/customers"
curl -i -X POST "$API_URL/api/customers" \
  -H 'Content-Type: application/json' \
  -d '{"firstName":"John","lastName":"Kamau","email":"john.kamau@example.com","phoneNumber":"0712345678"}'
```

Use `data.id` from the create response for the rest:

```sh
CUSTOMER_ID=1
curl "$API_URL/api/customers/$CUSTOMER_ID"
curl -i -X PUT "$API_URL/api/customers/$CUSTOMER_ID" \
  -H 'Content-Type: application/json' \
  -d '{"firstName":"John","lastName":"Kamau","email":"john.kamau@example.com","phoneNumber":"+254722334455"}'
```

- POST returns `201 Created`, `{ "data": { ...customer } }`, and a `Location` header.
- List returns `200` with customers in `data`, ordered by id, plus `pagination` (`page`, `pageSize`, `totalCount`, `totalPages`). Defaults are `page=1` and `pageSize=10`. `page` below 1, or `pageSize` outside 1–100, returns `400`. A page past the end is `200` with `data: []`.
- Get by id returns `200` with `{ "data": { ...customer } }`, or `404` when that customer is missing.
- PUT needs all four editable fields. It returns `200` with `{ "data": { ...customer } }`, or `404`. The id and creation time stay as they were.
- Invalid input returns `400` with field errors.
- A duplicate email returns `409`, including when two requests race.
- Unexpected failures return `500` with a generic problem response. Details stay in the server logs.

Every field is required and trimmed. Names can be 100 characters, email 254, phone 32. Email must look like an email, and uniqueness ignores case. Phone numbers keep a leading `0` or `+`. Creation times come from SQL Server in UTC and include the `Z` suffix in responses.

`src/CustomerManagement.Api/CustomerManagement.Api.http` has the same requests, including the error cases. Set `baseUrl` and `customerId` there to match your run.

## Check a build

```sh
dotnet build --configuration Release
dotnet format --verify-no-changes
```

The list accepts `page` and `pageSize`, for example `GET /api/customers?page=1&pageSize=10`. Scope and acceptance checks are in `docs/implementation-plan.md`. Authentication, deletion, and search are outside this assessment.

## Apple Silicon

The API image uses the host architecture. SQL Server is pinned to `linux/amd64`, so Docker emulates it on Apple Silicon. Microsoft supports SQL Server containers on x86-64 Linux and does not support emulation; see [their container guidance](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment?view=sql-server-ver17). If the database container fails under emulation, run the API locally and point it at a SQL Server you can reach.

This Compose file is for local use. A deployed service should use its own credentials, trusted TLS, and a separate step for migrations.
