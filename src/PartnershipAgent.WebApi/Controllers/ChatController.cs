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
    private readonly SimpleChatProcessService _chatProcessService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(SimpleChatProcessService chatProcessService, ILogger<ChatController> logger)
    {
        _chatProcessService = chatProcessService;
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
            var response = await _chatProcessService.ProcessChatAsync(request);
            return Ok(response);
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