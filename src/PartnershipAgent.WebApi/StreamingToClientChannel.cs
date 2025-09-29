using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PartnershipAgent.Core.Steps;

namespace PartnershipAgent.WebApi
{
    /// <summary>
    /// Implementation that streams messages directly to the HTTP response using Server-Sent Events format.
    /// Based on ProjectSight.WebAPI.AIAssist.Services.HttpResponseStreamWriter pattern.
    /// </summary>
    public class StreamingToClientChannel : IBidirectionalToClientChannel
    {
        private readonly HttpResponse? _httpResponse;
        private readonly List<string> _responses = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly StringBuilder _fullResponse = new();
        private int _eventId = 0;

        public StreamingToClientChannel(HttpResponse? httpResponse = null)
        {
            _httpResponse = httpResponse;
        }

        public async Task WriteAsync(string eventType, string content)
        {
            // Always collect responses for GetFullResponse()
            _responses.Add($"[{eventType}] {content}");

            // Extract content for full response building (like ProjectSight implementation)
            try
            {
                using var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("content", out var contentElement))
                {
                    // Include a space for readability as in ProjectSight implementation
                    _fullResponse.Append($" {contentElement.GetString()}");
                }
            }
            catch
            {
                // Fallback to appending raw content if it's not JSON
                _fullResponse.Append($" {content}");
            }

            // Stream to HTTP response if available
            if (_httpResponse != null)
            {
                await _writeLock.WaitAsync();
                try
                {
                    var eventData = JsonSerializer.Serialize(new
                    {
                        type = eventType,
                        content = content,
                        timestamp = DateTime.UtcNow
                    });

                    // Proper Server-Sent Events format following ProjectSight pattern
                    await _httpResponse.WriteAsync($"id: {_eventId}\n");
                    await _httpResponse.WriteAsync($"event: {eventType}\n");
                    await _httpResponse.WriteAsync($"data: {eventData}\n\n");
                    
                    // CRITICAL: Flush immediately for real-time streaming
                    await _httpResponse.Body.FlushAsync();
                    
                    _eventId++;
                }
                catch (Exception)
                {
                    // Ignore connection errors (client disconnect, etc.)
                }
                finally
                {
                    _writeLock.Release();
                }
            }
        }

        public string GetFullResponse()
        {
            // Return the concatenated content for final response
            return _fullResponse.ToString().Trim();
        }
    }
}