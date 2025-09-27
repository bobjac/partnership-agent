using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services
{
    /// <summary>
    /// Hybrid search service that can use either Elasticsearch or Vector Search based on configuration.
    /// Provides seamless migration path from text-based to vector-based search.
    /// </summary>
    public class HybridSearchService : IElasticSearchService
    {
        private readonly IElasticSearchService? _elasticSearchService;
        private readonly IVectorSearchService? _vectorSearchService;
        private readonly bool _useVectorSearch;
        private readonly ILogger<HybridSearchService> _logger;

        public HybridSearchService(
            IElasticSearchService elasticSearchService,
            IVectorSearchService vectorSearchService,
            IConfiguration configuration,
            ILogger<HybridSearchService> logger)
        {
            _elasticSearchService = elasticSearchService;
            _vectorSearchService = vectorSearchService;
            _useVectorSearch = configuration.GetValue<bool>("AzureSearch:UseVectorSearch", false);
            _logger = logger;

            _logger.LogInformation("HybridSearchService initialized with {SearchType} search", 
                _useVectorSearch ? "Vector" : "Elasticsearch");
        }

        public async Task<List<DocumentResult>> SearchDocumentsAsync(string query, string tenantId, List<string> allowedCategories)
        {
            if (_useVectorSearch && _vectorSearchService != null)
            {
                _logger.LogInformation("Using high-performance vector search for query: {Query}", query);
                return await _vectorSearchService.SearchDocumentsAsync(query, tenantId, 5, allowedCategories);
            }
            else if (_elasticSearchService != null)
            {
                _logger.LogInformation("Using Elasticsearch for query: {Query}", query);
                return await _elasticSearchService.SearchDocumentsAsync(query, tenantId, allowedCategories);
            }
            else
            {
                _logger.LogWarning("No search service available");
                return new List<DocumentResult>();
            }
        }

        public async Task<bool> IndexDocumentAsync(DocumentResult document)
        {
            if (_useVectorSearch && _vectorSearchService != null)
            {
                _logger.LogInformation("Indexing document using vector search: {DocumentId}", document.Id);
                return await _vectorSearchService.IndexDocumentAsync(document);
            }
            else if (_elasticSearchService != null)
            {
                _logger.LogInformation("Indexing document using Elasticsearch: {DocumentId}", document.Id);
                return await _elasticSearchService.IndexDocumentAsync(document);
            }
            else
            {
                _logger.LogWarning("No search service available for indexing");
                return false;
            }
        }
    }
}