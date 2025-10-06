using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Steps;

namespace PartnershipAgent.Core.Services
{
    /// <summary>
    /// Implementation that streams messages to a Stream using Server-Sent Events format.
    /// Refactored to be stream-based and reusable across different contexts.
    /// </summary>
    public class StreamingToClientChannel : IBidirectionalToClientChannel, IDisposable
    {
        private readonly Stream? _outputStream;
        private readonly List<string> _responses = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly StringBuilder _fullResponse = new();
        private readonly ILogger<StreamingToClientChannel>? _logger;
        private int _eventId = 0;
        private bool _disposed = false;

        public StreamingToClientChannel(Stream? outputStream = null, ILogger<StreamingToClientChannel>? logger = null)
        {
            _outputStream = outputStream;
            _logger = logger;
            _logger?.LogDebug("StreamingToClientChannel created with output stream: {HasStream}", outputStream != null);
        }

        public async Task WriteAsync(string eventType, string content)
        {
            if (_disposed)
            {
                _logger?.LogWarning("Attempted to write to disposed StreamingToClientChannel");
                return;
            }

            _logger?.LogDebug("Writing event: {EventType}, Content: {Content}", eventType, content);

            // Always collect responses for GetFullResponse()
            _responses.Add($"[{eventType}] {content}");

            // Extract content for full response building
            try
            {
                using var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("content", out var contentElement))
                {
                    // Include a space for readability
                    _fullResponse.Append($" {contentElement.GetString()}");
                }
            }
            catch
            {
                // Fallback to appending raw content if it's not JSON
                _fullResponse.Append($" {content}");
            }

            // Stream to output stream if available
            if (_outputStream != null && _outputStream.CanWrite)
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

                    // Proper Server-Sent Events format (match original implementation)
                    await WriteLineAsync($"id: {_eventId}");
                    await WriteLineAsync($"event: {eventType}");
                    await WriteLineAsync($"data: {eventData}");
                    await WriteLineAsync(""); // Empty line to end the event
                    
                    // Flush immediately for real-time streaming
                    await _outputStream.FlushAsync();
                    
                    _eventId++;
                }
                catch (Exception ex)
                {
                    // Log stream errors for debugging but don't throw
                    _logger?.LogWarning(ex, "Failed to write to output stream");
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

        private async Task WriteLineAsync(string line)
        {
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            await _outputStream!.WriteAsync(bytes, 0, bytes.Length);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _writeLock?.Dispose();
                _disposed = true;
            }
        }
    }
}