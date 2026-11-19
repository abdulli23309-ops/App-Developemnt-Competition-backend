# CampusConnect API (ASP.NET Core .NET 10)

Production-ready ASP.NET Core Web API foundation for CampusConnect.

## Architecture

- **Framework:** ASP.NET Core .NET 10 Web API
- **Primary Database:** PostgreSQL (`CompetitionDb`) via Entity Framework Core 10 (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Database Provider:** PostgreSQL ONLY across both local development and Railway production
- **Authentication:** JWT Bearer Authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **API Documentation:** OpenAPI (built-in .NET 10, Development only)
- **CORS:** Configurable environment-driven policy (`FrontendPolicy`)
- **Deployment:** Railway / Docker / Self-hosted

## Project Structure

```
CompetitionApi/
├── Controllers/
│   ├── BaseApiController.cs      # Reusable API controller with standardized ApiResponse
│   ├── HealthController.cs       # GET /api/health
│   ├── DatabaseController.cs     # GET /api/database/status
│   └── VersionController.cs      # GET /api/version
├── Data/
│   ├── Configurations/           # EF Core entity configuration folder
│   ├── Seed/                     # Database seed folder
│   └── ApplicationDbContext.cs   # Concrete EF Core DbContext with Npgsql
├── DTOs/
│   ├── Common/                   # ApiResponse, PagedResult
│   ├── Auth/                     # Auth request/response DTOs
│   ├── Complaints/               # Complaint DTOs
│   ├── Admin/                    # Admin management DTOs
│   └── Analytics/                # Analytics DTOs
├── Models/
│   ├── Entities/                 # Domain entities (User, Department, Complaint, etc.)
│   └── Enums/                    # CampusConnect enums (UserRole, ComplaintStatus, ComplaintPriority)
├── Services/
│   └── Interfaces/               # Domain service contracts
├── Middleware/
│   └── GlobalExceptionMiddleware.cs # Centralized error handling
├── Authorization/
│   └── Roles.cs                  # Student, DeptAdmin, SuperAdmin roles and policies
├── Extensions/
│   └── ServiceCollectionExtensions.cs # PostgreSQL, JWT & CORS setup
├── Validators/                   # Request validators
├── Properties/
│   └── launchSettings.json       # Local development profiles
├── Dockerfile                    # Multi-stage Dockerfile for Railway
├── .dockerignore                 # Excludes build output, git, and local binaries
├── appsettings.json              # Configuration (PostgreSQL, JWT)
├── appsettings.example.json      # Sanitized template
├── CompetitionApi.csproj         # Project file with NuGet references
└── Program.cs                    # Application entry point & pipeline
```

## Prerequisites

- .NET 10 SDK
- PostgreSQL (local install or Railway PostgreSQL)

## Local Development

### 1. Prerequisites

- PostgreSQL (16+ or 18) running locally on port 5432 (or configured port)
- Database: `CompetitionDb`

### 2. Configure Environment Variables

Local development reads `ConnectionStrings:DefaultConnection` from `appsettings.json` or `DATABASE_URL`.
Railway production sets `DATABASE_URL` (format: `postgresql://user:password@host:port/database`).

### 3. Run the Backend

```powershell
dotnet run --launch-profile http
```

The API will be available at:
- HTTP: `http://localhost:5116`
- Health: `http://localhost:5116/api/health`
- Database Status: `http://localhost:5116/api/database/status`
- Version: `http://localhost:5116/api/version`

### 4. Build

```powershell
dotnet build -c Release
```

## Environment Variables

| Variable | Description | Example / Notes |
|---|---|---|
| `DATABASE_URL` | PostgreSQL connection URL | `postgresql://postgres:password@localhost:5432/CompetitionDb` |
| `ConnectionStrings__DefaultConnection` | Alternative key/value format | `Host=localhost;Port=5432;Database=CompetitionDb;Username=postgres;Password=...` |
| `PORT` | Listening port for the application | Set automatically by Railway (e.g. `8080`) |
| `FRONTEND_ORIGIN` | Allowed CORS origin for frontend | `https://campusconnect-frontend.vercel.app` |
| `Jwt__Key` | Secret key for signing JWT tokens | Min 32 characters |
| `Jwt__Issuer` | JWT issuer | `CampusConnectApi` |
| `SEED_DEFAULT_PASSWORD` | Password for seeded demo accounts | Default: `Password123!` |
