# Customer Management API

A small .NET 10 API for creating, listing, reading, and updating customers. It stores them in SQL Server and applies EF Core migrations on startup when you run it with Docker.

The easiest way to try it is one command. Docker builds the API, starts SQL Server, waits until the database is ready, then starts the API.

## What you need

- Docker with Docker Compose
- A `.env` file in the project root (see below)

For running the API on your machine instead of in a container, you also need the .NET SDK 10.0.401 (or a newer 10.0.4xx patch). On this Mac it lives in `~/.dotnet`. New zsh terminals pick it up on their own. In an existing terminal:

```sh
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
```

## First-time setup

From the project root, create `.env` if you do not already have one:

```sh
cd /Users/mack/diskd/personal/customer-management
test -f .env || cp .env.example .env
```

Open `.env` and set `MSSQL_SA_PASSWORD` to a strong development password: 16 or more characters, with uppercase, lowercase, digits, and punctuation. Skip `;` and `$` — the password is placed into a connection string, and those characters get in the way. Git ignores `.env`, and Docker builds leave it out of the image.

`API_PORT` is the port on your machine. The example file uses `8080`. This Mac's `.env` uses `5081` because something else is already on `8080`. SQL Server is published only on `127.0.0.1:14330`.

## Run it with Docker

```sh
docker compose up --build
```

That builds the API image, starts SQL Server, and starts the API once the database health check passes. The API applies the committed migrations and listens on port 8080 inside the container, mapped to `API_PORT` on your machine.

Leave that terminal running. In another one, open:

| What | Address |
| --- | --- |
| Customers | `http://localhost:5081/api/customers` |
| OpenAPI JSON | `http://localhost:5081/openapi/v1.json` |
| Liveness | `http://localhost:5081/health` |

If your `.env` still has `API_PORT=8080`, use `8080` in those URLs. Compose runs in Development, so OpenAPI is on. There is no Swagger UI. `/health` only checks that the process is up; it does not check the database.

A quick smoke test:

```sh
curl http://localhost:5081/api/customers
```

An empty database answers with `[]`.

### Stop and start again

Stop the containers and keep the data:

```sh
docker compose down
```

The next `docker compose up --build` brings the same database back. The SQL password is stored in that volume. If you change `MSSQL_SA_PASSWORD` later, the existing database still expects the old one. Keep the original password, or reset the volume on purpose:

```sh
docker compose down --volumes
```

That deletes the customers stored for this assessment.

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

Point `API_URL` at whichever process you started. Docker on this Mac is `5081`. A local `dotnet run` is `5080`.

```sh
API_URL=http://localhost:5081
curl "$API_URL/api/customers"
curl -i -X POST "$API_URL/api/customers" \
  -H 'Content-Type: application/json' \
  -d '{"firstName":"John","lastName":"Kamau","email":"john.kamau@example.com","phoneNumber":"0712345678"}'
```

Use the id from the response for the rest:

```sh
CUSTOMER_ID=1
curl "$API_URL/api/customers/$CUSTOMER_ID"
curl -i -X PUT "$API_URL/api/customers/$CUSTOMER_ID" \
  -H 'Content-Type: application/json' \
  -d '{"firstName":"John","lastName":"Kamau","email":"john.kamau@example.com","phoneNumber":"+254722334455"}'
```

- POST returns `201 Created`, the customer, and a `Location` header.
- List returns `200` and an array ordered by id, or `[]`.
- Get by id returns `200`, or `404` when that customer is missing.
- PUT needs all four editable fields. It returns `200` or `404`. The id and creation time stay as they were.
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

Scope and acceptance checks are in `docs/implementation-plan.md`. Authentication, deletion, pagination, and search are outside this assessment.

## Apple Silicon

The API image uses your machine's architecture. SQL Server is pinned to `linux/amd64`, so Docker emulates it on Apple Silicon. Microsoft supports SQL Server containers on x86-64 Linux and does not support emulation; see [their container guidance](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment?view=sql-server-ver17).

On this Mac, SQL Server has exited with code 139 after becoming healthy. Starting it again was enough to finish the checks, and it is still the less stable option. An x64 Docker host is the reliable place to run the database. If emulation fails here, run the API locally and point it at a SQL Server you can reach. The provider stays SQL Server either way. Details are in `docs/verification.md`.

This Compose file is for local assessment. A deployed service should use its own credentials, trusted TLS, and a separate step for migrations.
