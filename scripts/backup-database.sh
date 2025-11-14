#!/bin/bash

# Database Backup Script for SWIFT Message Processor
# Supports both SQLite (development) and SQL Server (production)

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKUP_DIR="$PROJECT_ROOT/backups"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")

# Function to print colored output
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Create backup directory if it doesn't exist
mkdir -p "$BACKUP_DIR"

# Function to backup SQLite database
backup_sqlite() {
    local db_path=$1
    local backup_name=$2
    
    if [ -z "$db_path" ]; then
        print_error "Database path is required"
        echo "Usage: $0 sqlite <database-path> [backup-name]"
        exit 1
    fi
    
    if [ ! -f "$db_path" ]; then
        print_error "Database file not found: $db_path"
        exit 1
    fi
    
    if [ -z "$backup_name" ]; then
        backup_name="sqlite_backup_${TIMESTAMP}.db"
    fi
    
    local backup_file="$BACKUP_DIR/$backup_name"
    
    print_info "Backing up SQLite database..."
    print_info "Source: $db_path"
    print_info "Destination: $backup_file"
    
    # Copy database file
    cp "$db_path" "$backup_file"
    
    # Also backup WAL and SHM files if they exist
    if [ -f "${db_path}-wal" ]; then
        cp "${db_path}-wal" "${backup_file}-wal"
    fi
    
    if [ -f "${db_path}-shm" ]; then
        cp "${db_path}-shm" "${backup_file}-shm"
    fi
    
    # Compress backup
    print_info "Compressing backup..."
    tar -czf "${backup_file}.tar.gz" -C "$BACKUP_DIR" "$(basename "$backup_file")"* 2>/dev/null || true
    
    # Remove uncompressed files
    rm -f "$backup_file" "${backup_file}-wal" "${backup_file}-shm"
    
    print_info "Backup completed: ${backup_file}.tar.gz"
    print_info "Backup size: $(du -h "${backup_file}.tar.gz" | cut -f1)"
}

# Function to restore SQLite database
restore_sqlite() {
    local backup_file=$1
    local target_path=$2
    
    if [ -z "$backup_file" ] || [ -z "$target_path" ]; then
        print_error "Backup file and target path are required"
        echo "Usage: $0 restore-sqlite <backup-file> <target-path>"
        exit 1
    fi
    
    if [ ! -f "$backup_file" ]; then
        print_error "Backup file not found: $backup_file"
        exit 1
    fi
    
    print_warning "This will overwrite the existing database at: $target_path"
    print_warning "Are you sure? (yes/no)"
    read -r confirmation
    
    if [ "$confirmation" != "yes" ]; then
        print_info "Operation cancelled"
        exit 0
    fi
    
    print_info "Restoring SQLite database..."
    
    # Extract backup
    local temp_dir=$(mktemp -d)
    tar -xzf "$backup_file" -C "$temp_dir"
    
    # Find the database file
    local db_file=$(find "$temp_dir" -name "*.db" -type f | head -n 1)
    
    if [ -z "$db_file" ]; then
        print_error "No database file found in backup"
        rm -rf "$temp_dir"
        exit 1
    fi
    
    # Copy to target location
    cp "$db_file" "$target_path"
    
    # Copy WAL and SHM files if they exist
    if [ -f "${db_file}-wal" ]; then
        cp "${db_file}-wal" "${target_path}-wal"
    fi
    
    if [ -f "${db_file}-shm" ]; then
        cp "${db_file}-shm" "${target_path}-shm"
    fi
    
    # Cleanup
    rm -rf "$temp_dir"
    
    print_info "Restore completed: $target_path"
}

# Function to backup SQL Server database
backup_sqlserver() {
    local connection_string=$1
    local backup_name=$2
    
    if [ -z "$connection_string" ]; then
        print_error "Connection string is required"
        echo "Usage: $0 sqlserver <connection-string> [backup-name]"
        exit 1
    fi
    
    if [ -z "$backup_name" ]; then
        backup_name="sqlserver_backup_${TIMESTAMP}.bak"
    fi
    
    local backup_file="$BACKUP_DIR/$backup_name"
    
    print_info "Backing up SQL Server database..."
    print_info "Backup file: $backup_file"
    
    # Extract database name from connection string
    local db_name=$(echo "$connection_string" | grep -oP 'Database=\K[^;]+')
    
    if [ -z "$db_name" ]; then
        print_error "Could not extract database name from connection string"
        exit 1
    fi
    
    print_info "Database: $db_name"
    
    # Use sqlcmd to backup (requires SQL Server tools)
    if command -v sqlcmd &> /dev/null; then
        sqlcmd -S "$connection_string" -Q "BACKUP DATABASE [$db_name] TO DISK = N'$backup_file' WITH NOFORMAT, NOINIT, NAME = N'$db_name-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10"
        print_info "Backup completed: $backup_file"
    else
        print_error "sqlcmd not found. Please install SQL Server command-line tools"
        print_info "Alternative: Use SQL Server Management Studio or Azure Data Studio to create a backup"
        exit 1
    fi
}

# Function to list backups
list_backups() {
    print_info "Available backups in $BACKUP_DIR:"
    echo ""
    
    if [ ! -d "$BACKUP_DIR" ] || [ -z "$(ls -A "$BACKUP_DIR" 2>/dev/null)" ]; then
        print_warning "No backups found"
        return
    fi
    
    ls -lh "$BACKUP_DIR" | tail -n +2 | while read -r line; do
        echo "  $line"
    done
}

# Function to clean old backups
clean_backups() {
    local days=$1
    
    if [ -z "$days" ]; then
        days=30
    fi
    
    print_info "Cleaning backups older than $days days..."
    
    find "$BACKUP_DIR" -name "*.tar.gz" -type f -mtime +$days -delete
    find "$BACKUP_DIR" -name "*.bak" -type f -mtime +$days -delete
    
    print_info "Cleanup completed"
}

# Function to show help
show_help() {
    cat << EOF
Database Backup Script for SWIFT Message Processor

Usage: $0 <command> [options]

Commands:
    sqlite <db-path> [name]           Backup SQLite database
    restore-sqlite <backup> <target>  Restore SQLite database
    sqlserver <connection> [name]     Backup SQL Server database
    list                              List available backups
    clean [days]                      Clean backups older than N days (default: 30)
    help                              Show this help message

Examples:
    # Backup SQLite database
    $0 sqlite /app/data/messages.db

    # Restore SQLite database
    $0 restore-sqlite backups/sqlite_backup_20231103_120000.db.tar.gz /app/data/messages.db

    # Backup SQL Server database
    $0 sqlserver "Server=localhost;Database=SwiftMessages;User Id=sa;Password=pass"

    # List backups
    $0 list

    # Clean old backups
    $0 clean 30

Backup Location: $BACKUP_DIR

EOF
}

# Main script logic
main() {
    local command=$1
    shift
    
    case "$command" in
        sqlite)
            backup_sqlite "$@"
            ;;
        restore-sqlite)
            restore_sqlite "$@"
            ;;
        sqlserver)
            backup_sqlserver "$@"
            ;;
        list)
            list_backups
            ;;
        clean)
            clean_backups "$@"
            ;;
        help|--help|-h)
            show_help
            ;;
        *)
            print_error "Unknown command: $command"
            show_help
            exit 1
            ;;
    esac
}

# Run main function
if [ $# -eq 0 ]; then
    show_help
    exit 0
fi

main "$@"
