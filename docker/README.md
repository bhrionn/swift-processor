# Docker Deployment Guide

This directory contains Docker configurations for the SWIFT Message Processor system.

## Architecture

The system consists of three containerized services:

1. **swift-api**: ASP.NET Core Web API for REST endpoints and SignalR communication
2. **swift-console**: Console application for message processing
3. **swift-frontend**: React TypeScript frontend served by Nginx

## Quick Start

### Development Environment

1. **Start all services:**
   ```bash
   cd docker
   docker-compose up -d
   ```

2. **View logs:**
   ```bash
   docker-compose logs -f
   ```

3. **Access services:**
   - Frontend: http://localhost:3000
   - API: http://localhost:5000
   - API Health: http://localhost:5000/health

4. **Stop services:**
   ```bash
   docker-compose down
   ```

### Production Environment

1. **Create environment file:**
   ```bash
   cp .env.example .env
   # Edit .env with your production values
   ```

2. **Build images:**
   ```bash
   docker-compose -f docker-compose.prod.yml build
   ```

3. **Start services:**
   ```bash
   docker-compose -f docker-compose.prod.yml up -d
   ```

## Configuration

### Environment Variables

#### Development (docker-compose.yml)
- Uses SQLite database (shared volume)
- Uses in-memory queues
- Test mode enabled
- File-based inter-process communication

#### Production (docker-compose.prod.yml)
- Uses SQL Server database
- Uses AWS SQS queues
- Test mode disabled
- Requires AWS credentials

### Volumes

- **swift-data**: Shared database storage (SQLite in development)
- **swift-communication**: Inter-process communication files

### Networks

- **swift-network**: Bridge network for service communication

## Building Images

### Build all images:
```bash
# From project root
docker build -f docker/Dockerfile.api -t swift-api:latest .
docker build -f docker/Dockerfile.console -t swift-console:latest .
docker build -f docker/Dockerfile.frontend -t swift-frontend:latest .
```

### Build specific image:
```bash
docker build -f docker/Dockerfile.api -t swift-api:v1.0.0 .
```

## Health Checks

All services include health checks:

- **swift-api**: HTTP health endpoint at `/health`
- **swift-console**: Process check
- **swift-frontend**: HTTP check on root endpoint

View health status:
```bash
docker ps
docker inspect swift-api | grep -A 10 Health
```

## Troubleshooting

### View service logs:
```bash
docker-compose logs swift-api
docker-compose logs swift-console
docker-compose logs swift-frontend
```

### Access container shell:
```bash
docker exec -it swift-api /bin/bash
docker exec -it swift-console /bin/bash
docker exec -it swift-frontend /bin/sh
```

### Check resource usage:
```bash
docker stats
```

### Restart specific service:
```bash
docker-compose restart swift-console
```

### Clean up:
```bash
# Stop and remove containers, networks
docker-compose down

# Remove volumes as well
docker-compose down -v

# Remove all unused Docker resources
docker system prune -a
```

## Service Dependencies

```
swift-frontend
  └── depends on: swift-api (healthy)
      └── depends on: swift-console (started)
```

## Port Mapping

### Development
- 3000: Frontend (Nginx)
- 5000: API (ASP.NET Core)

### Production
- 80/443: API (ASP.NET Core)
- 8080: Frontend (Nginx)

## Security Considerations

1. **Non-root users**: All containers run as non-root users
2. **Secrets**: Use Docker secrets or environment variables for sensitive data
3. **Network isolation**: Services communicate through dedicated network
4. **Health checks**: Automatic restart on failure
5. **Resource limits**: CPU and memory limits in production

## Monitoring

### Check service health:
```bash
curl http://localhost:5000/health
```

### View real-time logs:
```bash
docker-compose logs -f --tail=100
```

### Monitor resource usage:
```bash
docker stats --no-stream
```

## Scaling

### Scale console processors:
```bash
docker-compose up -d --scale swift-console=3
```

Note: Ensure proper queue configuration for multiple processors.

## Database Migrations

### Run migrations in container:
```bash
docker exec swift-api dotnet ef database update
```

### Create new migration:
```bash
docker exec swift-api dotnet ef migrations add MigrationName
```

## Backup and Restore

### Backup SQLite database (development):
```bash
docker cp swift-console:/app/data/messages.db ./backup/messages.db
```

### Restore SQLite database:
```bash
docker cp ./backup/messages.db swift-console:/app/data/messages.db
```

## CI/CD Integration

Example GitHub Actions workflow:

```yaml
- name: Build and push images
  run: |
    docker build -f docker/Dockerfile.api -t registry/swift-api:${{ github.sha }} .
    docker push registry/swift-api:${{ github.sha }}
```

## Additional Resources

- [Docker Documentation](https://docs.docker.com/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [ASP.NET Core Docker Documentation](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/)
