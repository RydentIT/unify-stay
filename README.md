# UnifyStay

Property rental platform. This repository holds the whole product: a .NET 9 backend, two
Next.js portals, the database schema, and the pipelines that build and test them.

> **Status: scaffolding.** The architecture, wiring, CI and test infrastructure are real and
> verified. The business logic is not written yet — the Register, Login and password-reset
> handlers are stubs that return `501 Not Implemented`. See
> [What is deliberately not built](#what-is-deliberately-not-built).

---

## Contents

- [Repository structure](#repository-structure)
- [Running locally](#running-locally)
- [Running the tests](#running-the-tests)
- [Database migrations](#database-migrations)
- [Configuration](#configuration)
- [Architecture decisions](#architecture-decisions)
- [What is deliberately not built](#what-is-deliberately-not-built)
- [Branch protection](#branch-protection)

---

## Repository structure

```text
unify-stay/
├── .docker/                     Dockerfiles (build context is the REPO ROOT, not this folder)
│   ├── Dockerfile                 API — sdk:9.0 to build, aspnet:9.0 to run
│   ├── user-portal.Dockerfile     Next.js standalone
│   └── admin-portal.Dockerfile    Next.js standalone
├── .github/workflows/           backend-ci, frontend-ci, e2e-ci
├── database/sql/                Versioned migrations, applied by DbUp in filename order
├── docs/adr/                    Architecture Decision Records (template + index)
├── scripts/                     migrate.sh / migrate.ps1
├── src/
│   ├── backend/
│   │   ├── Unify.Domain/               Entities, value objects, domain rules. No dependencies.
│   │   ├── Unify.Application/          Use cases + the interfaces Infrastructure implements
│   │   ├── Unify.Application.Tests/    xUnit + NSubstitute, no external dependencies
│   │   ├── Unify.Infrastructure/       Dapper, Npgsql, DbUp, BCrypt, JWT issuing
│   │   ├── Unify.Infrastructure.Tests/ xUnit + Testcontainers, real Postgres
│   │   ├── Unify.Api/                  ASP.NET Core minimal API, auth, rate limiting, middleware
│   │   └── Unify.Api.Tests/            xUnit + WebApplicationFactory, real HTTP pipeline
│   └── frontend/                     Turborepo workspace (npm workspaces)
│       ├── apps/user-portal/           Guest + host portal, port 3000 (tests/ colocated)
│       ├── apps/admin-portal/          Staff portal, port 3001 (tests/ colocated)
│       ├── packages/ui/                Minimal shared components (tests/ colocated)
│       ├── packages/api-client/        Typed client for the backend (tests/ colocated)
│       ├── packages/config/            Shared eslint / tsconfig / tailwind
│       └── tests/e2e/                  Playwright specs, run against the full stack
├── docker-compose.yml
└── UnifyStay.sln
```

### Why the backend is four projects

Project references point **inward only**, and the build enforces it:

```text
Unify.Api ──► Unify.Application ──► Unify.Domain
     └──────► Unify.Infrastructure ──┘
```

`Unify.Domain` has no project references and no NuGet packages at all. If it ever needs one,
that is the signal the concept belongs a layer out. `Unify.Application` declares an interface
for everything it needs from the outside world (`IUserRepository`, `IPasswordHasher`,
`ITokenService`, `IEmailSender`, `IAuditLogger`, `IDateTimeProvider`, …) under
`Abstractions/`, and `Unify.Infrastructure` provides the implementations. Nothing in
Application or Domain knows that persistence is a relational database or that the transport
is HTTP.

The `GET /health` endpoint is the proof that this is wired correctly rather than just
described: the request enters the API, is routed by the Application dispatcher, calls a
database probe implemented with Dapper in Infrastructure, and the component states are folded
by a rule in Domain. All four layers on one round trip.

---

## Running locally

### With Docker (everything)

```bash
cp .env.example .env      # edit if you like; the defaults work
docker compose up --build
```

| Service | URL |
| --- | --- |
| user-portal | <http://localhost:3000> |
| admin-portal | <http://localhost:3001> |
| API | <http://localhost:8000> |
| API readiness | <http://localhost:8000/health> |
| API OpenAPI (Development) | <http://localhost:8000/openapi/v1.json> |
| API Swagger UI (Development) | <http://localhost:8000/swagger> |
| Postgres | `localhost:5432` |

Postgres data lives in the named volume `postgres-data`. `docker compose down -v` throws it
away and gives you a clean database next time.

For machine-specific tweaks that should never reach a pull request:

```bash
cp docker-compose.override.yml.example docker-compose.override.yml
```

`docker-compose.override.yml` is gitignored and picked up automatically.

### Without Docker

```bash
# Backend (needs a Postgres somewhere)
export ConnectionStrings__Unify="Host=localhost;Database=unify;Username=unify;Password=..."
export Jwt__SigningKey="at-least-32-bytes-of-random-material"
export Jwt__Issuer="https://localhost:8001" Jwt__Audience="unify-stay"
dotnet run --project src/backend/Unify.Api

# Frontend
cd src/frontend
npm install
cp apps/user-portal/.env.local.example  apps/user-portal/.env.local
cp apps/admin-portal/.env.local.example apps/admin-portal/.env.local
npm run dev            # both apps, via turbo
```

**Requirements:** .NET 9 SDK (pinned by `global.json`), Node ≥ 22.13, Docker.

---

## Running the tests

### Backend

```bash
dotnet test UnifyStay.sln                                        # everything
dotnet test src/backend/Unify.Application.Tests                  # fast, no dependencies
dotnet test src/backend/Unify.Api.Tests                          # real HTTP pipeline, no database
dotnet test src/backend/Unify.Infrastructure.Tests                # real Postgres
```

`Unify.Infrastructure.Tests` needs an actual database and finds one in one of two ways:

1. `UNIFY_TEST_POSTGRES` — a connection string to an existing server. This is how CI does it,
   pointing at a GitHub Actions service container.
2. Otherwise it starts a throwaway **Testcontainers** Postgres, which needs a running Docker
   daemon.

With neither available the tests report as **skipped** rather than failing, so a developer
with Docker Desktop stopped still gets a green run. A skipped integration suite is not a
passing one — check the counts before trusting a local result.

### Frontend

```bash
cd src/frontend
npm run lint          # eslint, flat config
npm run typecheck     # tsc --noEmit across every workspace
npm run test          # vitest: both apps + packages/ui + packages/api-client
npm run build         # next build for both apps
```

Unit tests live in a `tests/` folder inside each app or package (e.g.
`packages/ui/tests/button.test.tsx`, `apps/user-portal/tests/app/login/page.test.tsx`),
mirroring the source layout rather than sitting beside the file under test.

### End-to-end

Playwright specs live in `src/frontend/tests/e2e/` — outside any single app or package,
because they exercise both portals against a real backend and database and belong to the
system rather than to either app. They do **not** start any servers; bring the stack up first.

```bash
docker compose up -d
cd src/frontend
npm run e2e:install                    # once, downloads the browser
npm run e2e                            # both portals
npm run e2e -- --project=user-portal   # one portal
```

---

## Database migrations

Migrations are plain SQL files in `database/sql/`, named `NNNN_description.sql` and applied
by **DbUp** in ordinal filename order. DbUp records what it has run in a `schemaversions`
table, so re-running is a no-op.

The scripts are embedded into `Unify.Infrastructure` at build time, so the API container
carries its own migrations and behaves identically on a laptop, in CI and in a deployment.

- **Development:** applied automatically at startup (`Database__RunMigrationsOnStartup=true`).
- **Everywhere else:** startup migration is off. Run migrations as a deploy step, *before*
  the new API version starts taking traffic:

  ```bash
  ConnectionStrings__Unify="Host=...;Database=...;Username=...;Password=..." ./scripts/migrate.sh
  # or, on Windows
  ./scripts/migrate.ps1
  ```

  Both wrap `dotnet run --project src/backend/Unify.Api -- migrate`, which applies pending
  scripts and exits without starting the web host.

### Two things to know when adding a migration

1. **Keep every script idempotent** (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT
   EXISTS`, `ADD COLUMN IF NOT EXISTS`). `docker-compose.yml` also mounts `database/sql/` into
   the Postgres container's `docker-entrypoint-initdb.d`, so on a fresh volume the scripts run
   once via initdb and then again via DbUp's journal. Idempotent scripts make that harmless.
   If you would rather DbUp be the only thing that ever touches the schema, delete that mount
   from `docker-compose.yml` — nothing else depends on it.
2. **Never edit a script that has shipped.** DbUp journals by filename, so an edited file is
   never re-applied. Add a new numbered script instead.

Current schema: `users`, `roles`, `user_roles`, `auth_providers`, `login_attempts`, `sessions`,
`password_reset_tokens`, `email_verification_tokens`, `pending_email_changes`,
`role_upgrade_requests`, `verification_documents`, `audit_logs`.

---

## Admin bootstrap

There is no seeded Admin account and no implicit "first user becomes Admin" behaviour. The
first Admin is created by an explicit CLI command:

```bash
ADMIN_BOOTSTRAP_EMAIL=admin@example.com \
ADMIN_BOOTSTRAP_PASSWORD=a-strong-password-here \
  dotnet run --project src/backend/Unify.Api -- bootstrap-admin
```

Both environment variables are read only for this one invocation — never by the running web
host — and are documented as placeholders in [`.env.example`](./.env.example). If you keep
them in your local `.env`, the command above picks them up automatically; nothing else does.

What it does:

- If an Admin already exists, it logs that and exits. **Safe to run more than once** — it
  never creates a second Admin.
- Otherwise it creates the account (unverified, exactly like a normal registration), hashes
  the password with the same `IPasswordHasher` every other account uses, grants the Admin
  role, and sends a verification email through the same flow `POST /api/auth/register` uses
  (`SendVerificationEmailCommand` — there is no separate bootstrap-only verification path).
- The account **cannot sign in until that email is verified** (`LOG-004` blocks any
  unverified account, Admin included) — click the link, then sign in normally at
  `/login` on the admin portal.

---

## Configuration

**No secret is committed, and no connection string is hardcoded.** `appsettings.json` contains
placeholder/empty values only; everything real arrives from the environment.

ASP.NET Core maps `__` to configuration nesting, so `Jwt__SigningKey` overrides
`Jwt:SigningKey`. Precedence, lowest to highest:

```text
appsettings.json  <  appsettings.{Environment}.json  <  user secrets (Development)  <  environment variables
```

`.env.example` documents every variable with a dummy value. Copy it to `.env` (gitignored) for
compose. Each frontend app has its own `.env.local.example`.

Options are validated on startup (`ValidateDataAnnotations().ValidateOnStart()`), so a missing
connection string or a signing key under 32 bytes fails the host immediately rather than on
the first request that needs it.

`NEXT_PUBLIC_*` values are inlined into the browser bundle **at build time** — that is why
compose passes `NEXT_PUBLIC_API_URL` as a build arg, and why nothing secret may ever use that
prefix.

---

## Architecture decisions

Summarised here; each deserves a full ADR in [`docs/adr/`](./docs/adr/) once the team
confirms it. Use [the template](./docs/adr/template.md).

### Clean Architecture with four projects

Dependencies point inward and the compiler enforces it. The cost is more indirection than a
single-project API would need; the benefit is that swapping the persistence technology, the
mail provider or the token issuer touches one layer.

### Dapper, not EF Core

Every query is hand-written SQL against a schema we own, with no change tracking and no
generated SQL to reverse-engineer when something is slow. The trade-off is real and accepted:
we write the mapping code ourselves (`Persistence/Rows/`) and there is no migration
scaffolding from model diffs — which is why DbUp exists below.

### DbUp for migrations

Plain, reviewable `.sql` files applied in order and journalled. The database schema is a first
class artefact rather than a projection of C# classes. Chosen over Evolve mostly for the
embedded-scripts model, which keeps the API container self-sufficient.

### A hand-rolled dispatcher instead of MediatR

MediatR moved to a paid commercial licence from v13. Rather than freeze on the last Apache-2.0
release (12.5.0) or take on a licence obligation for an MVP, `Unify.Application` carries a
~90-line dispatcher with the same shape: `ICommand<T>` / `IQuery<T>`, one handler per use
case, and `IPipelineBehavior<,>` for cross-cutting concerns. Handlers and validators are
discovered by assembly scan, so adding a use case needs no DI edit.

One deliberate detail: the handler is resolved *inside* the innermost pipeline delegate, so a
request rejected by validation never constructs its handler — and never opens the database
connections that handler's dependencies would.

### JWT with a limited-scope token for forced resets

Access tokens are HS256, issued with `JsonWebTokenHandler` (not the legacy
`JwtSecurityTokenHandler`). A user with `must_change_password` set receives a short-lived
token carrying `token_type=password_change` **and no role claims at all**, so even an endpoint
that forgot its policy has nothing for a role check to match. The default authorization policy
requires `token_type=full`, which means forgetting to name a policy fails closed.

### Built-in .NET rate limiting

`Microsoft.AspNetCore.RateLimiting` rather than AspNetCoreRateLimit — in-box, actively
maintained, with named per-endpoint policies for registration, login and password reset.

**Known limitation:** partitions are in-memory, i.e. per API instance. Once the API runs more
than one replica the effective limit becomes *limit × replicas*, and this needs a distributed
counter (typically Redis). Flagged in `RateLimitingExtensions` as well.

### Turborepo + npm workspaces

Two Next.js apps sharing three packages. npm workspaces because npm is already present
everywhere including CI, with no extra setup step; the price is slower installs and a larger
`node_modules` than pnpm. The shared packages ship TypeScript source and are compiled by each
app through `transpilePackages`, so they need no build step of their own.

### `packages/api-client` is hand-written but generation-shaped

Every backend call goes through this package — no `fetch` in a page component. The types
mirror the server contract exactly rather than being reshaped for convenience, so replacing
them with output generated from the API's OpenAPI document is a delete-and-regenerate rather
than a rewrite of call sites.

---

## What is deliberately not built

This is scaffolding. Being explicit so nothing here is mistaken for working software:

| Area | State |
| --- | --- |
| Register / Login / password reset **handlers** | Stubs returning `501`. Routes, validation, rate limiting and error mapping around them are real. |
| Email sending | `LoggingEmailSender` writes the message to the log. No provider chosen — that lands with the cloud target. |
| Frontend styling | Bare Tailwind, no design tokens. Final Figma designs pending; anything invented now would only be unpicked. |
| Token storage in the browser | `localStorage`, confined to `lib/session.ts`. The intended end state is an httpOnly `SameSite` cookie set by the backend. |
| Distributed rate limiting | In-memory only. See above. |
| Seed data | None. Schema only. |

The tests assert these stub behaviours on purpose (`AuthStubHandlerTests`, the `501`
assertions in `Unify.Api.Tests` and the Playwright specs). When a module lands, **update those
assertions rather than deleting the tests** — they are what prevents an endpoint from
appearing to work by accident.

---

## Branch protection

Configure in **Settings → Branches → Add rule** for `main`. Suggested settings:

- ☑ Require a pull request before merging (1 approval)
- ☑ Dismiss stale approvals when new commits are pushed
- ☑ Require status checks to pass before merging
- ☑ Require branches to be up to date before merging
- ☑ Require conversation resolution before merging
- ☑ Do not allow bypassing the above settings

### Required status checks

Add these by name once each workflow has run on a pull request at least once — GitHub only
offers checks it has already seen:

| Check | Workflow |
| --- | --- |
| `Build, format and fast tests` | backend-ci.yml |
| `Integration tests (Postgres)` | backend-ci.yml |
| `Docker image builds` | backend-ci.yml |
| `Lint, typecheck, test and build` | frontend-ci.yml |

**Do not** mark E2E CI as required. It runs only on push to `main` and on manual dispatch, so
it never reports on a pull request — a required check that never runs blocks every merge.

Because both CI workflows use `paths:` filters, a frontend-only pull request never triggers
the backend jobs. GitHub treats a required check that did not run as pending, which blocks the
merge. Two ways out — pick one before turning the rules on:

1. Drop the `paths:` filters and let both workflows run on every pull request (simplest;
   costs runner minutes).
2. Keep the filters and add a matching skipped-job workflow with the same job names, the
   standard workaround for required checks behind path filters.
