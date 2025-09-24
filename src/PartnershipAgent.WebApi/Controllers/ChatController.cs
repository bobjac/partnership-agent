using System;
using System.Threading.Tasks;
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

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}