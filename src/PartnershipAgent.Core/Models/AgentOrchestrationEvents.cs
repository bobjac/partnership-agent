namespace PartnershipAgent.Core.Models;

/// <summary>
/// Constants for agent orchestration event identifiers used in the process framework.
/// </summary>
public static class AgentOrchestrationEvents
{
    /// <summary>
    /// Event fired to start the partnership agent process.
    /// </summary>
    public const string StartProcess = "StartProcess";

    /// <summary>
    /// Event fired when entity extraction is completed.
    /// </summary>
    public const string EntityExtractionCompleted = "EntityExtractionCompleted";

    /// <summary>
    /// Event fired when document search is completed.
    /// </summary>
    public const string DocumentSearchCompleted = "DocumentSearchCompleted";

    /// <summary>
    /// Event fired when FAQ response generation is completed.
    /// </summary>
    public const string FAQResponseCompleted = "FAQResponseCompleted";

    /// <summary>
    /// Event fired when response generation is completed.
    /// </summary>
    public const string ResponseGenerationCompleted = "ResponseGenerationCompleted";

    /// <summary>
    /// Event fired when the process is completed successfully.
    /// </summary>
    public const string ProcessCompleted = "ProcessCompleted";

    /// <summary>
    /// Event fired when user clarification is needed.
    /// </summary>
    public const string UserClarificationNeeded = "UserClarificationNeeded";

    /// <summary>
    /// Event fired when there's an error that should terminate the process.
    /// </summary>
    public const string ProcessError = "ProcessError";
}

/// <summary>
/// Constants for AI event types used in client communication.
/// </summary>
public static class AIEventTypes
{
    /// <summary>
    /// Chat message event type.
    /// </summary>
    public const string Chat = "chat";

    /// <summary>
    /// Status update event type.
    /// </summary>
    public const string Status = "status";

    /// <summary>
    /// Error event type.
    /// </summary>
    public const string Error = "error";

    /// <summary>
    /// Completion event type.
    /// </summary>
    public const string Completion = "completion";
}