# Portfolio Manager UI Service

A professional Next.js frontend application with TypeScript, AG Grid, and AI-powered chat for portfolio management.

## Features

### 🏦 Portfolio Management
- **Next.js 14** with App Router and TypeScript
- **AG Grid Community** for advanced data tables
- **Tailwind CSS** with financial styling theme
- **Typed API Client** for seamless backend integration
- **Professional UI** with financial application styling
- **Responsive Design** for desktop and mobile
- **Real-time Data** from Portfolio Manager API

### 🤖 AI-Powered Chat Assistant
- **Natural Language Queries**: Ask questions about your portfolio in plain English
- **Intelligent Responses**: Get insights about performance, holdings, and market conditions
- **Multiple AI Tools**: Access to 6 specialized portfolio analysis tools
- **Real-time Integration**: Direct connection to Portfolio Manager API and Azure OpenAI
- **Interactive Interface**: Modern chat UI with message history and loading states

## Development

```bash
# Install dependencies
npm install

# Run development server
npm run dev

# Build for production
npm run build

# Start production server
npm start

# Run linting with auto-fix
npm run lint

# Type checking
npm run type-check
```

## API Integration

The UI communicates with the Portfolio Manager API:
- **Development**: `http://localhost:8080`
- **Container**: `http://portfoliomanager-api:8080`

### Endpoints Used
- `GET /api/holdings/account/{accountId}/date/{date}` - Portfolio holdings
- `POST /api/ai/chat/query` - AI chat queries
- `GET /api/ai/chat/tools` - Available AI tools
- `GET /api/ai/chat/health` - AI service health
- `GET /health` - API health check

## Docker

```bash
# Build UI Docker image
docker build -t portfoliomanager-ui:latest .

# Run with Docker Compose (includes API)
docker-compose up -d
```

## Features

### Holdings Grid
- Search holdings by account ID and valuation date
- Professional financial data display
- Currency and number formatting
- Export capabilities (AG Grid Community features)
- Column sorting, filtering, and resizing
- Real-time API status indicator

### AI Chat Assistant
Ask natural language questions like:
- *"How is my portfolio performing today?"*
- *"What are my top holdings?"*
- *"Show me market sentiment for AAPL"*
- *"Compare my performance from last week to now"*
- *"What news might be affecting my stocks?"*

The AI assistant has access to:
- Portfolio holdings data
- Performance analysis tools
- Market intelligence and news
- Sentiment analysis
- Historical comparisons

### Responsive Design
- **Desktop**: Side-by-side holdings grid and chat interface
- **Mobile**: Tabbed interface for easy navigation
- **Service Monitoring**: Real-time API and AI service status indicators

### Styling
- Financial application theme
- Professional color scheme
- Responsive design
- Loading states and error handling
- Modern UI components with Tailwind CSS
- Chat-specific styling for messages and interactions

## Environment Variables

- `API_BASE_URL` - Backend API URL for server-side calls
- `NEXT_PUBLIC_API_BASE_URL` - Backend API URL for client-side calls (default: http://localhost:8080)

## Architecture

```
src/
├── app/                    # Next.js App Router pages
│   ├── globals.css        # Global styles with chat styling
│   ├── layout.tsx         # Root layout
│   └── page.tsx           # Main dashboard page
├── components/             # React components
│   ├── AiChat.tsx         # AI chat interface
│   ├── HoldingsGrid.tsx   # Portfolio holdings grid
│   └── PortfolioDashboard.tsx # Responsive dashboard layout
├── lib/                    # Utilities and API client
│   ├── api-client.ts      # API client with chat endpoints
│   └── grid-utils.ts      # Grid utilities
├── types/                  # TypeScript type definitions
│   ├── api.ts             # Portfolio API types
│   └── chat.ts            # Chat and AI types
└── styles/                 # Global CSS and Tailwind config
```

## Usage

1. **View Portfolio Holdings**: Interactive grid with your current positions
2. **Chat with AI**: Ask questions about your portfolio in natural language
3. **Monitor Services**: Check API and AI service status in the header
4. **Responsive Layout**: Seamlessly switch between desktop and mobile views

## Technology Stack

- **Frontend**: Next.js 14, React 18, TypeScript
- **Styling**: Tailwind CSS with custom financial design system
- **Data Grid**: AG Grid Community Edition
- **Icons**: Lucide React
- **AI Integration**: Azure OpenAI via Portfolio Manager API
- **Architecture**: Model Context Protocol (MCP) for AI tool integration