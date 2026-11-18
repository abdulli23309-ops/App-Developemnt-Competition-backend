# ============================================================================
# Multi-stage Dockerfile for ASP.NET Core .NET 10 Web API
# ============================================================================

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first (layer caching).
COPY CompetitionApi.csproj .
RUN dotnet restore CompetitionApi.csproj

# Copy the rest of the source code.
COPY . .

# ---- Publish stage ----
FROM build AS publish
RUN dotnet publish CompetitionApi.csproj -c Release -o /app/publish

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# ASP.NET Core listens on the PORT environment variable (set by Railway).
# Default to 8080 if PORT is not set (local container testing).
ENV PORT=8080
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

# Copy the published output.
COPY --from=publish /app/publish .

# Run the application.
ENTRYPOINT ["dotnet", "CompetitionApi.dll"]
