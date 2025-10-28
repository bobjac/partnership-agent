#!/bin/bash

# Setup Azure SQL table using .NET (avoids sqlcmd OpenSSL issues)

set -e

cd "$(dirname "$0")/.."

echo "==================================================================="
echo "Azure SQL Table Setup (using .NET)"
echo "==================================================================="
echo ""

# Create temporary console app
TEMP_DIR=$(mktemp -d)
cd "$TEMP_DIR"

echo "Creating temporary setup project..."
dotnet new console -n SqlSetup -f net8.0 > /dev/null

cd SqlSetup

# Add required package
dotnet add package Microsoft.Data.SqlClient > /dev/null

# Create the setup program
cat > Program.cs << 'CSHARP'
using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

var connectionString = "Server=tcp:bobjacai.database.windows.net,1433;Initial Catalog=bobjacai;Authentication=Active Directory Default;Encrypt=True;";

Console.WriteLine("Server: bobjacai.database.windows.net");
Console.WriteLine("Database: bobjacai");
Console.WriteLine("Authentication: Active Directory Default");
Console.WriteLine();

var createTableSql = @"
-- Drop table if exists
IF OBJECT_ID('dbo.ChatMessages', 'U') IS NOT NULL
    DROP TABLE dbo.ChatMessages;

-- Create ChatMessages table
CREATE TABLE dbo.ChatMessages (
    Id NVARCHAR(450) PRIMARY KEY,
    ThreadId NVARCHAR(450) NOT NULL,
    Role NVARCHAR(50) NOT NULL,
    Content NVARCHAR(MAX),
    ModelId NVARCHAR(255),
    InnerContentJson NVARCHAR(MAX),
    MetadataJson NVARCHAR(MAX),
    DateInserted DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Create indexes
CREATE NONCLUSTERED INDEX IX_ChatMessages_ThreadId_DateInserted
ON dbo.ChatMessages (ThreadId, DateInserted);

CREATE NONCLUSTERED INDEX IX_ChatMessages_DateInserted
ON dbo.ChatMessages (DateInserted);
";

try
{
    Console.WriteLine("Connecting to Azure SQL...");
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    Console.WriteLine("✓ Connected successfully");
    Console.WriteLine();
    Console.WriteLine("Creating ChatMessages table...");

    await using var command = new SqlCommand(createTableSql, connection);
    command.CommandTimeout = 60;
    await command.ExecuteNonQueryAsync();

    Console.WriteLine();
    Console.WriteLine("✓ ChatMessages table created successfully!");
    Console.WriteLine();
    Console.WriteLine("Table structure:");
    Console.WriteLine("  - Id (Primary Key)");
    Console.WriteLine("  - ThreadId (indexed)");
    Console.WriteLine("  - Role, Content, ModelId");
    Console.WriteLine("  - InnerContentJson, MetadataJson");
    Console.WriteLine("  - DateInserted (indexed, auto-filled)");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine();
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner error: {ex.InnerException.Message}");
    }
    Environment.Exit(1);
}
CSHARP

echo "Running setup..."
echo ""

dotnet run

EXIT_CODE=$?

# Cleanup
cd - > /dev/null
rm -rf "$TEMP_DIR"

if [ $EXIT_CODE -eq 0 ]; then
    echo ""
    echo "✓ Setup complete!"
    echo ""
    echo "You can now test with Azure SQL:"
    echo "  ./setup/test-with-user-secrets.sh"
    echo ""
else
    echo ""
    echo "Setup failed. Alternative approach:"
    echo "  1. Go to Azure Portal: https://portal.azure.com"
    echo "  2. Navigate to SQL Database: bobjacai"
    echo "  3. Open Query Editor (left menu)"
    echo "  4. Login with Azure AD"
    echo "  5. Run the SQL from: setup/azure-sql-setup.sql"
    echo ""
    exit 1
fi
