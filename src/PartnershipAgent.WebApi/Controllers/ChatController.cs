using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;

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

        Response.Headers["Content-Type"] = "text/plain; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        try
        {
            // Start with immediate status update
            var statusMessage = JsonSerializer.Serialize(new { 
                type = "status", 
                message = "Processing your request...", 
                timestamp = DateTime.UtcNow 
            });
            await HttpResponseWritingExtensions.WriteAsync(Response, statusMessage + "\n");
            await Response.Body.FlushAsync();

            // Process the request
            var chatResponse = await _stepOrchestrationService.ProcessRequestAsync(request);
            
            // Return final response
            var responseMessage = JsonSerializer.Serialize(new { 
                type = "response", 
                content = chatResponse.Response,
                timestamp = DateTime.UtcNow 
            });
            await HttpResponseWritingExtensions.WriteAsync(Response, responseMessage + "\n");
            await Response.Body.FlushAsync();

            var completeMessage = JsonSerializer.Serialize(new { 
                type = "complete", 
                timestamp = DateTime.UtcNow 
            });
            await HttpResponseWritingExtensions.WriteAsync(Response, completeMessage + "\n");
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
            await HttpResponseWritingExtensions.WriteAsync(Response, errorMessage + "\n");
            await Response.Body.FlushAsync();
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}