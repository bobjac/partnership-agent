using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

/// <summary>
/// Simple utility to create the ChatMessages table in Azure SQL.
/// Run with: dotnet script setup/SetupAzureSqlTable.cs
/// </summary>
public class SetupAzureSqlTable
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("===================================================================");
        Console.WriteLine("Azure SQL Table Setup (.NET)");
        Console.WriteLine("===================================================================");
        Console.WriteLine();

        var connectionString = "Server=tcp:bobjacai.database.windows.net,1433;Initial Catalog=bobjacai;Authentication=Active Directory Default;Encrypt=True;";

        Console.WriteLine("Server: bobjacai.database.windows.net");
        Console.WriteLine("Database: bobjacai");
        Console.WriteLine("Authentication: Active Directory Default");
        Console.WriteLine();

        var createTableSql = @"
-- Drop table if exists (be careful in production!)
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

-- Create indexes for better query performance
CREATE NONCLUSTERED INDEX IX_ChatMessages_ThreadId_DateInserted
ON dbo.ChatMessages (ThreadId, DateInserted);

CREATE NONCLUSTERED INDEX IX_ChatMessages_DateInserted
ON dbo.ChatMessages (DateInserted);

-- Verify table creation
SELECT 'ChatMessages table created successfully' AS Status;
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

            var result = await command.ExecuteScalarAsync();

            Console.WriteLine();
            Console.WriteLine($"✓ {result}");
            Console.WriteLine();
            Console.WriteLine("Table structure:");
            Console.WriteLine("  - Id (Primary Key)");
            Console.WriteLine("  - ThreadId (indexed)");
            Console.WriteLine("  - Role");
            Console.WriteLine("  - Content");
            Console.WriteLine("  - ModelId");
            Console.WriteLine("  - InnerContentJson");
            Console.WriteLine("  - MetadataJson");
            Console.WriteLine("  - DateInserted (indexed)");
            Console.WriteLine();
            Console.WriteLine("✓ Setup complete! You can now test with Azure SQL:");
            Console.WriteLine("  ./setup/test-with-user-secrets.sh");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Alternative: Use Azure Portal Query Editor");
            Console.WriteLine("  1. Go to: https://portal.azure.com");
            Console.WriteLine("  2. Navigate to your SQL database: bobjacai");
            Console.WriteLine("  3. Click 'Query editor' in left menu");
            Console.WriteLine("  4. Login with Azure AD");
            Console.WriteLine("  5. Copy/paste the SQL from: setup/azure-sql-setup.sql");
            Console.WriteLine();
            Environment.Exit(1);
        }
    }
}
