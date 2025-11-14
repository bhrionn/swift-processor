#!/bin/bash

# Database Migration Script for SWIFT Message Processor
# This script handles database migrations for both development and production environments

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INFRASTRUCTURE_PROJECT="$PROJECT_ROOT/src/SwiftMessageProcessor.Infrastructure"
STARTUP_PROJECT="$PROJECT_ROOT/src/SwiftMessageProcessor.Api"

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

# Function to check if dotnet-ef is installed
check_ef_tools() {
    if ! dotnet ef --version &> /dev/null; then
        print_error "dotnet-ef tool is not installed"
        print_info "Installing dotnet-ef..."
        dotnet tool install --global dotnet-ef
    else
        print_info "dotnet-ef version: $(dotnet ef --version)"
    fi
}

# Function to list migrations
list_migrations() {
    print_info "Listing migrations..."
    dotnet ef migrations list \
        --project "$INFRASTRUCTURE_PROJECT" \
        --startup-project "$STARTUP_PROJECT"
}

# Function to add a new migration
add_migration() {
    local migration_name=$1
    
    if [ -z "$migration_name" ]; then
        print_error "Migration name is required"
        echo "Usage: $0 add <MigrationName>"
        exit 1
    fi
    
    print_info "Adding migration: $migration_name"
    dotnet ef migrations add "$migration_name" \
        --project "$INFRASTRUCTURE_PROJECT" \
        --startup-project "$STARTUP_PROJECT" \
        --output-dir Migrations
    
    print_info "Migration added successfully"
}

# Function to update database
update_database() {
    local target_migration=$1
    
    print_info "Updating database..."
    
    if [ -z "$target_migration" ]; then
        dotnet ef database update \
            --project "$INFRASTRUCTURE_PROJECT" \
            --startup-project "$STARTUP_PROJECT"
    else
        print_info "Updating to migration: $target_migration"
        dotnet ef database update "$target_migration" \
            --project "$INFRASTRUCTURE_PROJECT" \
            --startup-project "$STARTUP_PROJECT"
    fi
    
    print_info "Database updated successfully"
}

# Function to rollback migration
rollback_migration() {
    local target_migration=$1
    
    if [ -z "$target_migration" ]; then
        print_error "Target migration is required for rollback"
        echo "Usage: $0 rollback <MigrationName>"
        exit 1
    fi
    
    print_warning "Rolling back to migration: $target_migration"
    dotnet ef database update "$target_migration" \
        --project "$INFRASTRUCTURE_PROJECT" \
        --startup-project "$STARTUP_PROJECT"
    
    print_info "Rollback completed"
}

# Function to remove last migration
remove_migration() {
    print_warning "Removing last migration..."
    dotnet ef migrations remove \
        --project "$INFRASTRUCTURE_PROJECT" \
        --startup-project "$STARTUP_PROJECT" \
        --force
    
    print_info "Migration removed"
}

# Function to generate SQL script
generate_script() {
    local from_migration=$1
    local to_migration=$2
    local output_file=$3
    
    if [ -z "$output_file" ]; then
        output_file="$PROJECT_ROOT/scripts/migration-script.sql"
    fi
    
    print_info "Generating SQL script..."
    
    if [ -z "$from_migration" ]; then
        dotnet ef migrations script \
            --project "$INFRASTRUCTURE_PROJECT" \
            --startup-project "$STARTUP_PROJECT" \
            --output "$output_file" \
            --idempotent
    else
        dotnet ef migrations script "$from_migration" "$to_migration" \
            --project "$INFRASTRUCTURE_PROJECT" \
            --startup-project "$STARTUP_PROJECT" \
            --output "$output_file" \
            --idempotent
    fi
    
    print_info "SQL script generated: $output_file"
}

# Function to drop database
drop_database() {
    print_warning "This will drop the database. Are you sure? (yes/no)"
    read -r confirmation
    
    if [ "$confirmation" = "yes" ]; then
        print_warning "Dropping database..."
        dotnet ef database drop \
            --project "$INFRASTRUCTURE_PROJECT" \
            --startup-project "$STARTUP_PROJECT" \
            --force
        print_info "Database dropped"
    else
        print_info "Operation cancelled"
    fi
}

# Function to show help
show_help() {
    cat << EOF
Database Migration Script for SWIFT Message Processor

Usage: $0 <command> [options]

Commands:
    list                    List all migrations
    add <name>             Add a new migration
    update [migration]     Update database to latest or specified migration
    rollback <migration>   Rollback to specified migration
    remove                 Remove the last migration
    script [from] [to]     Generate SQL script for migrations
    drop                   Drop the database (requires confirmation)
    help                   Show this help message

Examples:
    $0 list
    $0 add AddNewField
    $0 update
    $0 update InitialCreate
    $0 rollback InitialCreate
    $0 remove
    $0 script
    $0 script InitialCreate AddNewField
    $0 drop

EOF
}

# Main script logic
main() {
    check_ef_tools
    
    local command=$1
    shift
    
    case "$command" in
        list)
            list_migrations
            ;;
        add)
            add_migration "$@"
            ;;
        update)
            update_database "$@"
            ;;
        rollback)
            rollback_migration "$@"
            ;;
        remove)
            remove_migration
            ;;
        script)
            generate_script "$@"
            ;;
        drop)
            drop_database
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
main "$@"
