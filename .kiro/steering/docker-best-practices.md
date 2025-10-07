---
inclusion: always
---

# Docker Best Practices

## Dockerfile Optimization

### Multi-Stage Builds
```dockerfile
# Frontend Dockerfile
FROM node:18-alpine AS build
WORKDIR /app

# Copy package files first for better caching
COPY package*.json ./
RUN npm ci --only=production

# Copy source and build
COPY . .
RUN npm run build

# Production stage
FROM nginx:alpine AS production
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf

# Add health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/ || exit 1

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

```dockerfile
# Backend Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["SwiftMessageProcessor.Api/SwiftMessageProcessor.Api.csproj", "SwiftMessageProcessor.Api/"]
COPY ["SwiftMessageProcessor.Core/SwiftMessageProcessor.Core.csproj", "SwiftMessageProcessor.Core/"]
COPY ["SwiftMessageProcessor.Infrastructure/SwiftMessageProcessor.Infrastructure.csproj", "SwiftMessageProcessor.Infrastructure/"]
COPY ["SwiftMessageProcessor.Application/SwiftMessageProcessor.Application.csproj", "SwiftMessageProcessor.Application/"]

RUN dotnet restore "SwiftMessageProcessor.Api/SwiftMessageProcessor.Api.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/SwiftMessageProcessor.Api"
RUN dotnet build "SwiftMessageProcessor.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SwiftMessageProcessor.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --gid 1001 appuser

# Copy published application
COPY --from=publish /app/publish .

# Set ownership and permissions
RUN chown -R appuser:appgroup /app
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "SwiftMessageProcessor.Api.dll"]
```

### Layer Optimization
- Copy dependency files first to leverage Docker layer caching
- Use `.dockerignore` to exclude unnecessary files
- Minimize the number of RUN instructions
- Use specific base image tags, avoid `latest`

### .dockerignore Configuration
```dockerignore
# Frontend
node_modules
npm-debug.log*
yarn-debug.log*
yarn-error.log*
.env.local
.env.development.local
.env.test.local
.env.production.local
coverage/
build/
dist/

# Backend
bin/
obj/
*.user
*.suo
*.cache
.vs/
.vscode/
TestResults/
packages/

# General
.git
.gitignore
README.md
Dockerfile*
docker-compose*
.dockerignore
```

## Docker Compose Configuration

### Development Environment
```yaml
version: '3.8'

services:
  swift-api:
    build:
      context: .
      dockerfile: docker/Dockerfile.api
      target: development
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - Database__Provider=SQLite
      - Database__ConnectionString=Data Source=/app/data/messages.db
      - Queue__Provider=InMemory
    volumes:
      - ./src:/app/src:ro
      - swift-data:/app/data
    depends_on:
      - swift-db
    networks:
      - swift-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  swift-frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
      target: development
    ports:
      - "3000:3000"
    environment:
      - REACT_APP_API_URL=http://localhost:5000
      - REACT_APP_SIGNALR_URL=http://localhost:5000/messageHub
    volumes:
      - ./frontend/src:/app/src:ro
      - /app/node_modules
    depends_on:
      - swift-api
    networks:
      - swift-network
    restart: unless-stopped

  swift-db:
    image: postgres:15-alpine
    environment:
      - POSTGRES_DB=swiftmessages
      - POSTGRES_USER=swiftuser
      - POSTGRES_PASSWORD=swiftpass
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/init-db.sql:ro
    ports:
      - "5432:5432"
    networks:
      - swift-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U swiftuser -d swiftmessages"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  swift-data:
    driver: local
  postgres-data:
    driver: local

networks:
  swift-network:
    driver: bridge
```

### Production Environment
```yaml
version: '3.8'

