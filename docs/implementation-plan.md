# Customer Management API: implementation plan

Status: Approved, implemented, and functionally verified on 7 October 2026. SQL Server stability under Apple Silicon emulation remains unresolved; see [verification results](verification.md).

Project: `/Users/mack/diskd/personal/customer-management`

Source: [NET Engineer Assessment.pdf](/Users/mack/Downloads/NET%20Engineer%20Assessment.pdf), pages 1-8.

## 1. Goal and scope

Build the small, maintainable Customer Management API described in the assessment using .NET 10, ASP.NET Core, Entity Framework Core, SQL Server, and Docker.

The PDF defines the assessment requirements. Its example layout and commands are guidance, not instructions to execute during this planning step. This plan was approved before implementation; the README now contains the current run instructions.

Required deliverables:

- Create, list, retrieve, and update customers through the four specified endpoints.
- Validate input and enforce unique customer emails in SQL Server.
- Keep request/response models separate from database entities.
- Create the database schema through committed EF Core migrations.
- Run the API and SQL Server with `docker compose up --build` after documented environment setup.
- Document prerequisites, local setup, migrations, Docker startup, and API usage.

Keep pagination, search, and additional automated tests secondary to the core deliverables. Retain the starter's existing health endpoint. Do not add authentication, deletion, a frontend, or unrelated customer fields.

## 2. Recommended design

### Keep one application project

Extend `CustomerManagement.Api` without introducing separate infrastructure and domain assemblies. The assessment's multi-project layout is an example, and it explicitly allows a simpler structure.

Use one `CustomersController`, one EF Core `AppDbContext`, one customer entity, and small DTOs. Inject the context directly into the controller. For four straightforward database operations, a repository, service interface, mediator, mapping library, or generic response wrapper would add little value.

