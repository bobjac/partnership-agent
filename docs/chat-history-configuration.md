# Chat History Configuration Guide

This guide explains how to configure and use the different chat history providers available in the Partnership Agent.

## Overview

The Partnership Agent supports three chat history storage options:

- **InMemory** - Default, no persistence, fastest startup
- **SQLite** - Local persistence with Docker container
- **Azure SQL** - Cloud persistence for production scenarios

## Quick Setup

### Using Setup Scripts (Recommended)

```bash
# InMemory (default)
./setup/setup.sh

# SQLite with persistence
./setup/setup.sh sqlite

# Azure SQL Database
./setup/setup.sh azuresql
```

### Manual Configuration

```bash
cd setup
dotnet run --project CrossPlatformSetup.csproj [inmemory|sqlite|azuresql]
```

## Provider Details

### 🧠 InMemory Provider

**Best for**: Development, testing, quick demos

**Configuration**: No additional configuration required

**Pros**:
- Zero setup time
- Fastest performance
- No external dependencies

**Cons**:
- No persistence - data lost on restart
- Not suitable for production

**Usage**:
```bash
./setup/setup.sh inmemory
# or simply
./setup/setup.sh
```

### 🗄️ SQLite Provider

**Best for**: Local development with persistence

**Configuration**: Automatic Docker container setup

**Pros**:
- Full persistence
- Local file-based storage
- No cloud dependencies
- Automatic container management

**Cons**:
- Requires Docker
- Single-user scenarios only

**Container Details**:
- Container name: `partnership-agent-sqlite`
- Database file: `/data/partnership-agent.db`
- Volume: `partnership-agent-sqlite-data`

**Usage**:
```bash
./setup/setup.sh sqlite
```

**Manual Container Management**:
```bash
# Check container status
docker ps -a --filter name=partnership-agent-sqlite

# View container logs
docker logs partnership-agent-sqlite

# Stop container
docker stop partnership-agent-sqlite

# Remove container and volume
docker rm partnership-agent-sqlite
docker volume rm partnership-agent-sqlite-data
```

### ☁️ Azure SQL Provider

**Best for**: Production deployments, multi-user scenarios

**Configuration**: Requires Azure SQL Database setup

**Pros**:
- Enterprise-grade reliability
- Automatic backups
- Multi-user support
- Scalable performance

**Cons**:
- Requires Azure subscription
- Network dependency
- More complex setup

**Prerequisites**:
1. Azure SQL Database provisioned
2. Connection string configured
3. Database tables created

**Setup Steps**:

1. **Configure connection string**:
   ```bash
   cd src/PartnershipAgent.WebApi
   dotnet user-secrets set "AzureSQL:ConnectionString" "Server=tcp:your-server.database.windows.net,1433;Initial Catalog=your-database;Persist Security Info=False;User ID=your-username;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
   ```

2. **Run setup**:
   ```bash
   ./setup/setup.sh azuresql
   ```

3. **Verify database table creation**:
   The setup automatically creates the `ChatMessages` table with the required schema.

## Configuration Methods

### 1. Command-Line Arguments (Recommended)

The setup scripts accept provider arguments:

```bash
./setup/setup.sh [inmemory|sqlite|azuresql]
```

### 2. User Secrets

Configure the provider and connection strings using .NET user secrets:

```bash
cd src/PartnershipAgent.WebApi

# Set provider
dotnet user-secrets set "ChatHistory:Provider" "sqlite"

# Set SQLite connection (if using SQLite)
dotnet user-secrets set "SQLite:ConnectionString" "Data Source=/data/partnership-agent.db;Cache=Shared"

# Set Azure SQL connection (if using Azure SQL)
dotnet user-secrets set "AzureSQL:ConnectionString" "your-connection-string"
```

### 3. Configuration Files

Modify `appsettings.json` or `appsettings.Development.json`:

```json
{
  "ChatHistory": {
    "Provider": "sqlite"
  },
  "SQLite": {
    "ConnectionString": "Data Source=/data/partnership-agent.db;Cache=Shared"
  },
  "AzureSQL": {
    "ConnectionString": "your-connection-string"
  }
}
```

