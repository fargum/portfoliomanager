# Portfolio Manager Docker Deployment Guide

This guide covers running the Portfolio Manager API in Docker containers, connecting to your existing PostgreSQL database container.

## Prerequisites

- Docker installed on your system
- Docker Compose (usually included with Docker Desktop)
- Existing PostgreSQL database container running

## Quick Start

### 1. Build the Docker Image

**Windows:**
```bash
build-docker.bat
```

**Linux/macOS:**
```bash
chmod +x build-docker.sh
./build-docker.sh
```

**Manual build:**
```bash
docker build -t portfoliomanager-api:latest .
```

### 2. Configure Database Connection

Update the connection string in `docker-compose.yml` to match your PostgreSQL container:

```yaml
- ConnectionStrings__DefaultConnection=Host=your-db-host;Port=5432;Database=portfoliomanager;Username=postgres;Password=your_password
```

**Common database host configurations:**
- If database is in Docker Compose stack: `postgres` (service name)
- If database is in separate container: `host.docker.internal` (Windows/Mac) or container IP
- If database is on host machine: `host.docker.internal` (Windows/Mac) or `172.17.0.1` (Linux)

### 3. Run with Docker Compose

```bash
docker-compose up -d
```

This will:
- Build the API image if not already built
- Start the Portfolio Manager API container
- Expose the API on port 8080
- Connect to your database

### 4. Verify Deployment

**Check container status:**
```bash
docker-compose ps
```

**Check logs:**
```bash
docker-compose logs portfoliomanager-api
```

**Test API endpoints:**
- Health check: http://localhost:8080/health
- Swagger UI: http://localhost:8080/swagger
- API: http://localhost:8080/api/portfolios/

## Configuration Options

### Environment Variables

The following environment variables can be configured in `docker-compose.yml`:

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | Production |
| `ASPNETCORE_URLS` | URLs the server listens on | http://+:8080 |
| `ConnectionStrings__DefaultConnection` | Database connection string | (required) |
| `Logging__LogLevel__Default` | Log level | Information |
| `AllowedHosts` | Allowed host headers | * |

### Custom Configuration

Create your own `docker-compose.override.yml` for custom settings:

```yaml
version: '3.8'
services:
  portfoliomanager-api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - Logging__LogLevel__Default=Debug
    ports:
      - "8081:8080"  # Custom port mapping
```

## Docker Commands Reference

### Build Commands
```bash
# Build image
docker build -t portfoliomanager-api:latest .

# Build with custom tag
docker build -t portfoliomanager-api:v1.0.0 .
```

### Run Commands
```bash
# Run standalone container
docker run -d \
  --name portfoliomanager-api \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=portfoliomanager;Username=postgres;Password=yourpassword" \
  portfoliomanager-api:latest

# Run with docker-compose
docker-compose up -d

# Run with custom compose file
docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d
```

### Management Commands
```bash
# View logs
docker-compose logs -f portfoliomanager-api

# Stop services
docker-compose down

# Restart service
docker-compose restart portfoliomanager-api

# Update and restart
docker-compose build --no-cache portfoliomanager-api
docker-compose up -d --force-recreate portfoliomanager-api
```

## Database Connection Examples

### Scenario 1: Database in same Docker Compose stack
```yaml
services:
  postgres:
    image: postgres:15
    # ... postgres config
  
  portfoliomanager-api:
    # ... api config
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=portfoliomanager;Username=postgres;Password=yourpassword
```

### Scenario 2: External database container
```bash
# Get database container IP
docker inspect your-postgres-container | grep IPAddress

# Use in connection string
ConnectionStrings__DefaultConnection=Host=172.17.0.2;Port=5432;Database=portfoliomanager;Username=postgres;Password=yourpassword
```

### Scenario 3: Database on host machine
```yaml
environment:
  - ConnectionStrings__DefaultConnection=Host=host.docker.internal;Port=5432;Database=portfoliomanager;Username=postgres;Password=yourpassword
```

## Networking

### Docker Network Setup
The API container needs to communicate with your database. Options:

1. **Same Docker Compose stack** (recommended)
2. **Custom Docker network:**
   ```bash
   docker network create portfoliomanager-network
   docker run --network portfoliomanager-network your-postgres-container
   docker run --network portfoliomanager-network portfoliomanager-api
   ```

3. **Host networking:**
   ```bash
   docker run --network host portfoliomanager-api
   ```

## Troubleshooting

### Common Issues

**1. Database connection fails:**
```bash
# Check if database is accessible
docker exec -it portfoliomanager-api ping your-db-host

# Check connection string format
# Ensure username/password are correct
# Verify database exists
```

**2. Port already in use:**
```bash
# Change port mapping in docker-compose.yml
ports:
  - "8081:8080"  # Use different host port
```

**3. Container won't start:**
```bash
# Check logs for details
docker-compose logs portfoliomanager-api

# Check container status
docker ps -a
```

**4. Health check failing:**
```bash
# Test health endpoint manually
curl http://localhost:8080/health

# Check database connectivity from container
docker exec -it portfoliomanager-api curl localhost:8080/health
```

### Debug Mode

To run in development mode with detailed logging:

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - Logging__LogLevel__Default=Debug
  - Logging__LogLevel__Microsoft.EntityFrameworkCore=Debug
```

## Production Deployment

### Security Considerations

1. **Use specific image tags** (not `latest`)
2. **Set proper environment** (`Production`)
3. **Use secrets management** for database passwords
4. **Enable HTTPS** in production
5. **Restrict allowed hosts**

### Example Production Configuration

```yaml
version: '3.8'
services:
  portfoliomanager-api:
    image: portfoliomanager-api:v1.0.0
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=https://+:443;http://+:80
      - ConnectionStrings__DefaultConnection=${DATABASE_CONNECTION_STRING}
      - AllowedHosts=your-domain.com,api.your-domain.com
    ports:
      - "443:443"
      - "80:80"
    volumes:
      - ./certs:/app/certs:ro
    restart: unless-stopped
```

## Integration with UI Service

When you add your UI container later:

```yaml
version: '3.8'
services:
  portfoliomanager-api:
    # ... api configuration
    networks:
      - portfoliomanager-network
  
  portfoliomanager-ui:
    # ... ui configuration
    environment:
      - API_BASE_URL=http://portfoliomanager-api:8080
    networks:
      - portfoliomanager-network
    depends_on:
      - portfoliomanager-api

networks:
  portfoliomanager-network:
```

The UI will be able to call the API using the service name `portfoliomanager-api` as the hostname.

## Monitoring

### Health Checks
- Health endpoint: `/health`
- Returns 200 OK when healthy
- Can be used with Docker health checks, load balancers, etc.

### Logging
- Structured JSON logging in production
- Log levels configurable via environment variables
- Container logs accessible via `docker-compose logs`

This setup provides a robust, scalable foundation for your containerized Portfolio Manager API!