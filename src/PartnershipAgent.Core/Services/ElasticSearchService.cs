using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nest;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services;

public class ElasticSearchService : IElasticSearchService
{
    private readonly IElasticClient _elasticClient;
    private readonly ILogger<ElasticSearchService> _logger;
    private const string IndexName = "partnership-documents";

    public ElasticSearchService(IElasticClient elasticClient, ILogger<ElasticSearchService> logger)
    {
        _elasticClient = elasticClient;
        _logger = logger;
    }

    public async Task<List<DocumentResult>> SearchDocumentsAsync(string query, string tenantId, List<string> allowedCategories)
    {
        _logger.LogInformation("Searching for documents with query: {Query}, tenant: {TenantId}", query, tenantId);

        try
        {
            var searchResponse = await _elasticClient.SearchAsync<DocumentResult>(s => s
                .Index(IndexName)
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            m => m.MultiMatch(mm => mm
                                .Query(query)
                                .Fields(f => f
                                    .Field(doc => doc.Title, 2.0)
                                    .Field(doc => doc.Content)
                                )
                            ),
                            m => m.Term(t => t.Field(doc => doc.TenantId).Value(tenantId))
                        )
                        .Filter(f => f.Terms(t => t.Field(doc => doc.Category).Terms(allowedCategories)))
                    )
                )
                .Size(5)
            );

            if (searchResponse.IsValid)
            {
                var documents = searchResponse.Documents.ToList();
                _logger.LogInformation("Found {Count} documents", documents.Count);
                return documents;
            }
            else
            {
                _logger.LogWarning("ElasticSearch query failed: {Error}", searchResponse.DebugInformation);
                return new List<DocumentResult>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents");
            return new List<DocumentResult>();
        }
    }

    public async Task<bool> IndexDocumentAsync(DocumentResult document)
    {
        _logger.LogInformation("Indexing document: {Id}", document.Id);

        try
        {
            var response = await _elasticClient.IndexDocumentAsync(document);
            
            if (response.IsValid)
            {
                _logger.LogInformation("Successfully indexed document: {Id}", document.Id);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to index document: {Error}", response.DebugInformation);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing document: {Id}", document.Id);
            return false;
        }
    }

}