# PortfolioManager

A Domain Driven Design (DDD) based portfolio management solution built with .NET 9 and Entity Framework Core.

## Architecture

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

## Next Steps

1. Define domain entities for portfolio management
2. Implement specific repository interfaces
3. Add application services and CQRS handlers
4. Configure PostgreSQL database provider
5. Set up database migrations
6. Implement API controllers
7. Add authentication and authorization
8. Set up logging and monitoring