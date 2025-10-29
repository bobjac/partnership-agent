namespace PartnershipAgent.Core.Agents;

/// <summary>
/// Interface representing the context of who requested an agent operation.
/// Used for multi-tenancy and audit tracking.
/// </summary>
public interface IRequestedBy
{
    string UserId { get; }
    string CompanyId { get; }
    string CompanyName { get; }
    string ProjectId { get; }
}
