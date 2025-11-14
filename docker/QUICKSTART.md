# Quick Start Guide

Get the SWIFT Message Processor running in under 5 minutes!

## Prerequisites

- Docker and Docker Compose installed
- 4GB RAM available
- Ports 3000, 5000 available

## Start the System

```bash
# Navigate to docker directory
cd docker

# Start all services
docker-compose up -d

# Wait for services to be healthy (30-60 seconds)
docker-compose ps
```

## Access the Application

Once all services show as "healthy":

- **Frontend**: http://localhost:3000
- **API**: http://localhost:5000
- **Health Check**: http://localhost:5000/health

## Verify Everything Works

```bash
# Run health check
./healthcheck.sh

# View logs
docker-compose logs -f
```

Expected output:
```
✓ API: Running at http://localhost:5000
✓ Frontend: Running at http://localhost:3000
✓ Console: Container running
✓ Database: SQLite database exists
✓ Communication: Communication directory exists
```

## What's Running?

1. **Frontend (React)**: User interface for monitoring messages
2. **API (ASP.NET Core)**: REST API and SignalR for real-time updates
3. **Console App**: Background processor handling SWIFT messages

## Test the System

The console application automatically generates test messages every 10 seconds.

1. Open http://localhost:3000
2. Navigate to "Messages" page
3. Watch messages appear in real-time
4. Click on a message to view details

## Stop the System

```bash
# Stop all services
docker-compose down

# Stop and remove volumes (clean slate)
docker-compose down -v
```

## Troubleshooting

### Services won't start
```bash
# Check logs
docker-compose logs

# Restart specific service
docker-compose restart swift-api
```

### Port already in use
```bash
# Check what's using the port
lsof -i :5000
lsof -i :3000

# Kill the process or change ports in docker-compose.yml
```

### Database issues
```bash
# Reset database
docker-compose down -v
docker-compose up -d
```

## Next Steps

- **Add monitoring**: `docker-compose -f docker-compose.full.yml up -d`
- **View metrics**: http://localhost:9090 (Prometheus)
- **View dashboards**: http://localhost:3001 (Grafana - admin/admin)
- **Read full docs**: See [DEPLOYMENT.md](DEPLOYMENT.md)

## Common Commands

```bash
# View all logs
docker-compose logs -f

# View specific service logs
docker-compose logs -f swift-api

# Restart a service
docker-compose restart swift-console

# Check resource usage
docker stats

# Access container shell
docker exec -it swift-api /bin/bash
```

## Configuration

Default configuration uses:
- SQLite database (stored in volume)
- In-memory queues
- Test mode enabled (generates messages)

To customize, edit `docker-compose.yml` environment variables.

## Support

- Full deployment guide: [DEPLOYMENT.md](DEPLOYMENT.md)
- Docker documentation: [README.md](README.md)
- Scripts documentation: [../scripts/README.md](../scripts/README.md)
- Main project README: [../README.md](../README.md)
