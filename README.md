# Competition API (ASP.NET Core .NET 10)

Production-ready ASP.NET Core Web API foundation for an application development competition.

## Architecture

- **Framework:** ASP.NET Core .NET 10 Web API
- **Database:** Microsoft SQL Server (Entity Framework Core 10)
- **ORM:** EF Core SQL Server provider
- **API Documentation:** OpenAPI (built-in .NET 10, Development only)
- **CORS:** Configurable policy (`FrontendPolicy`)
- **Deployment:** Railway (containerized via Docker)
- **Database Hosting:** Railway (self-hosted SQL Server container)

## Project Structure

```
CompetitionApi/
├── Controllers/
│   ├── HealthController.cs       # GET /api/health
│   ├── DatabaseController.cs     # GET /api/database/status
│   └── WeatherForecastController.cs (sample, preserve)
├── Data/
│   └── ApplicationDbContext.cs   # EF Core DbContext (empty, ready for entities)
├── DTOs/                         # Empty, ready for data transfer objects
├── Interfaces/                   # Empty, ready for service interfaces
├── Models/                       # Empty, ready for domain models
├── Services/                     # Empty, ready for business services
├── Properties/
│   └── launchSettings.json       # Local development profiles
├── Dockerfile                    # Multi-stage Dockerfile for Railway
├── appsettings.json              # Configuration (connection strings, logging)
├── appsettings.Development.json  # Development-specific overrides
├── CompetitionApi.csproj         # Project file with NuGet references
└── Program.cs                    # Application entry point & middleware
```

## Prerequisites

- .NET 10 SDK
- SQL Server (local development) or Railway (production)

## Local Development

### 1. Prerequisites

- SQL Server 2025 Express (or later) running locally
- Instance: `DESKTOP-UD649GB\SQLEXPRESS`
- Database: `CompetitionDb` (created manually, see below)
- Authentication: Windows Integrated Authentication

### 2. Create the Database (Local)

If `CompetitionDb` does not exist yet, create it using Windows Authentication:

```powershell
sqlcmd -S "DESKTOP-UD649GB\SQLEXPRESS" -E -Q "CREATE DATABASE CompetitionDb"
```

> Do not drop or delete an existing database.

### 3. Configure Environment Variables (Local)

The connection string is in `appsettings.json`. For local development with Windows Auth, no changes are needed. For production, use the `ConnectionStrings__DefaultConnection` environment variable.

### 4. Run the Backend

```powershell
dotnet run --launch-profile https
```

The API will be available at:
- HTTP: `http://localhost:5116`
- HTTPS: `https://localhost:7194`
- OpenAPI (Development): `https://localhost:7194/openapi/v1.json`

### 5. Build

```powershell
dotnet build -c Debug
```

## API Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| GET | `/api/health` | API health check | None |
| GET | `/api/database/status` | Database connectivity check | None |

## How Backend Connects to SQL Server

- `Data/ApplicationDbContext.cs` is an empty `DbContext` ready for domain entities.
- `Program.cs` registers `ApplicationDbContext` with SQL Server using `UseSqlServer`.
- The connection string is read from `Configuration.GetConnectionString("DefaultConnection")`.
- In production, set the `ConnectionStrings__DefaultConnection` environment variable to override `appsettings.json`.

## Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Development`, `Production` |
| `PORT` | Port to listen on (Railway) | `8080` |
| `FRONTEND_ORIGIN` | CORS origin for deployed frontend | `https://your-app.vercel.app` |
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | `Server=...;Database=CompetitionDb;...` |

## CORS Configuration

The `FrontendPolicy` CORS policy allows:

- `http://localhost:5175` (local frontend development)
- `https://localhost:5175` (local frontend development, HTTPS)
- The origin from `FRONTEND_ORIGIN` environment variable (production)

## Dockerfile (Railway Deployment)

A multi-stage `Dockerfile` is included:

1. **Build stage:** .NET SDK 10, restores and builds
2. **Publish stage:** Publishes the app in Release mode
3. **Runtime stage:** ASP.NET Core runtime 10, listens on `PORT` env variable

Build and run locally:

```powershell
docker build -t competition-api .
docker run -e PORT=8080 -e ConnectionStrings__DefaultConnection="..." -p 8080:8080 competition-api
```

## Vercel Frontend

The React + Vite frontend is located at:
`E:\appdevcompetition\frontend`

Deployed to Vercel. The frontend calls this API using `VITE_API_BASE_URL`.

## Railway Deployment

### API Service

1. Connect your GitHub repository to Railway.
2. Add a new service from the `backend/CompetitionApi` directory.
3. Set the following environment variables in Railway:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ConnectionStrings__DefaultConnection=<your SQL Server connection string>`
   - `FRONTEND_ORIGIN=https://your-frontend.vercel.app`
4. Railway will detect the `Dockerfile` and build/deploy automatically.
5. The service will be accessible at your Railway-provided URL.

### SQL Server Service (Self-Hosted Container)

Railway does not provide a first-party managed SQL Server. You must deploy SQL Server as a container:

1. Add a new service in Railway using the `mcr.microsoft.com/mssql/server:2025-latest` container image.
2. Set environment variables:
   - `ACCEPT_EULA=Y`
   - `SA_PASSWORD=<secure password>` — generate a strong password, do not commit it.
   - `MSSQL_PID=Express`
3. Add a persistent volume to the SQL Server service for data durability.
4. Note the internal Railway hostname and port (e.g., `sqlserver.railway.internal:1433`).
5. Construct the connection string:
   ```
   Server=<internal-hostname>,1433;Database=CompetitionDb;User=sa;Password=<SA_PASSWORD>;TrustServerCertificate=True;
   ```
6. Set `ConnectionStrings__DefaultConnection` on the API service to this value.

### Why Windows Authentication Cannot Be Used in Railway

- Local development uses Windows Integrated Authentication (`Trusted_Connection=True`), which relies on Windows AD/Kerberos.
- Railway containers run Linux, where Windows Authentication is not available.
- Production must use SQL Server Authentication (`User ID=sa`, `Password=...`) instead.

## Production Verification Checklist

- [ ] Dockerfile builds successfully
- [ ] API service deploys to Railway
- [ ] SQL Server container deploys to Railway with persistent volume
- [ ] `ConnectionStrings__DefaultConnection` is set on the API service
- [ ] `FRONTEND_ORIGIN` is set on the API service
- [ ] `GET /api/health` returns HTTP 200 on the deployed API
- [ ] `GET /api/database/status` returns HTTP 200 on the deployed API
- [ ] CORS preflight from Vercel frontend origin succeeds
- [ ] OpenAPI is NOT exposed in production (only in Development)

## Common Connection Errors and Solutions

| Error | Cause | Solution |
|-------|-------|----------|
| `503 Database is unavailable` | SQL Server not running or wrong connection string | Verify SQL Server service is running, check connection string |
| `Network error` on frontend | CORS blocking frontend origin | Add frontend URL to `FRONTEND_ORIGIN` env var |
| `Login failed for user 'sa'` | Wrong SQL Server password | Verify `SA_PASSWORD` matches in connection string |
| `Cannot open database "CompetitionDb"` | Database doesn't exist | Create the database in SQL Server |
| `A network-related or instance-specific error` | Wrong server name or port | Verify `Server=` value includes instance name and port |


