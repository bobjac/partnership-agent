using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Services;

/// <summary>
/// Azure SQL implementation of chat history service using Agent Framework types.
/// </summary>
public class AzureSqlChatHistoryService : IChatHistoryService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public AzureSqlChatHistoryService(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task AddMessageToChatHistoryAsync(Guid threadId, ChatMessage message)
    {
        await using var connection = _sqlConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        string query = @"
            INSERT INTO ChatMessages
            (Id, ThreadId, Role, Content, ModelId, InnerContentJson, MetadataJson, DateInserted)
            VALUES
            (@Id, @ThreadId, @Role, @Content, @ModelId, @InnerContentJson, @MetadataJson, @DateInserted)";

        await using var command = connection.CreateCommand();
        command.CommandText = query;

        // Use DbParameter for cross-database compatibility
        command.Parameters.Add(CreateParameter(command, "@Id", Guid.NewGuid().ToString()));
        command.Parameters.Add(CreateParameter(command, "@ThreadId", threadId.ToString()));
        command.Parameters.Add(CreateParameter(command, "@Role", message.Role.Value));
        command.Parameters.Add(CreateParameter(command, "@Content", message.Text));
        // ModelId is not a property of ChatMessage - retrieve from AdditionalProperties if available
        var modelId = message.AdditionalProperties?.TryGetValue("ModelId", out var modelIdValue) == true
            ? modelIdValue?.ToString()
            : null;
        command.Parameters.Add(CreateParameter(command, "@ModelId", modelId));
        command.Parameters.Add(CreateParameter(command, "@InnerContentJson",
            message.Contents?.Count > 0 ? JsonSerializer.Serialize(message.Contents) : null));
        command.Parameters.Add(CreateParameter(command, "@MetadataJson",
            message.AdditionalProperties?.Count > 0 ? JsonSerializer.Serialize(message.AdditionalProperties) : null));
        command.Parameters.Add(CreateParameter(command, "@DateInserted", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<IList<ChatMessage>> GetChatHistoryAsync(Guid threadId)
    {
        await using var connection = _sqlConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        var messages = new List<ChatMessage>();

        string query = @"
        SELECT Id, Role, Content, ModelId, InnerContentJson, MetadataJson, DateInserted
        FROM ChatMessages
        WHERE ThreadId = @ThreadId
        ORDER BY DateInserted ASC"; // earliest to latest

        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(CreateParameter(command, "@ThreadId", threadId.ToString()));
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var role = ParseChatRole(GetStringValue(reader, 1));
            var content = GetStringValue(reader, 2) ?? string.Empty;
            var modelId = GetStringValue(reader, 3);
            var additionalProperties = GetMetadata(reader, 5);

            var message = new ChatMessage(role, content);
            // ModelId is not a property of ChatMessage - store in AdditionalProperties if available
            if (!string.IsNullOrEmpty(modelId))
            {
                message.AdditionalProperties["ModelId"] = modelId;
            }
            if (additionalProperties != null)
            {
                foreach (var kvp in additionalProperties)
                {
                    message.AdditionalProperties[kvp.Key] = kvp.Value;
                }
            }

            messages.Add(message);
        }

        return messages;
    }

    private static DbParameter CreateParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    private static string? GetStringValue(DbDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static Dictionary<string, object?>? GetMetadata(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var json = reader.GetString(ordinal);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
    }

    private static ChatRole ParseChatRole(string? roleLabel) =>
        roleLabel?.ToLowerInvariant() switch
        {
            "system" => ChatRole.System,
            "assistant" => ChatRole.Assistant,
            "user" => ChatRole.User,
            "tool" => ChatRole.Tool,
            _ => ChatRole.User // fallback/default
        };
}