Use `[ApiController]` and Data Annotations for built-in request validation and HTTP 400 responses. Reuse the existing Problem Details and exception-handler setup for unexpected failures. This follows [ASP.NET Core's controller conventions](https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0).

### Proposed layout

```text
customer-management/
  .config/dotnet-tools.json
  docs/implementation-plan.md
  src/CustomerManagement.Api/
    Controllers/CustomersController.cs
    DTOs/CustomerRequest.cs
    DTOs/CustomerResponse.cs
    Entities/Customer.cs
    Data/AppDbContext.cs
    Migrations/
    Properties/launchSettings.json
    Program.cs
    appsettings.json
    CustomerManagement.Api.csproj
    CustomerManagement.Api.http
  .dockerignore
  .env.example
  .gitignore
  Dockerfile
  docker-compose.yml
  CustomerManagement.slnx
  global.json
  README.md
```

POST and PUT can share `CustomerRequest` because they accept the same four fields. Keep response mapping explicit and small. Extract additional files only when the implementation actually needs them.

### Database and model decisions

- Use EF Core 10 with the SQL Server provider and matching 10.x tooling. Pin `dotnet-ef` in a repository-local tool manifest.
- Use SQL Server 2022 Developer for the assessment. The assessment does not require a particular SQL Server version; record the tested container tag in the README.
- `Id`: SQL Server `int`, identity primary key.
- `FirstName` and `LastName`: required strings, maximum 100 characters each.
- `Email`: required string, valid email format, maximum 254 characters, unique index.
- `PhoneNumber`: required string, maximum 32 characters. Preserve leading zeros and international prefixes; no country-specific phone regex.
- `CreatedAt`: `DateTime`, stored as `datetime2`, generated in UTC on creation and never modified by PUT. Ensure returned timestamps carry UTC semantics after reading from the database.
- Align DTO length validation with database limits. Reject null, empty, and whitespace-only required values. Trim surrounding whitespace before storage.
- Treat email uniqueness as case-insensitive, using an explicit case-insensitive email column collation so behavior does not depend on a machine's default database collation. Preserve the submitted email's casing after trimming.

These length limits and email comparison rules are our proposed defaults, not additional requirements from the PDF.

## 3. API behavior

Use the assessment's JSON field names and return customer objects directly.

- `POST /api/customers`: validate, check email availability, save, then return `201 Created`, the customer response, and a `Location` header pointing to the new customer.
- `GET /api/customers`: return `200 OK` with an array, ordered by `Id` for predictable results. An empty database returns `[]`.
- `GET /api/customers/{id}`: return `200 OK` with the customer or `404 Not Found`.
- `PUT /api/customers/{id}`: require all four editable fields, update the existing customer, and return `200 OK` with the updated customer. Return `404` when it does not exist. Preserve `Id` and `CreatedAt`; this is a full update, not PATCH or upsert.

Error handling:

- Invalid input: `400 Bad Request` with field-level validation details.
- Duplicate email: `409 Conflict` with a clear Problem Details message. Updating a customer with its own email must succeed.
- Missing customer: `404 Not Found` with a meaningful Problem Details response.
- Unexpected failure: `500 Internal Server Error`, with details logged on the server and no internal database information in the response.

An email availability query improves the response, but the unique index is authoritative. Catch SQL Server unique-constraint errors from `SaveChangesAsync` and translate them to `409`, including concurrent requests. Do not turn unrelated database failures into duplicate-email responses.

Use async EF Core calls and request cancellation tokens. Use no-tracking queries and DTO projections for reads; track the entity being updated.

## 4. Docker and local development

Keep the deployment small: an API container and a SQL Server container, with a named volume for database persistence.

- Add a multi-stage Dockerfile using .NET 10 SDK and ASP.NET runtime images, with the final application running as the image's non-root application user.
- Expose the container API at `http://localhost:8080`. Keep the current local `dotnet watch` address at `http://localhost:5080`.
- Supply the SQL password and connection string through environment/configuration. Commit `.env.example` with placeholders, ignore `.env`, and document copying it and setting a valid development password.
- Use the Compose SQL service name inside the API connection string; use `localhost` when running the API directly on the Mac. Bind any development SQL port to loopback.
- Add a SQL Server health check and wait for it before starting the API.
- Commit an `InitialCreate` migration. Do not use `EnsureCreated`.
- For this single-instance assessment setup, enable automatic application of migrations through an explicit configuration flag in Compose. Keep it disabled by default for local runs, which use `dotnet ef database update`. A failed migration should fail startup clearly.
- Keep the built-in OpenAPI document at `/openapi/v1.json` available in the documented development configuration, including Compose. Swagger UI is optional in the assessment; do not add a UI dependency initially.
- Preserve `/health` as the existing application liveness check; do not describe it as a database-readiness check.

### Check Apple Silicon compatibility first

This Mac and its running Docker engine are ARM64. Microsoft supports SQL Server Linux containers on x86-64 hosts and does not support emulation environments; see [SQL Server container support](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment?view=sql-server-ver17).

At the start of implementation, test the SQL Server image with `platform: linux/amd64` under Docker Desktop. Document that this is an emulated local development setup if it works. Do not assume setting the platform guarantees it will run.

If it fails, use a reachable SQL Server on a supported x64 host for development and validate the full Compose stack on an x64 Docker host. Confirm that host with the user before provisioning anything. Keep SQL Server as required; do not silently substitute SQLite or another database.

## 5. Plan of attack

The assessment allows 90 minutes. The estimates below are a focused implementation budget, not a promise that first-time downloads or an ARM compatibility issue fit within it.

### Step 1: Prove the environment (5 minutes)

Confirm the existing .NET 10 starter still builds. Test SQL Server container startup and a database connection on this Mac before investing in API code. Confirm that no existing local port or volume would be reused accidentally.

Exit condition: a usable SQL Server is available, or the x64 fallback is identified before continuing database-dependent work.

### Step 2: Add persistence (15 minutes)

Add the EF Core provider, design package, and local tooling. Implement the entity and context configuration, configure the connection string, generate `InitialCreate`, and apply it to a clean development database.

Exit condition: SQL Server has the expected primary key, required columns, lengths, timestamp behavior, and unique email index.

### Step 3: Implement the customer API (25 minutes)

Add request/response DTOs, controller registration, and the four endpoints. Implement validation, response mapping, missing-customer handling, and duplicate-email handling, including the database constraint path. Extend the `.http` examples with successful and failing requests.

Exit condition: create, list, fetch, and update work against SQL Server with the agreed status codes.

### Step 4: Package the working application (20 minutes)

Add the Dockerfile, Compose file, `.dockerignore`, environment example, SQL health check, and migration startup configuration. Verify `docker compose up --build` initializes a new assessment database and exposes the API.

Exit condition: the documented Compose startup works without a manual schema-creation step, and recreating containers preserves customer data in the named volume.

### Step 5: Verify the important behavior (15 minutes)

Run `dotnet build` and `dotnet format --verify-no-changes`, then run the acceptance checks below against actual SQL Server and the containerized API. Check the concurrent duplicate-email case explicitly; an in-memory EF provider would not prove SQL Server uniqueness behavior.

Exit condition: the core acceptance checks pass, with any environment limitation recorded honestly.

### Step 6: Finish the handoff (10 minutes)

Update the README with exact restore, tool restore, migration, local run, Docker run, API documentation, and verification commands. Explain configuration and the Apple Silicon caveat. Include normal shutdown instructions and clearly label any optional volume deletion as destructive. Review the final diff for unnecessary abstractions and secrets.

Only after the core is complete, consider one small automated regression test for a demonstrated edge case. Pagination and search remain deferred unless requested.

## 6. Acceptance checklist

- [x] A valid POST returns `201`, a `Location` header, and the persisted customer with generated `Id` and UTC `CreatedAt`.
- [x] List returns `[]` initially and the expected customer after creation.
- [x] Fetch and update return `404` for an unknown customer.
- [x] Missing, null, empty, whitespace-only, overlong, and invalid-email inputs return `400` and do not write data.
- [x] Duplicate email on POST returns `409`, including case variants and surrounding whitespace after normalization.
- [x] Keeping the same email on PUT succeeds; using another customer's email returns `409`.
- [x] Two concurrent creates for the same email yield one saved customer, one `201`, and one `409`.
- [x] PUT updates all editable fields and preserves `Id` and `CreatedAt`.
- [x] Phone numbers retain their leading zero or `+` prefix.
- [x] A clean database is created from committed migrations, and applying migrations again is safe.
- [x] Compose builds and starts the API and SQL Server; customer data survives container recreation with the volume retained.
- [x] OpenAPI and the existing health endpoint work at the documented addresses.
- [x] Build and formatting checks pass; the README is sufficient for another developer to run the solution.

## 7. Implementation outcome

Implemented decisions: one API project, controllers with built-in validation, direct EF Core access, case-insensitive email uniqueness, SQL Server 2022, and two-container Compose with explicitly enabled startup migrations for the assessment.

The API, migrations, and Docker workflow passed their functional checks. SQL Server nevertheless exited twice under ARM64 emulation after becoming healthy. The remaining infrastructure decision is an x64 host or supported SQL Server instance for reliable operation; see verification.md for the evidence and limits.

Next action: select a supported SQL Server environment if reliable long-running database operation is needed on this setup.
