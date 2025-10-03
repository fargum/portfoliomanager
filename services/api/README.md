# Portfolio Manager API Service

This is the REST API service for the Portfolio Manager application, built with .NET 9 and Entity Framework Core.

## Quick Start

```bash
# Build the API Docker image
docker build -t portfoliomanager-api:latest .

# Run the API container
docker run -d --name portfoliomanager-api -p 8080:8080 portfoliomanager-api:latest
```

## Development

```bash
# Restore dependencies
dotnet restore

# Run locally
cd src/FtoConsulting.PortfolioManager.Api
dotnet run
```

## API Documentation

When running, the API documentation is available at:
- Swagger UI: http://localhost:8080/index.html (Development mode)
- Health Check: http://localhost:8080/health

## Key Endpoints

- `GET /api/holdings/account/{accountId}/date/{date}` - Retrieve holdings for an account
- `GET /api/portfolios/` - Portfolio management endpoints
- `GET /health` - Health check endpoint

## Configuration

The API uses PostgreSQL database with connection string configured via:
- Local development: User Secrets
- Docker container: Environment variables

See the main project documentation for database setup instructions.