using System.Threading.Tasks;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Agents;

/// <summary>
/// Interface for the ScopingAgent that determines whether a user request is in scope
/// for the partnership agent system.
/// </summary>
public interface IScopingAgent
{
    /// <summary>
    /// The name of the agent.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Brief description of the agent's purpose.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Evaluates whether a user prompt is in scope for the partnership agent.
    /// </summary>
    /// <param name="prompt">The user's input prompt</param>
    /// <returns>A ScopingAgentResponse indicating whether the request is in scope</returns>
    Task<ScopingAgentResponse> EvaluateScopeAsync(string prompt);
}
