# Portfolio Manager UI Service

A professional Next.js frontend application with TypeScript and AG Grid for portfolio management.

## Features

- **Next.js 14** with App Router and TypeScript
- **AG Grid Community** for advanced data tables
- **Tailwind CSS** with financial styling theme
- **Typed API Client** for seamless backend integration
- **Professional UI** with financial application styling
- **Responsive Design** for desktop and mobile
- **Real-time Data** from Portfolio Manager API

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
```

## API Integration

The UI communicates with the Portfolio Manager API:
- **Development**: `http://localhost:8080`
- **Container**: `http://portfoliomanager-api:8080`

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

### Styling
- Financial application theme
- Professional color scheme
- Responsive design
- Loading states and error handling
- Modern UI components with Tailwind CSS

## Environment Variables

- `API_BASE_URL` - Backend API URL for server-side calls
- `NEXT_PUBLIC_API_BASE_URL` - Backend API URL for client-side calls

## Architecture

```
src/
├── app/                    # Next.js App Router pages
├── components/             # React components
├── lib/                    # Utilities and API client
├── types/                  # TypeScript type definitions
└── styles/                 # Global CSS and Tailwind config
```