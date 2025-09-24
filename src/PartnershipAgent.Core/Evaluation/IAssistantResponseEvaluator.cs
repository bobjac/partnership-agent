using Microsoft.Extensions.AI.Evaluation;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Evaluation
{
    /// <summary>
    /// Interface for evaluating AI assistant responses using quality metrics.
    /// Supports optional ground truth comparison and logs evaluation metrics using OpenTelemetry.
    /// </summary>
    public interface IAssistantResponseEvaluator
    {
        /// <summary>
        /// Evaluates the response from the AI assistant using a combination of evaluators that analyze response quality.
        /// </summary>
        /// <param name="userPrompt">The original user input to the assistant.</param>
        /// <param name="response">The AI assistant's response to evaluate.</param>
        /// <param name="module">The module name used for logging/tracing context.</param>
        /// <param name="parentActivity">An optional activity used to trace and log evaluation metrics.</param>
        /// <param name="expectedAnswer">An optional expected answer for ground-truth-based evaluation.</param>
        /// <returns>An <see cref="EvaluationResult"/> containing the metrics from the evaluation.</returns>
        Task<EvaluationResult> EvaluateAndLogAsync(string userPrompt, string response, string module, ActivitySource? parentActivity = null, string? expectedAnswer = null);
    }
}