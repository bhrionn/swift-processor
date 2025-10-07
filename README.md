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

```bash
# Build and run all services
docker-compose -f docker/docker-compose.yml up --build
```

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

## Next Steps

The project structure is now complete. Continue with the implementation tasks:

1. Implement SWIFT message parsing (Task 2)
2. Set up database layer (Task 3)
3. Implement queue management (Task 4)
4. And so on...

## Contributing

Follow the established patterns and ensure all new code includes appropriate tests and documentation.