# Multi-Service Deployment Guide

This guide covers deploying the SWIFT Message Processor as a distributed system with multiple services.

## Architecture Overview

The system consists of three main services:

1. **swift-api**: Web API for REST endpoints and SignalR real-time communication
2. **swift-console**: Background processor for message processing
3. **swift-frontend**: React frontend served by Nginx

### Service Communication

```
┌─────────────┐
│   Frontend  │
│   (Nginx)   │
└──────┬──────┘
       │ HTTP/SignalR
       ▼
┌─────────────┐     File System      ┌──────────────┐
│   Web API   │◄────Communication────►│   Console    │
│  (ASP.NET)  │                       │     App      │
└──────┬──────┘                       └──────┬───────┘
       │                                     │
       │         Shared Database             │
       └─────────────┬───────────────────────┘
                     ▼
              ┌─────────────┐
              │   SQLite    │
              │  (Dev/Test) │
              └─────────────┘
```

## Deployment Options

### Option 1: Basic Development (docker-compose.yml)
Minimal setup for local development:
```bash
docker-compose up -d
```

Services:
- API: http://localhost:5000
- Frontend: http://localhost:3000
- Console: Background service

### Option 2: Full Stack with Monitoring (docker-compose.full.yml)
Complete setup with monitoring and logging:
```bash
docker-compose -f docker-compose.full.yml up -d
```

Additional services:
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3001 (admin/admin)
- Loki: http://localhost:3100

### Option 3: Production (docker-compose.prod.yml)
Production-ready configuration:
```bash
# Set environment variables
cp .env.example .env
# Edit .env with production values

# Deploy
docker-compose -f docker-compose.prod.yml up -d
```

## Service Dependencies

### Startup Order
1. **swift-console** starts first (no dependencies)
2. **swift-api** starts after console (depends on: swift-console)
3. **swift-frontend** starts last (depends on: swift-api healthy)

### Health Checks
All services include health checks:
- **API**: HTTP endpoint `/health`
- **Console**: Process check
- **Frontend**: HTTP root check

View health status:
```bash
docker ps
docker inspect swift-api | grep -A 10 Health
```

## Shared Resources

### Volumes

#### swift-data
- **Purpose**: Shared database storage
- **Used by**: swift-api, swift-console
- **Contains**: SQLite database file (messages.db)
- **Backup**: Use `scripts/backup-database.sh`

#### swift-communication
- **Purpose**: Inter-process communication
- **Used by**: swift-api, swift-console
- **Contains**: status.json, command.json
- **Format**: JSON files for status and commands

#### swift-logs (optional)
- **Purpose**: Centralized logging
- **Used by**: All services
- **Contains**: Application logs
- **Rotation**: Configured in logging settings

### Networks

#### swift-network
- **Type**: Bridge
- **Purpose**: Service-to-service communication
- **Services**: swift-api, swift-console, swift-frontend

#### monitoring-network (full deployment)
- **Type**: Bridge
- **Purpose**: Monitoring stack isolation
- **Services**: prometheus, grafana, loki, promtail

## Inter-Service Communication

### API ↔ Console Communication

The Web API and Console Application communicate through:

1. **Shared Database**
   - Console writes processed messages
   - API reads messages for display
   - Real-time updates via polling or triggers

2. **File-Based Communication**
   ```
   /app/communication/
   ├── status.json    # Console writes status
   └── command.json   # API writes commands
   ```

3. **Status Updates**
   ```json
   {
     "status": "Running",
     "messagesProcessed": 1234,
     "lastProcessedAt": "2023-11-03T12:00:00Z",
     "errors": 5
   }
   ```

4. **Commands**
   ```json
   {
     "command": "Restart",
     "timestamp": "2023-11-03T12:00:00Z",
     "requestedBy": "admin"
   }
   ```

### Frontend ↔ API Communication

1. **REST API**
   - HTTP requests for data retrieval
   - Standard CRUD operations
   - Authentication via API keys

2. **SignalR WebSocket**
   - Real-time message updates
   - System status broadcasts
   - Processor control feedback

## Configuration Management

### Environment-Specific Settings

#### Development
```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - Database__Provider=SQLite
  - Queue__Provider=InMemory
  - TestMode__Enabled=true
```

#### Production
```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Production
  - Database__Provider=SqlServer
  - Queue__Provider=AmazonSQS
  - TestMode__Enabled=false
```

### Configuration Precedence
1. Environment variables (highest)
2. appsettings.{Environment}.json
3. appsettings.json (lowest)

## Monitoring and Observability

### Metrics (Prometheus)

Access Prometheus at http://localhost:9090

Key metrics:
- `swift_messages_processed_total`: Total messages processed
- `swift_messages_failed_total`: Total failed messages
- `swift_processing_duration_seconds`: Processing time
- `swift_queue_depth`: Current queue depth

### Dashboards (Grafana)

Access Grafana at http://localhost:3001 (admin/admin)

