using Microsoft.Data.SqlClient;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Services;

public class AzureSqlChatHistoryService : IChatHistoryService
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public AzureSqlChatHistoryService(ISqlConnectionFactory sqlConnectionFactory)
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

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Id", Guid.NewGuid());
        command.Parameters.AddWithValue("@ThreadId", thread_id);
        command.Parameters.AddWithValue("@Role", chatMessage.Role.ToString());
        command.Parameters.AddWithValue("@Content", (object?)chatMessage.Content ?? DBNull.Value);
        command.Parameters.AddWithValue("@ModelId", (object?)chatMessage.ModelId ?? DBNull.Value);
        command.Parameters.AddWithValue("@InnerContentJson", chatMessage.InnerContent != null ? JsonSerializer.Serialize(chatMessage.InnerContent) : DBNull.Value);
        command.Parameters.AddWithValue("@MetadataJson", chatMessage.Metadata != null ? JsonSerializer.Serialize(chatMessage.Metadata) : DBNull.Value);
        command.Parameters.AddWithValue("@DateInserted", DateTime.UtcNow);

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
        ORDER BY DateInserted ASC"; // earliest to latest

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@ThreadId", thread_id);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            messages.Add(new ChatMessageContent(
                role: ParseAuthorRole(reader.GetString(1)),
                content: reader.IsDBNull(2) ? null : reader.GetString(2),
                modelId: reader.IsDBNull(3) ? null : reader.GetString(3),
                innerContent: reader.IsDBNull(4) ? null : JsonSerializer.Deserialize<JsonElement>(reader.GetString(4)),
                metadata: reader.IsDBNull(5) ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(reader.GetString(5))
            ));
        }

        return new ChatHistory(messages);
    }

    private static AuthorRole ParseAuthorRole(string roleLabel) =>
    roleLabel.ToLowerInvariant() switch
    {
        "developer" => AuthorRole.Developer,
        "system" => AuthorRole.System,
        "assistant" => AuthorRole.Assistant,
        "user" => AuthorRole.User,
        "tool" => AuthorRole.Tool,
        _ => AuthorRole.User // fallback/default
    };
}