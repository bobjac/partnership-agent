using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.SemanticKernel;
using PartnershipAgent.Core.Services;

namespace PartnershipAgent.Core.Evaluation
{
    /// <summary>
    /// Evaluates AI assistant responses against various quality metrics.
    /// Supports optional ground truth comparison and logs evaluation metrics using OpenTelemetry for observability.
    /// </summary>
    public class AssistantResponseEvaluator : IAssistantResponseEvaluator
    {
        private readonly Kernel _kernel;
        private readonly IGroundTruthService _groundTruthService;

        public AssistantResponseEvaluator(Kernel kernel, IGroundTruthService groundTruthService)
        {
            _kernel = kernel;
            _groundTruthService = groundTruthService;
        }

        /// <summary>
        /// Evaluates the response from the AI assistant using a combination of evaluators that analyze response quality.
        /// Evaluation occurs regardless of whether an expected answer is provided.
        /// 
        /// If an expected answer is provided (either explicitly or retrieved from the ground truth repository),
        /// additional evaluators such as Equivalence and Groundedness are used.
        /// 
        /// If retrieved documents are provided, RetrievalEvaluator is used to assess the quality of document retrieval in RAG scenarios.
        /// Base evaluators (Coherence, Fluency) are always applied to assess response quality.
        ///
        /// Results are optionally logged to telemetry if a parent activity is provided.
        /// </summary>
        /// <param name="userPrompt">The original user input to the assistant.</param>
        /// <param name="response">The AI assistant's response to evaluate.</param>
        /// <param name="module">The module name used for logging/tracing context.</param>
        /// <param name="parentActivity">An optional activity used to trace and log evaluation metrics.</param>
        /// <param name="expectedAnswer">An optional expected answer for ground-truth-based evaluation.</param>
        /// <param name="retrievedDocuments">An optional list of retrieved documents for retrieval evaluation in RAG scenarios.</param>
        /// <returns>An <see cref="EvaluationResult"/> containing the metrics from the evaluation.</returns>
        public async Task<EvaluationResult> EvaluateAndLogAsync(string userPrompt, string response, string module, ActivitySource? parentActivity = null, string? expectedAnswer = null, IReadOnlyList<Models.DocumentResult>? retrievedDocuments = null)
        {
            // Prepare chat history for evaluation
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, userPrompt)
            };

            var chatResponse = new ChatResponse
            {
                Messages = [new ChatMessage(ChatRole.Assistant, response)]
            };

            // Base evaluators that work without ground truth
            var evaluators = new List<IEvaluator>
            {
                new CoherenceEvaluator(),
                new FluencyEvaluator()
                // NOTE: Additional evaluators like RelevanceEvaluator, CompletenessEvaluator, and TruthEvaluator
                // are not available in Microsoft.Extensions.AI.Evaluation.Quality v9.4.0-preview.1.25207.5
                // These will be available in future package versions compatible with current SemanticKernel
            };

            var contexts = new List<EvaluationContext>();

            // Try to get ground truth from service if not explicitly provided
            if (string.IsNullOrWhiteSpace(expectedAnswer))
            {
                expectedAnswer = _groundTruthService.GetExpectedOutput(userPrompt, module);
            }

            // Add ground-truth based evaluators if expected answer is available
            if (!string.IsNullOrWhiteSpace(expectedAnswer))
            {
                evaluators.AddRange([
                    new EquivalenceEvaluator(),
                    new GroundednessEvaluator()
                ]);

                contexts.AddRange([
                    new EquivalenceEvaluatorContext(expectedAnswer),
                    new GroundednessEvaluatorContext(expectedAnswer)
                ]);
            }

            // Add retrieval evaluator if retrieved documents are available (for RAG scenarios)
            // NOTE: RetrievalEvaluator is not available in Microsoft.Extensions.AI.Evaluation.Quality v9.4.0-preview.1.25207.5
            // It requires a newer version that is incompatible with current Microsoft.SemanticKernel v1.48.0
            // TODO: Enable RetrievalEvaluator when upgrading to compatible package versions
            /*
            if (retrievedDocuments != null && retrievedDocuments.Count > 0)
            {
                evaluators.Add(new RetrievalEvaluator());

                // Convert DocumentResult to the format expected by RetrievalEvaluator
                var retrievedTexts = retrievedDocuments.Select(doc => doc.Content).ToList();
                contexts.Add(new RetrievalEvaluatorContext(retrievedTexts));
            }
            */

            // Create a basic ChatConfiguration using the kernel's chat completion service
            var chatClient = _kernel.GetRequiredService<IChatClient>();
            var chatConfiguration = new ChatConfiguration(chatClient);

            // Evaluate response using composite evaluator
            var compositeEvaluator = new CompositeEvaluator(evaluators);
            var evaluationResult = await compositeEvaluator.EvaluateAsync(
                messages,
                chatResponse,
                chatConfiguration,
                contexts.Count > 0 ? contexts : null);

            // Log metrics to OpenTelemetry activity if provided
            if (parentActivity is not null)
            {
                using (var activity = parentActivity.StartActivity($"{module} Evaluation"))
                {
                    activity?.SetTag($"gen_ai.full_nl_response", response);
                    activity?.SetTag($"gen_ai.evaluation.module", module);
                    activity?.SetTag($"gen_ai.evaluation.user_prompt", userPrompt);

                    // Log all evaluation metrics
                    foreach (var metric in evaluationResult.Metrics)
                    {
                        if (metric.Value is NumericMetric numericMetric)
                        {
                            activity?.SetTag($"gen_ai.evaluation.{numericMetric.Name}.score", numericMetric.Value);
                        }
                    }

                    // Log ground truth evaluation flag
                    activity?.SetTag($"gen_ai.evaluation.has_ground_truth", !string.IsNullOrWhiteSpace(expectedAnswer));
                    
                    // Log retrieval evaluation flag
                    activity?.SetTag($"gen_ai.evaluation.has_retrieval", retrievedDocuments != null && retrievedDocuments.Count > 0);
                    activity?.SetTag($"gen_ai.evaluation.retrieved_document_count", retrievedDocuments?.Count ?? 0);
                }
            }

            return evaluationResult;
        }
    }
}