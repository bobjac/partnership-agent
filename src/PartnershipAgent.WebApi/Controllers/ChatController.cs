using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;
using PartnershipAgent.Core.Steps;

namespace PartnershipAgent.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly StepOrchestrationService _stepOrchestrationService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(StepOrchestrationService stepOrchestrationService, ILogger<ChatController> logger)
    {
        _stepOrchestrationService = stepOrchestrationService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> PostAsync([FromBody] ChatRequest request)
    {
        _logger.LogInformation("Received chat request for thread: {ThreadId}", request.ThreadId);

        if (string.IsNullOrEmpty(request.ThreadId) || string.IsNullOrEmpty(request.Prompt))
        {
            return BadRequest("ThreadId and Prompt are required.");
        }

        request.UserId = "mock-user-123";
        request.TenantId = "tenant-123";

        _logger.LogInformation("Processing request for User: {UserId}, Tenant: {TenantId}", 
            request.UserId, request.TenantId);

        try
        {
            // Process the query using the step orchestration service (Semantic Kernel process framework)
            var chatResponse = await _stepOrchestrationService.ProcessRequestAsync(request);
            return Ok(chatResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }

    [HttpPost("stream")]
    public async Task PostStreamAsync([FromBody] ChatRequest request)
    {
        _logger.LogInformation("Received streaming chat request for thread: {ThreadId}", request.ThreadId);

        if (string.IsNullOrEmpty(request.ThreadId) || string.IsNullOrEmpty(request.Prompt))
        {
            await HttpResponseWritingExtensions.WriteAsync(Response, JsonSerializer.Serialize(new { error = "ThreadId and Prompt are required." }));
            return;
        }

        request.UserId = "mock-user-123";
        request.TenantId = "tenant-123";

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["Access-Control-Allow-Origin"] = "*";

        try
        {
            // Create a streaming channel that writes directly to this HTTP response body stream
            var streamingChannel = new StreamingToClientChannel(Response.Body);
            _logger.LogInformation("CHATCONTROLLER: Created StreamingToClientChannel for thread {ThreadId}", request.ThreadId);
            
            // Start with immediate status update
            var statusMessage = JsonSerializer.Serialize(new { 
                type = "status", 
                message = "Processing your request...", 
                timestamp = DateTime.UtcNow 
            });
            await Response.WriteAsync($"data: {statusMessage}\n\n");
            await Response.Body.FlushAsync();

            // Process the request with streaming enabled
            _logger.LogInformation("CHATCONTROLLER: Calling ProcessRequestAsync with streaming channel for thread {ThreadId}", request.ThreadId);
            var chatResponse = await _stepOrchestrationService.ProcessRequestAsync(request, streamingChannel);
            
            // Return final response
            var responseMessage = JsonSerializer.Serialize(new { 
                type = "response", 
                content = chatResponse.Response,
                timestamp = DateTime.UtcNow 
            });
            await HttpResponseWritingExtensions.WriteAsync(Response, $"data: {responseMessage}\n\n");
            await Response.Body.FlushAsync();

            var completeMessage = JsonSerializer.Serialize(new { 
                type = "complete", 
                timestamp = DateTime.UtcNow 
            });
            await HttpResponseWritingExtensions.WriteAsync(Response, $"data: {completeMessage}\n\n");
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing streaming chat request");
            var errorMessage = JsonSerializer.Serialize(new { 
                type = "error", 
                message = "An error occurred while processing your request.",
                timestamp = DateTime.UtcNow 
            });
            await HttpResponseWritingExtensions.WriteAsync(Response, $"data: {errorMessage}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}