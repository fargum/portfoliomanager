# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Local Development
```bash
# Run everything with Docker Compose (recommended)
docker-compose up -d
# API: http://localhost:8080 | UI: http://localhost:3000 | Swagger: http://localhost:8080/swagger
```

### API (.NET 10)
```bash
# Build
dotnet build

# Run API locally
cd services/api/src/FtoConsulting.PortfolioManager.Api && dotnet run

# Run all tests
dotnet test services/api/tests/FtoConsulting.PortfolioManager.Application.Tests/

# Run a single test class or method
dotnet test services/api/tests/FtoConsulting.PortfolioManager.Application.Tests/ --filter "FullyQualifiedName~ClassName"

# Add a new EF Core migration
cd services/api/src/FtoConsulting.PortfolioManager.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../FtoConsulting.PortfolioManager.Api

# Apply migrations
dotnet ef database update --startup-project ../FtoConsulting.PortfolioManager.Api
```

### UI (Next.js 14)
```bash
cd services/ui
npm run dev          # Dev server
npm run build        # Production build
npm run lint         # Lint with auto-fix
npm run type-check   # TypeScript check (tsc --noEmit)
```

### Deploy to Azure
```powershell
.\build-api.ps1   # Build and push API to Azure Container Apps
.\build-ui.ps1    # Build and push UI to Azure Container Apps
```

### AI Evaluation (Python)
```bash
cd evaluation/
pip install -r requirements.txt
python portfolio_evaluator.py
```

## Architecture

### Layer Structure
```
Api → Application → Domain ← Infrastructure
```
- **Domain** (`FtoConsulting.PortfolioManager.Domain`): Entities, aggregate roots, repository interfaces, domain events. No dependencies on other layers.
- **Application** (`FtoConsulting.PortfolioManager.Application`): CQRS commands/queries via MediatR, AI orchestration, portfolio services. Depends on Domain only.
- **Infrastructure** (`FtoConsulting.PortfolioManager.Infrastructure`): EF Core DbContext, repository implementations, external integrations (EOD API, Redis). Depends on Domain.
- **Api** (`FtoConsulting.PortfolioManager.Api`): Controllers, JWT auth (Azure AD B2C), OpenTelemetry, Swagger. Wires everything together.

### AI Agent Pipeline
User chat → `ChatController` → `AiOrchestrationService` → Azure OpenAI LLM → (if tool needed) → AI Tools (MCP protocol) → data sources → response back through LLM.

AI tools live in `Application/Services/Ai/Tools/`: `PortfolioHoldingsTool`, `PortfolioAnalysisTool`, `PortfolioComparisonTool`, `MarketIntelligenceTool`, `EodMarketDataTool`.

Input/output guardrails (`Application/Services/Ai/Guardrails/`) validate all AI interactions for safety and log security incidents.

### Conversation Memory
`ConversationThreadService` → `PostgreSqlChatMessageStore` → stores last 50 messages + AI-generated `MemorySummary` → `PortfolioMemoryContextProvider` injects context into each AI request. Memory is account-scoped and persisted in PostgreSQL.

### Authentication
Two schemes run in parallel:
- **Bearer (Azure AD B2C)**: User-facing endpoints, requires `Portfolio.ReadWrite` scope (`RequirePortfolioScope` policy).
- **SystemApiKey**: Internal/scheduler endpoints (`SystemApiAccess` policy).

### Database Conventions
PostgreSQL with EF Core. All table and column names use **snake_case** (enforced via EF configuration). Migrations are in `Infrastructure/Migrations/`. Migration history table: `app.__EFMigrationsHistory`.

### Frontend
Next.js 14 App Router. Key components: `PortfolioDashboard`, `HoldingsGrid` (AG Grid), `AiChat`. Auth via MSAL (`@azure/msal-react`). API calls go through `src/lib/api-client.ts`. Standalone output mode for Docker.

## Key Files
- `services/api/src/FtoConsulting.PortfolioManager.Application/Configuration/AgentPrompts.json` — AI system prompts
- `services/api/src/FtoConsulting.PortfolioManager.Api/appsettings.json` — app configuration structure
- `.env` / `.env.template` — environment variables for local Docker Compose
- `docker-compose.yml` — local orchestration; `docker-compose.prod.yml` — production variant
- `docs/` — detailed documentation for each subsystem

## Conventions
- Follow DDD principles: business logic belongs in Domain entities/aggregates, not controllers or services.
- snake_case for all database entities/columns.
- Be direct and honest — don't agree with something if it's wrong.
