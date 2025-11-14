# SWIFT Message Processor

A comprehensive SWIFT message processing system built with .NET Core 9.0 and React TypeScript.

## Project Structure

```
SwiftMessageProcessor/
├── src/
│   ├── SwiftMessageProcessor.Core/          # Domain models and interfaces
│   ├── SwiftMessageProcessor.Infrastructure/ # Data access and external services
│   ├── SwiftMessageProcessor.Application/   # Business logic and services
│   ├── SwiftMessageProcessor.Api/           # Web API project
│   └── SwiftMessageProcessor.Console/       # Console application for message processing
├── frontend/                                # React TypeScript frontend
├── docker/                                  # Docker configuration files
├── tests/                                   # Test projects (to be created)
└── docs/                                    # SWIFT documentation and examples
```

## Architecture

The system follows a clean architecture pattern with clear separation of concerns:

- **Core**: Contains domain models, interfaces, and business rules
- **Infrastructure**: Implements external concerns (database, queues, external APIs)
- **Application**: Contains business logic and orchestrates the domain
- **API**: Web API for frontend communication and system management
- **Console**: Background service for message processing
- **Frontend**: React TypeScript application for monitoring and management

## Key Features

- MT103 SWIFT message processing (extensible to other message types)
- Queue-based message processing architecture
- Real-time updates via SignalR
- Docker containerization support
- Environment-specific configuration
- Health checks and monitoring
- Test message generation for development

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- Node.js 18+
- Docker (optional)

### Development Setup

1. **Backend Setup**:
   ```bash
   dotnet restore
   dotnet build
   ```

2. **Frontend Setup**:
   ```bash
   cd frontend
   npm install
   npm run build
   ```

3. **Run Applications**:
   ```bash
   # Start API
   dotnet run --project src/SwiftMessageProcessor.Api

   # Start Console Application
   dotnet run --project src/SwiftMessageProcessor.Console

   # Start Frontend (development)
   cd frontend
   npm run dev
   ```

### Docker Setup

#### Quick Start (Development)
```bash
cd docker
docker-compose up -d
```

Access services:
- Frontend: http://localhost:3000
- API: http://localhost:5000
- API Health: http://localhost:5000/health

#### Full Stack with Monitoring
```bash
cd docker
docker-compose -f docker-compose.full.yml up -d
```

Additional services:
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3001 (admin/admin)

#### Production Deployment
```bash
cd docker
cp .env.example .env
# Edit .env with production values
docker-compose -f docker-compose.prod.yml up -d
```

For detailed deployment instructions, see [docker/DEPLOYMENT.md](docker/DEPLOYMENT.md)

## Configuration

The system supports environment-specific configuration:

- **Development**: Uses SQLite database and in-memory queues
- **Production**: Configurable for SQL Server and AWS SQS

Configuration files:
- `src/SwiftMessageProcessor.Api/appsettings.Development.json`
- `src/SwiftMessageProcessor.Console/appsettings.Development.json`
- `frontend/.env.development`

## API Endpoints

- `GET /api/messages` - Retrieve processed messages
- `GET /api/system/status` - Get system status
- `POST /api/system/restart` - Restart message processor
- `GET /health` - Health check endpoint

## Development

This project follows SOLID principles and clean architecture patterns. Key interfaces:

- `ISwiftMessageParser<T>` - Message parsing
- `IQueueService` - Queue operations
- `IMessageRepository` - Data persistence
- `IMessageProcessingService` - Message processing orchestration

## Deployment

The system supports multiple deployment scenarios:

### Local Development
- SQLite database
- In-memory queues
- File-based inter-process communication
- Test mode enabled

### Production
- SQL Server database
- AWS SQS queues
- Scalable architecture
- Monitoring and logging

See [docker/DEPLOYMENT.md](docker/DEPLOYMENT.md) for comprehensive deployment guide.

## Database Management

### Migrations
```bash
# Apply migrations
./scripts/migrate-database.sh update

# Create new migration
./scripts/migrate-database.sh add MigrationName

# Rollback migration
./scripts/migrate-database.sh rollback MigrationName
```

### Backups
```bash
# Backup SQLite database
./scripts/backup-database.sh sqlite /app/data/messages.db

# Restore database
./scripts/backup-database.sh restore-sqlite backup.tar.gz /app/data/messages.db

# List backups
./scripts/backup-database.sh list
```

See [scripts/README.md](scripts/README.md) for detailed script documentation.

## Monitoring

### Health Checks
```bash
# Run health check script
./docker/healthcheck.sh

# Check individual services
curl http://localhost:5000/health
docker ps --filter "name=swift-"
```

### Metrics and Logs
- Prometheus metrics: http://localhost:9090
- Grafana dashboards: http://localhost:3001
- Centralized logging with Loki

## Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/SwiftMessageProcessor.Core.Tests

# Frontend tests
cd frontend
npm test
```

## Contributing

Follow the established patterns and ensure all new code includes appropriate tests and documentation.