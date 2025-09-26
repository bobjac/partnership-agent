using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Services;

public class SqliteChatHistoryService : IChatHistoryService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public SqliteChatHistoryService(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task AddMessageToChatHistoryAsync(Guid thread_id, ChatMessageContent chatMessage)
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
        command.Parameters.Add(CreateParameter(command, "@ThreadId", thread_id.ToString()));
        command.Parameters.Add(CreateParameter(command, "@Role", chatMessage.Role.ToString()));
        command.Parameters.Add(CreateParameter(command, "@Content", chatMessage.Content));
        command.Parameters.Add(CreateParameter(command, "@ModelId", chatMessage.ModelId));
        command.Parameters.Add(CreateParameter(command, "@InnerContentJson", 
            chatMessage.InnerContent != null ? JsonSerializer.Serialize(chatMessage.InnerContent) : null));
        command.Parameters.Add(CreateParameter(command, "@MetadataJson", 
            chatMessage.Metadata != null ? JsonSerializer.Serialize(chatMessage.Metadata) : null));
        command.Parameters.Add(CreateParameter(command, "@DateInserted", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")));

        await command.ExecuteNonQueryAsync();
    }
    
    public async Task<ChatHistory> GetChatHistoryAsync(Guid thread_id)
    {
        await using var connection = _sqlConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        var messages = new List<ChatMessageContent>();

        string query = @"
        SELECT Id, Role, Content, ModelId, InnerContentJson, MetadataJson, DateInserted
        FROM ChatMessages
        WHERE ThreadId = @ThreadId
        ORDER BY DateInserted ASC";

        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(CreateParameter(command, "@ThreadId", thread_id.ToString()));
        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            messages.Add(new ChatMessageContent(
                role: ParseAuthorRole(GetStringValue(reader, 1)),
                content: GetStringValue(reader, 2),
                modelId: GetStringValue(reader, 3),
                innerContent: GetJsonElement(reader, 4),
                metadata: GetMetadata(reader, 5)
            ));
        }

        return new ChatHistory(messages);
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

    private static JsonElement? GetJsonElement(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var json = reader.GetString(ordinal);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static Dictionary<string, object?>? GetMetadata(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var json = reader.GetString(ordinal);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
    }

    private static AuthorRole ParseAuthorRole(string? roleLabel) =>
    roleLabel?.ToLowerInvariant() switch
    {
        "developer" => AuthorRole.Developer,
        "system" => AuthorRole.System,
        "assistant" => AuthorRole.Assistant,
        "user" => AuthorRole.User,
        "tool" => AuthorRole.Tool,
        _ => AuthorRole.User // fallback/default
    };
}