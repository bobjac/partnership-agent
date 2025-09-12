using System.Collections.Generic;
using System.Threading.Tasks;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Agents;

public interface IFAQAgent
{
    Task<List<DocumentResult>> SearchDocumentsAsync(string query, string tenantId, List<string> allowedCategories);
    Task<string> GenerateResponseAsync(string query, List<DocumentResult> relevantDocuments);
    Task<FAQAgentResponse> GenerateStructuredResponseAsync(string query, List<DocumentResult> relevantDocuments);
}