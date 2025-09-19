using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PartnershipAgent.Core.Configuration;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.ConsoleApp.Services;

public class ChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private string _threadId = Guid.NewGuid().ToString();

    public ChatService(HttpClient httpClient, IOptions<WebApiConfiguration> webApiConfig)
    {
        _httpClient = httpClient;
        _apiBaseUrl = webApiConfig.Value.ChatUrl;
    }

    public async Task RunInteractiveChat()
    {
        Console.WriteLine("Partnership Agent Chat Console");
        Console.WriteLine("==============================");
        Console.WriteLine($"Thread ID: {_threadId}");
        Console.WriteLine("Type 'quit' or 'exit' to end the session.\n");

        while (true)
        {
            Console.Write("You: ");
            var userInput = Console.ReadLine();

            if (string.IsNullOrEmpty(userInput) || 
                userInput.Equals("quit", StringComparison.OrdinalIgnoreCase) || 
                userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            await SendChatMessage(userInput);
        }
    }

    private async Task SendChatMessage(string prompt)
    {
        try
        {
            var request = new ChatRequest
            {
                ThreadId = _threadId,
                Prompt = prompt
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Console.WriteLine("Sending request to API...");
            var response = await _httpClient.PostAsync(_apiBaseUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                
                var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (chatResponse != null)
                {
                    Console.WriteLine($"\nA: {chatResponse.Response}");
                    
                    if (chatResponse.ExtractedEntities.Count > 0)
                    {
                        Console.WriteLine($"\nExtracted Entities: {string.Join(", ", chatResponse.ExtractedEntities)}");
                    }

                    if (chatResponse.RelevantDocuments.Count > 0)
                    {
                        Console.WriteLine($"\nRelevant Documents ({chatResponse.RelevantDocuments.Count}):");
                        foreach (var doc in chatResponse.RelevantDocuments)
                        {
                            Console.WriteLine($"- {doc.Title} (Category: {doc.Category}, Score: {doc.Score:F2})");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
            Console.WriteLine("Make sure the Web API is running on http://localhost:5000");
        }

        Console.WriteLine();
    }
}