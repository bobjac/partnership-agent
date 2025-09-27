using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core.Pipeline;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services
{
    /// <summary>
    /// High-performance vector search service using Azure AI Search with embeddings.
    /// Provides sub-second document retrieval vs. traditional text search.
    /// </summary>
    public class AzureVectorSearchService : IVectorSearchService
    {
        private readonly SearchClient _searchClient;
        private readonly SearchIndexClient _indexClient;
        private readonly AzureOpenAIClient _azureOpenAIClient;
        private readonly string _embeddingDeploymentName;
        private readonly ILogger<AzureVectorSearchService> _logger;
        private const string IndexName = "partnership-documents-vector";
        private const int EmbeddingDimensions = 1536; // text-embedding-ada-002

        public AzureVectorSearchService(
            IConfiguration configuration,
            ILogger<AzureVectorSearchService> logger)
        {
            _logger = logger;

            // Azure AI Search configuration
            var serviceName = configuration["AzureSearch:ServiceName"] ?? throw new InvalidOperationException("AzureSearch:ServiceName not configured");
            var searchEndpoint = $"https://{serviceName}.search.windows.net";
            var searchApiKey = configuration["AzureSearch:ApiKey"] ?? throw new InvalidOperationException("AzureSearch:ApiKey not configured");
            
            var searchCredential = new AzureKeyCredential(searchApiKey);
            _searchClient = new SearchClient(new Uri(searchEndpoint), IndexName, searchCredential);
            _indexClient = new SearchIndexClient(new Uri(searchEndpoint), searchCredential);

            // OpenAI configuration for embeddings
            var openAIEndpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
            var openAIApiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
            _embeddingDeploymentName = configuration["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-ada-002";
            
            // Create client options with infinite network timeout for embeddings
            var clientOptions = new AzureOpenAIClientOptions();
            clientOptions.NetworkTimeout = System.Threading.Timeout.InfiniteTimeSpan;
            
            _azureOpenAIClient = new AzureOpenAIClient(new Uri(openAIEndpoint), new AzureKeyCredential(openAIApiKey), clientOptions);
        }

        /// <summary>
        /// Ultra-fast vector similarity search (typically <100ms vs 5-60s for text search).
        /// </summary>
        public async Task<List<DocumentResult>> SearchDocumentsAsync(string query, string tenantId, int topK = 5, List<string>? allowedCategories = null)
        {
            try
            {
                _logger.LogInformation("Performing vector search for query: {Query}, tenant: {TenantId}", query, tenantId);
                
                // Step 1: Convert query to embedding (fast: ~50-100ms)
                var queryVector = await GenerateEmbeddingAsync(query);
                
                // Step 2: Vector similarity search (ultra-fast: <50ms)
                var searchOptions = new SearchOptions
                {
                    Size = topK,
                    Select = { "id", "title", "content", "category", "tenantId", "createdAt" },
                    Filter = BuildFilter(tenantId, allowedCategories)
                };

                // Add vector search
                searchOptions.VectorSearch = new()
                {
                    Queries = { new VectorizedQuery(queryVector) { KNearestNeighborsCount = topK, Fields = { "contentVector" } } }
                };

                var response = await _searchClient.SearchAsync<VectorDocument>("*", searchOptions);
                
                var results = new List<DocumentResult>();
                await foreach (var result in response.Value.GetResultsAsync())
                {
                    var doc = result.Document;
                    results.Add(new DocumentResult
                    {
                        Id = doc.Id,
                        Title = doc.Title,
                        Content = doc.Content,
                        Category = doc.Category,
                        TenantId = doc.TenantId,
                        Score = (float)(result.Score ?? 0.0),
                        LastModified = doc.CreatedAt
                    });
                }

                _logger.LogInformation("Vector search returned {Count} results in high-speed query", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing vector search");
                
                // Graceful fallback to empty results
                return new List<DocumentResult>();
            }
        }

        /// <summary>
        /// Index a single document with pre-computed embeddings.
        /// </summary>
        public async Task<bool> IndexDocumentAsync(DocumentResult document)
        {
            try
            {
                var embedding = await GenerateEmbeddingAsync(document.Content);
                
                var vectorDoc = new VectorDocument
                {
                    Id = document.Id,
                    Title = document.Title,
                    Content = document.Content,
                    Category = document.Category,
                    TenantId = document.TenantId,
                    ContentVector = embedding,
                    CreatedAt = document.LastModified,
                    UpdatedAt = DateTime.UtcNow
                };

                var batch = IndexDocumentsBatch.Create(IndexDocumentsAction.Upload(vectorDoc));
                await _searchClient.IndexDocumentsAsync(batch);
                
                _logger.LogInformation("Successfully indexed document: {DocumentId}", document.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing document: {DocumentId}", document.Id);
                return false;
            }
        }

        /// <summary>
        /// Batch index multiple documents for better performance.
        /// Uses batch embedding generation to reduce API calls and avoid rate limiting.
        /// </summary>
        public async Task<int> IndexDocumentsBatchAsync(List<DocumentResult> documents)
        {
            try
            {
                _logger.LogInformation("Batch indexing {Count} documents with batch embedding generation", documents.Count);
                
                // Extract all content texts for batch embedding generation
                var contentTexts = documents.Select(doc => doc.Content).ToList();
                
                // Generate all embeddings in a single batch request
                var embeddings = await GenerateEmbeddingsBatchAsync(contentTexts);
                
                // Create vector documents with pre-generated embeddings
                var vectorDocs = new List<VectorDocument>();
                for (int i = 0; i < documents.Count; i++)
                {
                    var doc = documents[i];
                    var embedding = embeddings[i];
                    
                    vectorDocs.Add(new VectorDocument
                    {
                        Id = doc.Id,
                        Title = doc.Title,
                        Content = doc.Content,
                        Category = doc.Category,
                        TenantId = doc.TenantId,
                        ContentVector = embedding,
                        CreatedAt = doc.LastModified,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                // Batch upload to Azure AI Search
                var actions = vectorDocs.Select(doc => IndexDocumentsAction.Upload(doc));
                var batch = IndexDocumentsBatch.Create(actions.ToArray());
                
                var result = await _searchClient.IndexDocumentsAsync(batch);
                var successCount = result.Value.Results.Count(r => r.Succeeded);
                
                _logger.LogInformation("Successfully indexed {SuccessCount}/{TotalCount} documents using batch processing", successCount, documents.Count);
                return successCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch indexing documents");
                return 0;
            }
        }

        /// <summary>
        /// Create the vector search index if it doesn't exist.
        /// </summary>
        public async Task<bool> EnsureIndexExistsAsync()
        {
            try
            {
                // Check if index exists
                try
                {
                    await _indexClient.GetIndexAsync(IndexName);
                    _logger.LogInformation("Vector search index already exists: {IndexName}", IndexName);
                    return true;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    // Index doesn't exist, create it
                    _logger.LogInformation("Creating vector search index: {IndexName}", IndexName);
                }

                var definition = new SearchIndex(IndexName)
                {
                    Fields =
                    {
                        new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                        new SearchableField("title") { IsFilterable = true, IsSortable = true },
                        new SearchableField("content"),
                        new SimpleField("category", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
                        new SimpleField("tenantId", SearchFieldDataType.String) { IsFilterable = true },
                        new SimpleField("createdAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                        new SimpleField("updatedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                        new VectorSearchField("contentVector", EmbeddingDimensions, "default")
                    },
                    VectorSearch = new()
                    {
                        Profiles = { new VectorSearchProfile("default", "default") },
                        Algorithms = { new HnswAlgorithmConfiguration("default") }
                    }
                };

                await _indexClient.CreateIndexAsync(definition);
                _logger.LogInformation("Successfully created vector search index: {IndexName}", IndexName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating vector search index");
                return false;
            }
        }

        private async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            try
            {
                var embeddingClient = _azureOpenAIClient.GetEmbeddingClient(_embeddingDeploymentName);
                var response = await embeddingClient.GenerateEmbeddingAsync(text);
                
                return response.Value.ToFloats().ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text");
                throw;
            }
        }

        /// <summary>
        /// Generate embeddings for multiple texts in a single batch request.
        /// This reduces API calls and helps avoid rate limiting.
        /// </summary>
        private async Task<List<float[]>> GenerateEmbeddingsBatchAsync(List<string> texts)
        {
            try
            {
                _logger.LogInformation("Generating embeddings for {Count} texts in batch", texts.Count);
                
                var embeddingClient = _azureOpenAIClient.GetEmbeddingClient(_embeddingDeploymentName);
                var response = await embeddingClient.GenerateEmbeddingsAsync(texts);
                
                var embeddings = response.Value.Select(embedding => embedding.ToFloats().ToArray()).ToList();
                
                _logger.LogInformation("Successfully generated {Count} embeddings in batch", embeddings.Count);
                return embeddings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating batch embeddings");
                throw;
            }
        }

        private static string BuildFilter(string tenantId, List<string>? allowedCategories)
        {
            var filters = new List<string> { $"tenantId eq '{tenantId}'" };
            
            if (allowedCategories?.Any() == true)
            {
                var categoryFilter = string.Join(" or ", allowedCategories.Select(c => $"category eq '{c}'"));
                filters.Add($"({categoryFilter})");
            }
            
            return string.Join(" and ", filters);
        }
    }
}