## Database Schema

All providers use the same chat message schema:

```sql
CREATE TABLE ChatMessages (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ThreadId NVARCHAR(100) NOT NULL,
    Role NVARCHAR(50) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    UserId NVARCHAR(100),
    TenantId NVARCHAR(100)
);

CREATE INDEX IX_ChatMessages_ThreadId ON ChatMessages(ThreadId);
CREATE INDEX IX_ChatMessages_UserId ON ChatMessages(UserId);
CREATE INDEX IX_ChatMessages_TenantId ON ChatMessages(TenantId);
CREATE INDEX IX_ChatMessages_CreatedAt ON ChatMessages(CreatedAt);
```

## Switching Providers

### During Development

Stop the application and run setup with the new provider:

```bash
# Stop current application (Ctrl+C)

# Switch to SQLite
./setup/setup.sh sqlite

# Or switch to Azure SQL
./setup/setup.sh azuresql
```

### Runtime Configuration

Update the configuration and restart the application:

```bash
# Update user secrets
dotnet user-secrets set "ChatHistory:Provider" "azuresql"

# Restart application
cd src/PartnershipAgent.WebApi
dotnet run
```

## Troubleshooting

### SQLite Issues

**Container not starting**:
```bash
# Check Docker status
docker ps -a --filter name=partnership-agent-sqlite

# View logs
docker logs partnership-agent-sqlite

# Recreate container
docker rm partnership-agent-sqlite
docker volume rm partnership-agent-sqlite-data
./setup/setup.sh sqlite
```

**Database file permissions**:
- Ensure Docker has access to create volumes
- Check Docker Desktop settings for file sharing

### Azure SQL Issues

**Connection failures**:
- Verify connection string format
- Check firewall rules in Azure portal
- Ensure database exists and is accessible

**Authentication errors**:
- Verify username/password in connection string
- Check Azure AD authentication if applicable
- Ensure user has appropriate database permissions

**Table creation errors**:
- Verify user has CREATE TABLE permissions
- Check if tables already exist
- Run table creation script manually if needed

### General Issues

**Provider not recognized**:
- Check spelling: `inmemory`, `sqlite`, `azuresql`
- Verify configuration is properly set
- Check application logs for configuration errors

**Performance issues**:
- InMemory: Fastest, no persistence
- SQLite: Good performance, local storage
- Azure SQL: Network dependent, check latency

## Testing Chat History

### Verify Persistence

1. **Start application with chosen provider**
2. **Send a chat message via API**:
   ```bash
   curl -X POST "http://localhost:5001/api/chat" \
     -H "Content-Type: application/json" \
     -d '{
       "threadId": "test-persistence",
       "prompt": "Hello, test message"
     }'
   ```
3. **Restart application**
4. **Send another message with same threadId**
5. **Verify conversation history is maintained**

### Load Testing

For production scenarios with Azure SQL:

```bash
# Multiple concurrent requests
for i in {1..10}; do
  curl -X POST "http://localhost:5001/api/chat" \
    -H "Content-Type: application/json" \
    -d "{
      \"threadId\": \"load-test-$i\",
      \"prompt\": \"Load test message $i\"
    }" &
done
wait
```

## Best Practices

### Development
- Use **InMemory** for quick testing and development
- Use **SQLite** when you need to test persistence locally
- Keep SQLite containers running during development sessions

### Production
- Use **Azure SQL** for production deployments
- Configure proper connection pooling
- Set up monitoring and alerts
- Regular database backups
- Consider read replicas for high-load scenarios

### Security
- Never commit connection strings to source control
- Use Azure Key Vault for production secrets
- Implement proper authentication and authorization
- Regular security updates for database engines

## Performance Considerations

| Provider | Startup Time | Request Latency | Persistence | Concurrency |
|----------|--------------|-----------------|-------------|-------------|
| InMemory | Fastest      | Lowest          | None        | Single Process |
| SQLite   | Fast         | Low             | Full        | Single User |
| Azure SQL| Moderate     | Variable        | Full        | Multi-User |

Choose the provider that best matches your performance and persistence requirements.