Pre-configured dashboards:
- System Overview
- Message Processing Metrics
- Error Rates and Trends
- Resource Utilization

### Logs (Loki)

Centralized logging with Loki:
- Structured JSON logs
- Log aggregation from all services
- Query via Grafana or Loki API

Query examples:
```logql
# All logs from API
{service="swift-api"}

# Error logs from all services
{level="Error"}

# Logs for specific message
{logger="MessageProcessingService"} |= "message-id-123"
```

## Health Monitoring

### Automated Health Checks

Run health check script:
```bash
./docker/healthcheck.sh
```

Output:
```
SWIFT Message Processor - Health Check
========================================

✓ API: Running at http://localhost:5000
✓ Frontend: Running at http://localhost:3000
✓ Console: Container running
✓ Database: SQLite database exists
✓ Communication: Communication directory exists
⚠ Prometheus: Not running (optional)
⚠ Grafana: Not running (optional)

========================================
All critical services are healthy
```

### Manual Health Checks

```bash
# Check API health
curl http://localhost:5000/health

# Check container status
docker ps --filter "name=swift-"

# Check logs
docker-compose logs -f swift-api
docker-compose logs -f swift-console

# Check resource usage
docker stats
```

## Scaling Considerations

### Horizontal Scaling

#### Console Processors
```bash
# Scale console processors
docker-compose up -d --scale swift-console=3
```

**Requirements:**
- Shared database with proper locking
- Queue-based message distribution
- Unique instance identifiers

#### API Instances
```bash
# Scale API instances
docker-compose up -d --scale swift-api=3
```

**Requirements:**
- Load balancer (nginx, traefik)
- Shared session storage
- SignalR backplane (Redis)

### Vertical Scaling

Adjust resource limits in docker-compose:
```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 2G
    reservations:
      cpus: '1.0'
      memory: 1G
```

## Troubleshooting

### Service Won't Start

```bash
# Check logs
docker-compose logs swift-api

# Check configuration
docker exec swift-api cat /app/appsettings.json

# Verify volumes
docker volume inspect swift-data
```

### Communication Issues

```bash
# Check communication files
docker exec swift-console ls -la /app/communication/
docker exec swift-api ls -la /app/communication/

# Verify file contents
docker exec swift-console cat /app/communication/status.json
```

### Database Issues

```bash
# Check database file
docker exec swift-console ls -la /app/data/

# Backup database
./scripts/backup-database.sh sqlite /app/data/messages.db

# Run migrations
docker exec swift-api dotnet ef database update
```

### Performance Issues

```bash
# Monitor resources
docker stats

# Check disk usage
docker system df

# View detailed metrics
curl http://localhost:9090/api/v1/query?query=swift_messages_processed_total
```

## Backup and Recovery

### Backup Procedure

```bash
# 1. Backup database
./scripts/backup-database.sh sqlite /app/data/messages.db

# 2. Backup volumes
docker run --rm -v swift-data:/data -v $(pwd)/backup:/backup \
  alpine tar czf /backup/swift-data.tar.gz -C /data .

# 3. Export configurations
docker-compose config > backup/docker-compose-backup.yml
```

### Recovery Procedure

```bash
# 1. Stop services
docker-compose down

# 2. Restore database
./scripts/backup-database.sh restore-sqlite backup/backup.tar.gz /app/data/messages.db

# 3. Restore volumes
docker run --rm -v swift-data:/data -v $(pwd)/backup:/backup \
  alpine tar xzf /backup/swift-data.tar.gz -C /data

# 4. Start services
docker-compose up -d
```

## Security Best Practices

1. **Use secrets for sensitive data**
   ```yaml
   secrets:
     - db_password
     - api_key
   ```

2. **Run as non-root user** (already configured)

3. **Limit network exposure**
   ```yaml
   networks:
     - internal  # No external access
   ```

4. **Enable TLS/SSL** in production

5. **Regular security updates**
   ```bash
   docker-compose pull
   docker-compose up -d
   ```

## Maintenance

### Regular Tasks

1. **Log rotation** (automated via Docker)
2. **Database backups** (daily recommended)
3. **Clean old backups** (monthly)
   ```bash
   ./scripts/backup-database.sh clean 30
   ```
4. **Update images** (monthly)
   ```bash
   docker-compose pull
   docker-compose up -d
   ```
5. **Monitor disk usage**
   ```bash
   docker system df
   docker system prune -a
   ```

### Upgrade Procedure

```bash
# 1. Backup everything
./scripts/backup-database.sh sqlite /app/data/messages.db

# 2. Pull new images
docker-compose pull

# 3. Stop services
docker-compose down

# 4. Start with new images
docker-compose up -d

# 5. Verify health
./docker/healthcheck.sh
```

## Support and Documentation

- Docker README: `docker/README.md`
- Scripts README: `scripts/README.md`
- API Documentation: http://localhost:5000/swagger
- Grafana Dashboards: http://localhost:3001

For issues:
1. Check service logs
2. Run health check script
3. Review configuration
4. Consult documentation
