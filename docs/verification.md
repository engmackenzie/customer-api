# Verification results

Verified on 7 October 2026 against actual SQL Server, using both the native API
and the Dockerized API.

## Implemented

- Four customer endpoints with separate input/output DTOs and a database entity.
- Required-field, length, and email validation; trimmed input.
- Case-insensitive unique email index and database-conflict handling.
- Database-generated UTC creation timestamps, preserved on update.
- EF Core initial migration and repository-local EF tooling.
- Multi-stage, non-root API container; SQL Server health check and persistent volume.
- Environment-based settings, ignored local credentials, HTTP request examples, and README.

## Checks passed

- Release build: zero warnings and errors.
- Formatting verification and Compose configuration validation.
- Applying the initial migration to a clean SQL Server database.
- Reapplying migrations without changes and checking for pending model changes.
- 66 HTTP acceptance assertions against the native API.
- The same 66 assertions against the API container, whose startup created its database from migrations.
- Required, null, empty, whitespace-only, invalid-email, and overlong input rejection on POST and PUT.
- Valid create/read/list/update, stable ordering, correct status codes, and a correct creation Location header.
- UTC timestamps round-trip through SQL Server; PUT preserves ID and CreatedAt.
- Case and surrounding-space duplicate email handling; an update can retain its own email.
- Twelve simultaneous creates for one email produce one 201 and eleven 409 responses.
- A controlled uncommitted SQL insert forces an API request past its pre-check; the eventual unique-index violation is returned as 409.
- A stopped database produces generic 500 Problem Details without internal exception or connection details.
- Liveness remains independent of database availability.
- Recreating both containers preserves the customer data and timestamps in the named volume.
- Temporary verification records and the separate native verification database were removed. The customer list is empty.

The acceptance checks used a temporary HTTP verification script. Representative
requests are provided in `src/CustomerManagement.Api/CustomerManagement.Api.http`; the full
acceptance checklist is in `docs/implementation-plan.md`. No additional test
framework or optional pagination/search feature was added.

## Environment and limitation

- .NET SDK 10.0.401; ASP.NET Core and EF Core 10.0.12.
- Native Apple Silicon API and an ARM64 API container.
- SQL Server image: `mcr.microsoft.com/mssql/server:2022-latest`.
- Tested SQL Server image digest: `sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090`.
- Docker API address configured for this Mac: `http://localhost:5081`.
- Local native API address: `http://localhost:5080`.

SQL Server runs as an emulated x64 container here. It exited twice with code 139
after initially becoming healthy, with Docker reporting `OOMKilled=false`.
The cause was not conclusively diagnosed. Restarting allowed all functional checks
to pass, but that is not evidence of long-term stability.

Microsoft does not support SQL Server in emulation environments; see its
[container support guidance](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment?view=sql-server-ver17).
Reliable database operation still needs verification on an x64 Docker host or
against a supported SQL Server instance. No alternative database was substituted,
and no other running projects were restarted or reconfigured.
