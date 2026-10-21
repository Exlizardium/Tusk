# Tusk

A simple helpdesk / ticket tracker API. Customers file tickets in projects, agents work on them, admins manage projects and users.

Built with ASP.NET Core 10, EF Core, PostgreSQL and JWT authentication.

## Quick start (Docker)

You need [Docker Desktop](https://www.docker.com/products/docker-desktop/).

```sh
cp .env.example .env    # then fill in the values
docker compose up -d --build
```

The API is now running at `http://localhost:8080`. Database migrations are applied automatically on startup.

To stop it: `docker compose down` (add `-v` to also delete the database).

## Running locally with .NET

For day-to-day development you can run the API with `dotnet` and keep only the database in Docker.

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and a `.env` file (see above).

1. Start the database and apply migrations:

   ```sh
   docker compose up -d migrate
   ```

2. Set the API's secrets (once). Use the same password as in `.env`:

   ```sh
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=tuskdb;Username=postgres;Password=<your password>" --project Tusk.Api
   dotnet user-secrets set "Jwt:Key" "<random string, at least 32 characters>" --project Tusk.Api
   ```

3. Run the API:

   ```sh
   dotnet run --project Tusk.Api
   ```

The API runs at `http://localhost:5054`, with Swagger UI at `http://localhost:5054/swagger`.

## Configuration

`.env` (used by Docker Compose, never committed):

| Variable | Description |
|---|---|
| `POSTGRES_USER` | Database user |
| `POSTGRES_PASSWORD` | Database password |
| `JWT_KEY` | Secret used to sign login tokens, at least 32 characters |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | Optional. Creates the first admin on startup (see below) |

## Roles

| Role | Can |
|---|---|
| Customer | File tickets, see, comment on, close and reopen their own tickets |
| Agent | Work on any ticket: assign, change status and priority |
| Admin | Everything an agent can, plus manage projects and user roles |

New users always register as **Customer**. An admin can change roles via `POST /api/admin/assign-role`.

### Creating the first admin

Set `ADMIN_EMAIL` and `ADMIN_PASSWORD` in `.env` and (re)start with `docker compose up -d`. When running with `dotnet`, use user-secrets instead:

```sh
dotnet user-secrets set "Admin:Email" "admin@example.com" --project Tusk.Api
dotnet user-secrets set "Admin:Password" "<password>" --project Tusk.Api
```

On startup the API creates that account with the Admin role; it then logs in like any other user. Good to know:

- It only creates the account if the email isn't taken. An existing user with that email is **not** promoted (a warning is logged), so pick an email nobody has registered with.
- Changing the password later in config does nothing; the account already exists.
- If the password doesn't meet the rules, or only one of the two is set, the API refuses to start and says why.

## API overview

All endpoints except register and login need a `Authorization: Bearer <token>` header.

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login` |
| Projects | `/api/projects` |
| Tickets | `/api/projects/{projectId}/tickets` |
| My tickets | `GET /api/tickets/mine`, `GET /api/tickets/assigned` |
| Comments | `/api/tickets/{ticketId}/comments` |
| Admin | `GET /api/admin/users-with-roles`, `POST /api/admin/assign-role` |
| Health | `GET /health` (no token needed) |

See Swagger UI for the full list, request bodies and responses.

## Tests

```sh
dotnet test Tusk.slnx
```

Docker must be running: some tests start a temporary PostgreSQL container.
