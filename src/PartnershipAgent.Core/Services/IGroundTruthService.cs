using System.Collections.Generic;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services
{
    /// <summary>
    /// Service for managing ground truth data for evaluation purposes.
    /// </summary>
    public interface IGroundTruthService
    {
        /// <summary>
        /// Retrieves the expected output for a given user prompt if it exists in the ground truth data.
        /// </summary>
        /// <param name="userPrompt">The user input prompt to search for.</param>
        /// <param name="module">Optional module filter to restrict search to specific modules.</param>
        /// <returns>The expected output if found, null otherwise.</returns>
        string? GetExpectedOutput(string userPrompt, string? module = null);

        /// <summary>
        /// Gets all ground truth items for a specific module.
        /// </summary>
        /// <param name="module">The module name to filter by.</param>
        /// <returns>Collection of ground truth items for the specified module.</returns>
        IEnumerable<GroundTruthItem> GetGroundTruthByModule(string module);

        /// <summary>
        /// Gets all ground truth items.
        /// </summary>
        /// <returns>All ground truth items.</returns>
        IEnumerable<GroundTruthItem> GetAllGroundTruth();
    }
}