services:
  swift-api:
    image: swift-message-processor:${VERSION:-latest}
    ports:
      - "80:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Database__Provider=SqlServer
      - Database__ConnectionString=${DATABASE_CONNECTION_STRING}
      - Queue__Provider=AmazonSQS
      - Queue__Region=${AWS_REGION}
      - AWS_ACCESS_KEY_ID=${AWS_ACCESS_KEY_ID}
      - AWS_SECRET_ACCESS_KEY=${AWS_SECRET_ACCESS_KEY}
    secrets:
      - db_connection_string
      - aws_credentials
    deploy:
      replicas: 3
      update_config:
        parallelism: 1
        delay: 10s
        order: start-first
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
      resources:
        limits:
          cpus: '1.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
    networks:
      - swift-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s

  swift-frontend:
    image: swift-frontend:${VERSION:-latest}
    ports:
      - "443:80"
    environment:
      - REACT_APP_API_URL=https://api.swiftprocessor.com
    deploy:
      replicas: 2
      update_config:
        parallelism: 1
        delay: 10s
      restart_policy:
        condition: on-failure
    networks:
      - swift-network

secrets:
  db_connection_string:
    external: true
  aws_credentials:
    external: true

networks:
  swift-network:
    driver: overlay
    attachable: true
```

## Security Best Practices

### Container Security
```dockerfile
# Use specific, minimal base images
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime

# Create non-root user
RUN addgroup -g 1001 -S appgroup && \
    adduser -u 1001 -S appuser -G appgroup

# Set working directory and copy files
WORKDIR /app
COPY --from=publish --chown=appuser:appgroup /app/publish .

# Remove unnecessary packages and clean cache
RUN apk del --purge curl && \
    rm -rf /var/cache/apk/*

# Use non-root user
USER appuser

# Set read-only root filesystem
# Add to docker-compose.yml:
# security_opt:
#   - no-new-privileges:true
# read_only: true
# tmpfs:
#   - /tmp
#   - /var/tmp
```

### Secrets Management
```yaml
# docker-compose.yml
services:
  swift-api:
    secrets:
      - source: db_password
        target: /run/secrets/db_password
        mode: 0400
    environment:
      - DATABASE_PASSWORD_FILE=/run/secrets/db_password

secrets:
  db_password:
    file: ./secrets/db_password.txt
```

### Network Security
```yaml
# Isolate services with custom networks
networks:
  frontend:
    driver: bridge
    internal: false
  backend:
    driver: bridge
    internal: true
  database:
    driver: bridge
    internal: true

services:
  frontend:
    networks:
      - frontend
      - backend
  
  api:
    networks:
      - backend
      - database
  
  database:
    networks:
      - database
```

## Performance Optimization

### Resource Limits
```yaml
services:
  swift-api:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
          pids: 100
        reservations:
          cpus: '1.0'
          memory: 1G
    ulimits:
      nofile:
        soft: 65536
        hard: 65536
      memlock:
        soft: -1
        hard: -1
```

### Volume Optimization
```yaml
volumes:
  # Use named volumes for better performance
  app-data:
    driver: local
    driver_opts:
      type: none
      o: bind
      device: /opt/swift-data

  # Use tmpfs for temporary data
  tmpfs-data:
    driver: tmpfs
    driver_opts:
      size: 100m
```

## Monitoring and Logging

### Health Checks
```dockerfile
# Application health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health/ready || exit 1
```

```yaml
# Compose health checks
services:
  swift-api:
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    depends_on:
      swift-db:
        condition: service_healthy
```

### Logging Configuration
```yaml
services:
  swift-api:
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
        labels: "service,version"
    labels:
      - "service=swift-api"
      - "version=${VERSION:-latest}"
```

### Monitoring Stack
```yaml
# monitoring/docker-compose.yml
version: '3.8'

services:
  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/etc/prometheus/console_libraries'
      - '--web.console.templates=/etc/prometheus/consoles'

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3001:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-data:/var/lib/grafana
      - ./grafana/dashboards:/etc/grafana/provisioning/dashboards:ro
      - ./grafana/datasources:/etc/grafana/provisioning/datasources:ro

volumes:
  prometheus-data:
  grafana-data:
```

## Development Workflow

### Development Dockerfile
```dockerfile
# Development stage with hot reload
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development
WORKDIR /app

# Install dotnet tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# Copy project files
COPY *.sln ./
COPY */*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p ${file%.*}/ && mv $file ${file%.*}/; done

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Set development environment
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

