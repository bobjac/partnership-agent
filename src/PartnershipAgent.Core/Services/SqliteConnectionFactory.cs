using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace PartnershipAgent.Core.Services;

public class SqliteConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}