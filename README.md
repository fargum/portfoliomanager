# Portfolio Manager

AI-powered portfolio management platform with intelligent market analysis, automated revaluation, and conversational insights.
I built this to create and manage personal and model portfolios across multiple accounts.

I also wanted to extend my work into full agentic workflows with tools like Microsoft Agent Framework which was released October / November 2025 after speaking to several senior engineers from Microsoft I met at the 2025 Azure dev conference I attended in Lisbon.

This also gave me an opportunity to really kick the tires of Claude sonnet 4.5 on Github copilot far beyond my usage of it at work.
Agent assisted coding is a gamechanger in software development and needs to be embraced. But its not a magic wand and requires careful steering. I have established an iterative, collaborative approach with the tool, holding a mantra I picked up from one of the devs at Microsoft : "Never trust a thing your llm produces. Once you've understood that, you're well on the way to working with them effectively"....AI without direction, care and correction becomes slop...I need to keep reminding myself every day of that.

Finally, I followed a DDD approach with this repo, which closely represents the systems I work with every day in finance. It's almost certainly overkill for this use case, but I wanted to see how fast and how well Claude could set it all up for me. 

More detailed documentation can be found in the docs folder. I probably need a bit of cleanup of it - particularly the memory section which i went back and forth with many times. I think its a fascinating and very powerful feature of these systems.

## Quick Start

```bash
# Local development
docker-compose up -d

# Deploy to Azure
.\build-api.ps1    # API deployment
.\build-ui.ps1     # UI deployment
```

**Access:**
- **API**: http://localhost:8080 | [Azure Production](https://pm-api.thankfulbay-b96a666e.uksouth.azurecontainerapps.io)
- **UI**: http://localhost:3000 | [Azure Production](https://pm-ui.thankfulbay-b96a666e.uksouth.azurecontainerapps.io)
- **Swagger**: /swagger

## Architecture

Microservices platform with DDD principles:

```
services/
├── api/                # .NET 9 REST API
│   ├── Domain         # Core business logic
│   ├── Application    # CQRS, AI agents, market intelligence
│   ├── Infrastructure # EF Core, PostgreSQL, EOD integration
│   └── Api            # Controllers, auth, telemetry
├── ui/                # Next.js frontend with Azure AD auth
└── evaluation/        # Python-based AI evaluation framework
```

### Key Layers

- **Domain**: Entities, value objects, aggregates
- **Application**: CQRS handlers, AI services, tools registry
- **Infrastructure**: Database, external APIs, caching
- **API**: REST endpoints, JWT auth, OpenTelemetry

## Core Features

### Portfolio Management
- Real-time holdings tracking with automated revaluation
- Multi-currency support with exchange rate integration
- Historical performance analysis and comparison
- Portfolio ingestion from various data sources

### AI Intelligence
- **Conversational Interface**: Natural language portfolio queries
- **Market Context**: Real-time news and sentiment analysis via EOD API
- **Agent Tools**: GetMarketContext, AnalyzePortfolio, ComparePerformance, GetMarketSentiment
- **Memory System**: Persistent conversation context with Redis
- **Evaluation Framework**: Python-based testing with quality metrics

### Integration & Security
- **Azure AD B2C**: OAuth 2.0 authentication with JWT tokens
- **Azure OpenAI**: GPT-4 powered chat with function calling
- **OpenTelemetry**: Distributed tracing and monitoring
- **EOD Historical Data**: Real-time pricing and sentiment data

## Tech Stack

**Backend**: .NET 9, EF Core, PostgreSQL, MediatR (CQRS)  
**Frontend**: Next.js 15, React, TailwindCSS, Azure AD auth  
**AI**: Azure OpenAI (GPT-4), Microsoft Agent Framework  
**Infrastructure**: Docker, Azure Container Apps, Azure Cache for Redis  
**Observability**: OpenTelemetry, Azure Monitor, Application Insights

## Configuration

### Required Secrets

```bash
# Database
ConnectionStrings__DefaultConnection="Host=...;Database=portfolio_manager;..."

# Azure AD B2C
AzureAdB2C__Instance="https://login.microsoftonline.com/"
AzureAdB2C__ClientId="<client-id>"
AzureAdB2C__TenantId="<tenant-id>"

# Azure OpenAI
AzureOpenAI__Endpoint="https://<resource>.openai.azure.com/"
AzureOpenAI__Key="<api-key>"
AzureOpenAI__DeploymentName="gpt-4"

# EOD Historical Data
EodApi__Token="<eod-token>"

# Redis (for memory)
Redis__ConnectionString="<redis-connection>"
```

Use **Azure Key Vault** for production, **User Secrets** for local development.

## Development

### Local Setup

```bash
# Clone and build
git clone https://github.com/fargum/portfoliomanager.git
cd PortfolioManager
dotnet build

# Run with Docker Compose
docker-compose up -d

# Or run API locally
cd services/api/src/FtoConsulting.PortfolioManager.Api
dotnet run
```

### Database Migrations

Migrations run automatically in Docker. For local development:

```bash
cd services/api/src/FtoConsulting.PortfolioManager.Infrastructure
dotnet ef database update --startup-project ../FtoConsulting.PortfolioManager.Api
```

Database uses **snake_case** conventions for PostgreSQL compatibility.

## Deployment

### Azure Container Apps

```powershell
# Deploy API
.\build-api.ps1

# Deploy UI
.\build-ui.ps1
```

Scripts handle ACR build and container app updates with proper revision management.

## Documentation

**API & Platform**
- [API Documentation](docs/API-Documentation.md) - REST endpoints
- [Holdings API](docs/Holdings-API-Documentation.md) - Portfolio management
- [Docker Deployment](docs/Docker-Deployment-Guide.md) - Container orchestration
- [UI Build & Deployment](docs/UI-Build-Deployment.md) - Frontend deployment
- [Security Configuration](docs/Security-Configuration.md) - Auth & secrets

**AI & Intelligence**
- [AI Agent Architecture](docs/AI-Agent-Architecture.md) - Agent design patterns
- [AI Evaluation Framework](docs/AI-Evaluation-Framework.md) - Testing & metrics
- [Memory Architecture](docs/Memory-Architecture.md) - Conversation persistence
- [Security Incident Management](docs/Security-Incident-Management.md) - AI guardrails

**Services**
- [Portfolio Ingest Service](docs/PortfolioIngestService.md) - Data ingestion
- [Holding Revaluation Service](docs/HoldingRevaluationService.md) - Automated valuation
- [OpenTelemetry Setup](docs/OpenTelemetry-Setup.md) - Observability

## License

Proprietary - FTO Consulting