EXPOSE 8080
CMD ["dotnet", "watch", "run", "--project", "SwiftMessageProcessor.Api"]
```

### Build Scripts
```bash
#!/bin/bash
# scripts/build.sh

set -e

VERSION=${1:-latest}
REGISTRY=${REGISTRY:-localhost:5000}

echo "Building Swift Message Processor v${VERSION}"

# Build backend
docker build -f docker/Dockerfile.api -t ${REGISTRY}/swift-api:${VERSION} .

# Build frontend
docker build -f docker/Dockerfile.frontend -t ${REGISTRY}/swift-frontend:${VERSION} ./frontend

# Tag as latest
docker tag ${REGISTRY}/swift-api:${VERSION} ${REGISTRY}/swift-api:latest
docker tag ${REGISTRY}/swift-frontend:${VERSION} ${REGISTRY}/swift-frontend:latest

echo "Build completed successfully"
```

### Testing in Docker
```yaml
# docker-compose.test.yml
version: '3.8'

services:
  swift-api-test:
    build:
      context: .
      dockerfile: docker/Dockerfile.api
      target: test
    environment:
      - ASPNETCORE_ENVIRONMENT=Testing
    volumes:
      - ./test-results:/app/test-results
    command: ["dotnet", "test", "--logger", "trx;LogFileName=test-results.trx"]

  integration-tests:
    build:
      context: .
      dockerfile: docker/Dockerfile.integration-tests
    depends_on:
      - swift-api
      - swift-db
    environment:
      - API_BASE_URL=http://swift-api:8080
    volumes:
      - ./test-results:/app/test-results
```

## CI/CD Integration

### GitHub Actions Workflow
```yaml
# .github/workflows/docker.yml
name: Docker Build and Push

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3
        
      - name: Login to Container Registry
        uses: docker/login-action@v3
        with:
          registry: ${{ secrets.REGISTRY_URL }}
          username: ${{ secrets.REGISTRY_USERNAME }}
          password: ${{ secrets.REGISTRY_PASSWORD }}
          
      - name: Build and push API
        uses: docker/build-push-action@v5
        with:
          context: .
          file: ./docker/Dockerfile.api
          push: true
          tags: |
            ${{ secrets.REGISTRY_URL }}/swift-api:${{ github.sha }}
            ${{ secrets.REGISTRY_URL }}/swift-api:latest
          cache-from: type=gha
          cache-to: type=gha,mode=max
          
      - name: Run security scan
        uses: aquasecurity/trivy-action@master
        with:
          image-ref: ${{ secrets.REGISTRY_URL }}/swift-api:${{ github.sha }}
          format: 'sarif'
          output: 'trivy-results.sarif'
```

## Troubleshooting

### Common Issues and Solutions

#### Container Won't Start
```bash
# Check logs
docker logs <container_name>

# Check resource usage
docker stats <container_name>

# Inspect container configuration
docker inspect <container_name>
```

#### Performance Issues
```bash
# Monitor resource usage
docker stats --no-stream

# Check disk usage
docker system df

# Clean up unused resources
docker system prune -a
```

#### Network Connectivity
```bash
# Test network connectivity
docker exec <container> ping <target>

# List networks
docker network ls

# Inspect network configuration
docker network inspect <network_name>
```

### Debugging Tools
```dockerfile
# Add debugging tools to development image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS debug
RUN apt-get update && apt-get install -y \
    curl \
    netcat-openbsd \
    procps \
    && rm -rf /var/lib/apt/lists/*
```