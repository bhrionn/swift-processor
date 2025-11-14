# Database Management Scripts

This directory contains scripts for managing database migrations, backups, and maintenance.

## Scripts Overview

### migrate-database.sh
Manages Entity Framework Core migrations for the SWIFT Message Processor database.

**Prerequisites:**
- .NET 9.0 SDK installed
- dotnet-ef tool installed (script will install if missing)

**Commands:**
```bash
# List all migrations
./scripts/migrate-database.sh list

# Add a new migration
./scripts/migrate-database.sh add AddNewField

# Update database to latest migration
./scripts/migrate-database.sh update

# Update to specific migration
./scripts/migrate-database.sh update InitialCreate

# Rollback to specific migration
./scripts/migrate-database.sh rollback InitialCreate

# Remove last migration
./scripts/migrate-database.sh remove

# Generate SQL script for all migrations
./scripts/migrate-database.sh script

# Generate SQL script between migrations
./scripts/migrate-database.sh script InitialCreate AddNewField

# Drop database (requires confirmation)
./scripts/migrate-database.sh drop

# Show help
./scripts/migrate-database.sh help
```

### backup-database.sh
Creates and manages database backups for both SQLite and SQL Server.

**Commands:**
```bash
# Backup SQLite database
./scripts/backup-database.sh sqlite /app/data/messages.db

# Backup with custom name
./scripts/backup-database.sh sqlite /app/data/messages.db my_backup

# Restore SQLite database
./scripts/backup-database.sh restore-sqlite backups/sqlite_backup_20231103_120000.db.tar.gz /app/data/messages.db

# Backup SQL Server database
./scripts/backup-database.sh sqlserver "Server=localhost;Database=SwiftMessages;User Id=sa;Password=pass"

# List all backups
./scripts/backup-database.sh list

# Clean backups older than 30 days
./scripts/backup-database.sh clean 30

# Show help
./scripts/backup-database.sh help
```

## Common Workflows

### Initial Setup
```bash
# Apply all migrations to create database
./scripts/migrate-database.sh update
```

### Adding New Features
```bash
# 1. Make changes to entity models
# 2. Create migration
./scripts/migrate-database.sh add AddNewFeature

# 3. Review generated migration files
# 4. Apply migration
./scripts/migrate-database.sh update
```

### Production Deployment
```bash
# 1. Generate SQL script for review
./scripts/migrate-database.sh script > migration.sql

# 2. Review SQL script
# 3. Apply to production database manually or via CI/CD
```

### Backup Before Major Changes
```bash
# Development (SQLite)
./scripts/backup-database.sh sqlite /app/data/messages.db pre_migration_backup

# Production (SQL Server)
./scripts/backup-database.sh sqlserver "Server=prod;Database=SwiftMessages;..."
```

### Rollback After Issues
```bash
# 1. Restore from backup
./scripts/backup-database.sh restore-sqlite backups/backup.tar.gz /app/data/messages.db

# 2. Or rollback migration
./scripts/migrate-database.sh rollback PreviousMigration
```

## Docker Integration

### Run migrations in Docker container
```bash
# API container
docker exec swift-api dotnet ef database update --project /app

# Console container
docker exec swift-console dotnet ef database update --project /app
```

### Backup from Docker container
```bash
# Copy database from container
docker cp swift-console:/app/data/messages.db ./backup/

# Backup using script
./scripts/backup-database.sh sqlite ./backup/messages.db
```

### Restore to Docker container
```bash
# Restore backup
./scripts/backup-database.sh restore-sqlite backups/backup.tar.gz ./messages.db

# Copy to container
docker cp ./messages.db swift-console:/app/data/messages.db

# Restart container
docker-compose restart swift-console
```

## CI/CD Integration

### GitHub Actions Example
```yaml
- name: Run Database Migrations
  run: |
    chmod +x scripts/migrate-database.sh
    ./scripts/migrate-database.sh update

- name: Backup Database
  run: |
    chmod +x scripts/backup-database.sh
    ./scripts/backup-database.sh sqlite /app/data/messages.db
```

### Azure DevOps Example
```yaml
- script: |
    chmod +x scripts/migrate-database.sh
    ./scripts/migrate-database.sh update
  displayName: 'Apply Database Migrations'

- script: |
    chmod +x scripts/backup-database.sh
    ./scripts/backup-database.sh list
  displayName: 'List Database Backups'
```

## Troubleshooting

### Migration Fails
```bash
# Check database connection
./scripts/migrate-database.sh list

# View detailed error
dotnet ef database update --verbose

# Rollback and retry
./scripts/migrate-database.sh rollback PreviousMigration
./scripts/migrate-database.sh update
```

### Backup Fails
```bash
# Check disk space
df -h

# Check file permissions
ls -la /app/data/

# Verify database file exists
ls -la /app/data/messages.db
```

### Cannot Connect to Database
```bash
# Check connection string in appsettings
cat src/SwiftMessageProcessor.Api/appsettings.json

# Test connection
dotnet ef dbcontext info --project src/SwiftMessageProcessor.Infrastructure
```

## Best Practices

1. **Always backup before migrations** in production
2. **Test migrations** in development/staging first
3. **Review generated SQL** before production deployment
4. **Keep backups** for at least 30 days
5. **Use idempotent scripts** for production deployments
6. **Version control** all migration files
7. **Document** any manual database changes
8. **Monitor** migration execution time
9. **Have rollback plan** ready
10. **Test restore** procedures regularly

## Backup Location

Default backup directory: `./backups/`

Backups are compressed using tar.gz format and include:
- Database file (.db)
- Write-Ahead Log (.db-wal) if exists
- Shared Memory (.db-shm) if exists

## Security Notes

- Never commit database files to version control
- Store production connection strings securely
- Use environment variables for sensitive data
- Restrict access to backup files
- Encrypt backups for production data
- Rotate backup encryption keys regularly

## Support

For issues or questions:
1. Check script help: `./scripts/migrate-database.sh help`
2. Review logs in the console output
3. Check Entity Framework documentation
4. Consult project documentation in `/docs`
