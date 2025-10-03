# PortfolioManager

A Domain Driven Design (DDD) based portfolio management solution built with .NET 9 and Entity Framework Core. The solution provides a containerized REST API for managing portfolio holdings and financial data.

## Quick Start with Docker

```bash
# Build and run all services with Docker Compose
docker-compose up -d

# Or build individual services
docker build -t portfoliomanager-api:latest ./services/api
docker build -t portfoliomanager-ui:latest ./services/ui
```

**API Access:**
- Health Check: http://localhost:8080/health
- Swagger UI: http://localhost:8080/swagger
- Holdings API: http://localhost:8080/api/holdings/

📖 **See [Docker Deployment Guide](docs/Docker-Deployment-Guide.md) for comprehensive setup instructions**

## Architecture

This solution follows Domain Driven Design (DDD) principles with a microservices architecture:

### Monorepo Structure

```
PortfolioManager/
├── services/
│   ├── api/              # .NET 9 REST API Service
│   │   ├── src/          # Domain, Application, Infrastructure, API layers
│   │   ├── tests/        # Unit and integration tests
│   │   └── Dockerfile    # API containerization
│   └── ui/               # Frontend UI Service
│       ├── src/          # UI application code
│       └── Dockerfile    # UI containerization
├── docker-compose.yml    # Multi-service orchestration
└── docs/                 # Shared documentation
```

This solution follows Domain Driven Design (DDD) principles with a clean architecture approach:

### Projects Structure

- **FtoConsulting.PortfolioManager.Domain** - Core business logic and domain entities
- **FtoConsulting.PortfolioManager.Application** - Application services, CQRS handlers, and DTOs
- **FtoConsulting.PortfolioManager.Infrastructure** - Data access, external services, and infrastructure concerns
- **FtoConsulting.PortfolioManager.Api** - REST API controllers and presentation layer

### Domain Layer (`FtoConsulting.PortfolioManager.Domain`)

Contains the core business logic and domain entities:

```
├── Entities/           # Domain entities and base classes
├── ValueObjects/       # Value objects for domain modeling
├── Aggregates/         # Aggregate roots and domain aggregates
├── DomainEvents/       # Domain events and event interfaces
├── Repositories/       # Repository interfaces
├── Services/           # Domain services
└── Specifications/     # Domain specifications
```

### Application Layer (`FtoConsulting.PortfolioManager.Application`)

Orchestrates business logic and handles cross-cutting concerns:

```
├── Commands/           # CQRS command handlers
├── Queries/            # CQRS query handlers
├── DTOs/              # Data transfer objects
├── Services/          # Application services
├── Interfaces/        # Application service interfaces
└── Validators/        # Input validation logic
```

### Infrastructure Layer (`FtoConsulting.PortfolioManager.Infrastructure`)

Implements data access and external service integrations:

```
├── Data/
│   ├── Configurations/ # Entity Framework configurations
│   └── Migrations/     # Database migrations
├── Repositories/       # Repository implementations
├── Services/          # Infrastructure service implementations
└── ExternalServices/  # Third-party service integrations
```

### API Layer (`FtoConsulting.PortfolioManager.Api`)

Provides REST API endpoints and handles HTTP concerns:

```
├── Controllers/        # API controllers
├── Middleware/        # Custom middleware
└── Extensions/        # Service registration extensions
```

## Technologies Used

- **.NET 9** - Application framework
- **Entity Framework Core 9.0** - Object-relational mapping (ORM)
- **MediatR** - CQRS and mediator pattern implementation
- **ASP.NET Core** - Web API framework

## Getting Started

### Prerequisites

- .NET 9 SDK
- PostgreSQL (for production - migrations will be set up later)

### Building the Solution

```bash
dotnet build
```

### Running the API

```bash
dotnet run --project src/FtoConsulting.PortfolioManager.Api
```

The API will be available at `https://localhost:7000` (HTTPS) and `http://localhost:5000` (HTTP).

