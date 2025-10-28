#!/bin/bash

# Setup ChatMessages table in Azure SQL using sqlcmd

set -e

echo "==================================================================="
echo "Azure SQL Table Setup"
echo "==================================================================="
echo ""

SERVER="bobjacai.database.windows.net"
DATABASE="bobjacai"
SQL_FILE="$(dirname "$0")/azure-sql-setup.sql"

echo "Server: $SERVER"
echo "Database: $DATABASE"
echo ""
echo "This script will create the ChatMessages table using Azure AD authentication."
echo ""

# Check if sqlcmd is installed
if ! command -v sqlcmd &> /dev/null; then
    echo "❌ sqlcmd is not installed."
    echo ""
    echo "Please install it:"
    echo "  macOS: brew install sqlcmd"
    echo "  Or download from: https://learn.microsoft.com/en-us/sql/tools/sqlcmd/sqlcmd-utility"
    echo ""
    echo "Alternative: Run the SQL script manually in Azure Data Studio or Azure Portal Query Editor:"
    echo "  $SQL_FILE"
    exit 1
fi

echo "Running SQL setup script..."
echo ""

# Execute the SQL script using Azure AD authentication
sqlcmd -S "$SERVER" -d "$DATABASE" -G -i "$SQL_FILE"

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ ChatMessages table created successfully!"
    echo ""
    echo "You can now test with Azure SQL:"
    echo "  ./setup/test-with-user-secrets.sh"
else
    echo ""
    echo "❌ Failed to create table."
    echo ""
    echo "You can:"
    echo "  1. Run the SQL script manually in Azure Portal"
    echo "  2. Use Azure Data Studio"
    echo "  3. Or test with InMemory chat history instead"
fi
