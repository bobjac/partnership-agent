using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services
{
    /// <summary>
    /// Adapter that allows IVectorSearchService to be used as IElasticSearchService.
    /// This avoids the circular dependency issue with HybridSearchService.
    /// </summary>
    public class VectorSearchAdapter : IElasticSearchService
    {
        private readonly IVectorSearchService _vectorSearchService;
        private readonly ILogger<VectorSearchAdapter> _logger;

        public VectorSearchAdapter(
            IVectorSearchService vectorSearchService,
            ILogger<VectorSearchAdapter> logger)
        {
            _vectorSearchService = vectorSearchService;
            _logger = logger;
            
            _logger.LogInformation("VectorSearchAdapter initialized - using Azure AI Search for all search operations");
        }

        public async Task<List<DocumentResult>> SearchDocumentsAsync(string query, string tenantId, List<string> allowedCategories)
        {
            _logger.LogInformation("Performing vector search via adapter for query: {Query}", query);
            return await _vectorSearchService.SearchDocumentsAsync(query, tenantId, 5, allowedCategories);
        }

        public async Task<bool> IndexDocumentAsync(DocumentResult document)
        {
            _logger.LogInformation("Indexing document via vector search adapter: {DocumentId}", document.Id);
            return await _vectorSearchService.IndexDocumentAsync(document);
        }
    }
}