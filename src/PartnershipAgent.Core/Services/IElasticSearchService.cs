using System.Collections.Generic;
using System.Threading.Tasks;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services;

public interface IElasticSearchService
{
    Task<List<DocumentResult>> SearchDocumentsAsync(string query, string tenantId, List<string> allowedCategories);
    Task<bool> IndexDocumentAsync(DocumentResult document);
}