## Development Notes

- The solution uses a code-first approach with Entity Framework Core
- Database migrations will be configured for PostgreSQL in future iterations
- The Domain layer is technology-agnostic and contains no external dependencies
- Repository pattern is implemented with Unit of Work for transaction management
- CQRS pattern is implemented using MediatR for command and query separation

## Project Structure

```
PortfolioManager/
├── src/
│   ├── FtoConsulting.PortfolioManager.Domain/
│   ├── FtoConsulting.PortfolioManager.Application/
│   ├── FtoConsulting.PortfolioManager.Infrastructure/
│   └── FtoConsulting.PortfolioManager.Api/
├── PortfolioManager.sln
└── README.md
```

## Database Setup

This project uses PostgreSQL with Entity Framework Core and snake_case naming conventions.

### Prerequisites

1. **For Docker Deployment (Recommended):**
   - Docker and Docker Compose installed
   - Existing PostgreSQL database container

2. **For Local Development:**
   ```bash
   docker run --name portfolio-postgres -e POSTGRES_DB=portfolio_manager -e POSTGRES_USER=migrator -e POSTGRES_PASSWORD=your_password -p 5432:5432 -d postgres:15
   ```

### Configuration

#### For Development (User Secrets - Recommended):
```bash
cd src/FtoConsulting.PortfolioManager.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=portfolio_manager;Username=your_username;Password=your_password"
```

#### For Production (Environment Variables):
Set the environment variable:
```bash
PORTFOLIO_DB_CONNECTION="Host=localhost;Port=5432;Database=portfolio_manager;Username=your_username;Password=your_password"
```

### Database Migration

**For Docker deployment:** Migrations run automatically when the container starts.

**For local development:**
```bash
cd src/FtoConsulting.PortfolioManager.Infrastructure
dotnet ef database update --startup-project ../FtoConsulting.PortfolioManager.Api
```

## EOD Historical Data API Configuration

The application uses EOD Historical Data API for fetching market prices. You need to configure your API token:

### Setup Steps

1. Get your API token from [EOD Historical Data](https://eodhd.com/)
2. Copy `.env.example` to `.env`
3. Update the `EOD_API_TOKEN` value in your `.env` file

```bash
# In .env file
EOD_API_TOKEN=your_actual_api_token_here
```

### Configuration Options

The EOD API can be configured in `appsettings.json` or via environment variables:

```json
{
  "EodApi": {
    "Token": "",
    "BaseUrl": "https://eodhd.com/api",
    "TimeoutSeconds": 30
  }
}
```

**For Docker:** The token is passed via the `EOD_API_TOKEN` environment variable.
**For local development:** Set the token in user secrets or environment variables.

### Database Schema

The database uses snake_case naming conventions for PostgreSQL compatibility:
- Tables: `accounts`, `portfolios`, `instruments`, `holdings`, etc.
- Columns: `user_name`, `created_at`, `instrument_type_id`, etc.

## Docker Deployment

The application is fully containerized and ready for microservices architecture:

### 🐳 **Container Architecture**
- **API Service**: Containerized .NET 9 REST API
- **Database**: External PostgreSQL container (your existing setup)
- **Future UI**: Planned containerized frontend service

### 🚀 **Quick Docker Commands**
```bash
# Build and run
docker build -t portfoliomanager-api:latest .
docker-compose up -d

# View logs
docker-compose logs -f portfoliomanager-api

# Health check
curl http://localhost:8080/health
```

### 📚 **Complete Docker Guide**
See [Docker Deployment Guide](docs/Docker-Deployment-Guide.md) for:
- Container networking configuration
- Database connection setup
- Production deployment
- Troubleshooting
- Integration with UI services

## Security Notes

- **Never commit database credentials to source control**
- Use User Secrets for development
- Use environment variables or Azure Key Vault for production
- The connection string in `appsettings.json` contains placeholder values only