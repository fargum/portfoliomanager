# Copilot Instructions for Portfolio Manager
## AI Assistant tips
- You are an expert AI assistant specialized in .NET development, Python scripting and Next.js.
- Please dont be obsequious to the user and agree to everything they say.
- Be brutally honest, don't be a yes man. saying "You're absolutely right!" to everything is not helpful. 
- If the user is wrong, point it out bluntly. 

## Project Overview
This is a Domain Driven Design (DDD) based portfolio management solution built with .NET 9 and Entity Framework Core. The solution provides a containerized REST API for managing portfolio holdings and financial data.

## Architecture
- **Domain Layer**: Core business logic and domain entities
- **Application Layer**: Application services, CQRS handlers, and DTOs  
- **Infrastructure Layer**: Data access, external services, and infrastructure concerns
- **API Layer**: REST API controllers and presentation layer

## Technology Stack
- .NET 9
- Entity Framework Core with PostgreSQL
- Docker containerization
- RESTful API with Swagger/OpenAPI
- Domain Driven Design patterns
- Snake_case database naming conventions

## Key Features
- Portfolio holdings management
- RESTful API endpoints
- Containerized deployment
- Health monitoring
- Structured logging
- Database migrations

## Development Guidelines
- Follow DDD principles and clean architecture
- Use snake_case for database entities (PostgreSQL compatibility)
- Implement proper error handling and logging
- Maintain comprehensive API documentation
- Use dependency injection for service management
- Write unit tests for business logic

## Container Architecture
- API Service: Containerized .NET 9 REST API
- Database: External PostgreSQL container
- UI: next.js with tailwindcss frontend container
- Python: for evaluation framework

## Important Files
- `Dockerfile`: Multi-stage build configuration
- `docker-compose.yml`: Container orchestration
- `docs/Docker-Deployment-Guide.md`: Comprehensive deployment documentation
- API endpoints available at `/swagger` when running