namespace PartnershipAgent.Core.Models
{
    /// <summary>
    /// Represents a ground truth data item for evaluation purposes.
    /// </summary>
    public class GroundTruthItem
    {
        /// <summary>
        /// The user input prompt.
        /// </summary>
        public required string UserPrompt { get; set; }

        /// <summary>
        /// The expected output/response for the given prompt.
        /// </summary>
        public required string ExpectedOutput { get; set; }

        /// <summary>
        /// The category of the prompt (e.g., General, Financial, Legal).
        /// </summary>
        public required string Category { get; set; }

        /// <summary>
        /// The module that should handle this prompt (e.g., FAQAgent, EntityResolutionAgent).
        /// </summary>
        public required string Module { get; set; }
    }
}