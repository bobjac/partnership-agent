using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Services;

namespace PartnershipAgent.WebApi.Controllers
{
    /// <summary>
    /// Administrative endpoints for managing the vector search system.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly DocumentIndexingService _indexingService;
        private readonly IElasticSearchService _elasticSearchService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            DocumentIndexingService indexingService,
            IElasticSearchService elasticSearchService,
            ILogger<AdminController> logger)
        {
            _indexingService = indexingService;
            _elasticSearchService = elasticSearchService;
            _logger = logger;
        }

        /// <summary>
        /// Initialize the vector search system with sample documents.
        /// Call this once after setting up Azure AI Search configuration.
        /// </summary>
        [HttpPost("initialize-vector-search")]
        public async Task<IActionResult> InitializeVectorSearchAsync()
        {
            try
            {
                _logger.LogInformation("Admin request to initialize vector search system");
                
                var success = await _indexingService.InitializeAsync();
                
                if (success)
                {
                    return Ok(new { 
                        message = "Vector search system initialized successfully",
                        timestamp = DateTime.UtcNow,
                        status = "ready"
                    });
                }
                else
                {
                    return StatusCode(500, new { 
                        message = "Failed to initialize vector search system",
                        timestamp = DateTime.UtcNow,
                        status = "error"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing vector search system");
                return StatusCode(500, new { 
                    message = "Internal server error during initialization",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Re-index sample documents (useful for testing or updates).
        /// </summary>
        [HttpPost("reindex-documents")]
        public async Task<IActionResult> ReindexDocumentsAsync()
        {
            try
            {
                _logger.LogInformation("Admin request to re-index sample documents");
                
                var indexedCount = await _indexingService.IndexSampleDocumentsAsync();
                
                return Ok(new { 
                    message = $"Successfully indexed {indexedCount} documents",
                    documentsIndexed = indexedCount,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error re-indexing documents");
                return StatusCode(500, new { 
                    message = "Internal server error during re-indexing",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Index documents one by one with delay to avoid rate limits.
        /// </summary>
        [HttpPost("reindex-documents-slow")]
        public async Task<IActionResult> ReindexDocumentsSlowAsync()
        {
            try
            {
                _logger.LogInformation("Admin request to re-index sample documents one by one");
                
                var indexedCount = 0;
                var sampleDocuments = GetSampleDocuments();
                
                foreach (var doc in sampleDocuments)
                {
                    try
                    {
                        _logger.LogInformation("Indexing document {Id}: {Title}", doc.Id, doc.Title);
                        var success = await _indexingService.IndexDocumentAsync(doc);
                        if (success)
                        {
                            indexedCount++;
                            _logger.LogInformation("Successfully indexed document {Id}", doc.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to index document {Id}", doc.Id);
                        }
                        
                        // Wait 10 seconds between documents to avoid rate limiting
                        await Task.Delay(10000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error indexing document {Id}", doc.Id);
                    }
                }
                
                return Ok(new { 
                    message = $"Slowly indexed {indexedCount}/{sampleDocuments.Count} documents",
                    documentsIndexed = indexedCount,
                    totalDocuments = sampleDocuments.Count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in slow re-indexing");
                return StatusCode(500, new { 
                    message = "Internal server error during slow re-indexing",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Check Azure OpenAI configuration settings.
        /// </summary>
        [HttpGet("check-config")]
        public IActionResult CheckConfiguration()
        {
            try
            {
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                
                return Ok(new {
                    azureOpenAI = new {
                        endpoint = !string.IsNullOrEmpty(configuration["AzureOpenAI:Endpoint"]) ? 
                            configuration["AzureOpenAI:Endpoint"] : "NOT SET",
                        hasApiKey = !string.IsNullOrEmpty(configuration["AzureOpenAI:ApiKey"]),
                        embeddingDeploymentName = configuration["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-ada-002 (default)",
                        apiVersion = configuration["AzureOpenAI:ApiVersion"] ?? "2024-02-15-preview (default)"
                    },
                    azureSearch = new {
                        serviceName = configuration["AzureSearch:ServiceName"] ?? "NOT SET",
                        hasApiKey = !string.IsNullOrEmpty(configuration["AzureSearch:ApiKey"]),
                        useVectorSearch = configuration.GetValue<bool>("AzureSearch:UseVectorSearch", false)
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new {
                    message = "Error checking configuration",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Test Azure OpenAI embeddings configuration directly.
        /// </summary>
        [HttpPost("test-embeddings")]
        public async Task<IActionResult> TestEmbeddingsAsync()
        {
            try
            {
                _logger.LogInformation("Testing Azure OpenAI embeddings configuration");
                
                // Test with simple text
                var testText = "This is a test for Azure OpenAI embeddings.";
                
                // We need to access the vector search service directly
                var vectorService = HttpContext.RequestServices.GetRequiredService<IVectorSearchService>();
                
                // Test individual embedding generation
                _logger.LogInformation("Attempting to generate embedding for test text...");
                var startTime = DateTime.UtcNow;
                
                // Try to call the embedding method through a test document
                var testDoc = new PartnershipAgent.Core.Models.DocumentResult
                {
                    Id = "test-embedding",
                    Title = "Test Embedding",
                    Content = testText,
                    Category = "test",
                    TenantId = "test",
                    Score = 1.0f,
                    LastModified = DateTime.UtcNow
                };
                
                var success = await vectorService.IndexDocumentAsync(testDoc);
                var duration = DateTime.UtcNow - startTime;
                
                if (success)
                {
                    return Ok(new {
                        message = "Azure OpenAI embeddings working correctly",
                        testText = testText,
                        duration = duration.TotalSeconds,
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new {
                        message = "Azure OpenAI embeddings failed",
                        testText = testText,
                        duration = duration.TotalSeconds,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure OpenAI embeddings test failed");
                return StatusCode(500, new {
                    message = "Azure OpenAI embeddings test failed with exception",
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Test endpoint to index a single document (for debugging).
        /// </summary>
        [HttpPost("test-single-document")]
        public async Task<IActionResult> TestSingleDocumentAsync()
        {
            try
            {
                _logger.LogInformation("Admin request to test single document indexing");
                
                var testDoc = new PartnershipAgent.Core.Models.DocumentResult
                {
                    Id = "test-doc-1",
                    Title = "Test Partnership Document",
                    Content = "This is a test partnership document with minimal content for testing Azure AI Search vector indexing functionality.",
                    Category = "test",
                    TenantId = "test-tenant",
                    Score = 1.0f,
                    LastModified = DateTime.UtcNow
                };
                
                var success = await _indexingService.IndexDocumentAsync(testDoc);
                
                if (success)
                {
                    return Ok(new { 
                        message = "Successfully indexed test document",
                        documentId = testDoc.Id,
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new { 
                        message = "Failed to index test document",
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing test document");
                return StatusCode(500, new { 
                    message = "Internal server error during test document indexing",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Directly upload all sample documents using Azure AI Search REST API.
        /// This bypasses the .NET SDK and embedding generation to isolate issues.
        /// </summary>
        [HttpPost("direct-upload")]
        public async Task<IActionResult> DirectUploadAsync()
        {
            try
            {
                _logger.LogInformation("Admin request to directly upload documents to Azure AI Search");
                
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var searchApiKey = configuration["AzureSearch:ApiKey"];
                var serviceName = configuration["AzureSearch:ServiceName"];
                
                if (string.IsNullOrEmpty(searchApiKey) || string.IsNullOrEmpty(serviceName))
                {
                    return BadRequest(new { message = "Azure Search configuration missing" });
                }
                
                // Get embeddings for all sample documents first
                var httpClient = new HttpClient();
                var openAIEndpoint = configuration["AzureOpenAI:Endpoint"];
                var openAIApiKey = configuration["AzureOpenAI:ApiKey"];
                var embeddingDeploymentName = configuration["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-ada-002";
                
                var sampleDocuments = GetSampleDocuments();
                var documentsWithEmbeddings = new List<object>();
                
                _logger.LogInformation("Generating embeddings for {Count} documents", sampleDocuments.Count);
                
                foreach (var doc in sampleDocuments)
                {
                    try
                    {
                        // Generate embedding
                        var embeddingRequest = new
                        {
                            input = doc.Content
                        };
                        
                        httpClient.DefaultRequestHeaders.Clear();
                        httpClient.DefaultRequestHeaders.Add("api-key", openAIApiKey);
                        
                        var embeddingResponse = await httpClient.PostAsJsonAsync(
                            $"{openAIEndpoint}/openai/deployments/{embeddingDeploymentName}/embeddings?api-version=2023-05-15",
                            embeddingRequest,
                            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                        
                        if (!embeddingResponse.IsSuccessStatusCode)
                        {
                            var errorContent = await embeddingResponse.Content.ReadAsStringAsync();
                            _logger.LogError("Failed to generate embedding for document {Id}: {Error}", doc.Id, errorContent);
                            continue;
                        }
                        
                        var embeddingResult = await embeddingResponse.Content.ReadFromJsonAsync<dynamic>();
                        var embedding = embeddingResult?.data?[0]?.embedding;
                        
                        // Create document for Azure AI Search
                        var searchDoc = new
                        {
                            id = doc.Id,
                            title = doc.Title,
                            content = doc.Content,
                            category = doc.Category,
                            tenantId = doc.TenantId,
                            createdAt = doc.LastModified,
                            updatedAt = DateTime.UtcNow,
                            contentVector = embedding
                        };
                        
                        documentsWithEmbeddings.Add(searchDoc);
                        _logger.LogInformation("Generated embedding for document {Id}", doc.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error generating embedding for document {Id}", doc.Id);
                    }
                }
                
                // Upload to Azure AI Search
                var uploadRequest = new
                {
                    value = documentsWithEmbeddings.Select(doc => new Dictionary<string, object>
                    {
                        ["@search.action"] = "upload",
                        // Flatten the document properties
                        ["id"] = ((dynamic)doc).id,
                        ["content"] = ((dynamic)doc).content,
                        ["title"] = ((dynamic)doc).title,
                        ["category"] = ((dynamic)doc).category,
                        ["tenantId"] = ((dynamic)doc).tenantId,
                        ["createdAt"] = ((dynamic)doc).createdAt,
                        ["updatedAt"] = ((dynamic)doc).updatedAt,
                        ["contentVector"] = ((dynamic)doc).contentVector
                    }).ToArray()
                };
                
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("api-key", searchApiKey);
                
                var uploadResponse = await httpClient.PostAsJsonAsync(
                    $"https://{serviceName}.search.windows.net/indexes/partnership-documents-vector/docs/index?api-version=2023-11-01",
                    uploadRequest);
                
                if (uploadResponse.IsSuccessStatusCode)
                {
                    return Ok(new
                    {
                        message = $"Successfully uploaded {documentsWithEmbeddings.Count} documents directly to Azure AI Search",
                        documentsUploaded = documentsWithEmbeddings.Count,
                        totalDocuments = sampleDocuments.Count,
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    var errorContent = await uploadResponse.Content.ReadAsStringAsync();
                    return StatusCode(500, new
                    {
                        message = "Failed to upload documents to Azure AI Search",
                        error = errorContent,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in direct upload");
                return StatusCode(500, new
                {
                    message = "Internal server error during direct upload",
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Diagnostic endpoint to trace exactly where requests are failing.
        /// </summary>
        [HttpPost("debug-request")]
        public async Task<IActionResult> DebugRequestAsync()
        {
            try
            {
                _logger.LogInformation("DEBUG: Starting diagnostic request");
                
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var openAIEndpoint = configuration["AzureOpenAI:Endpoint"];
                var openAIApiKey = configuration["AzureOpenAI:ApiKey"];
                var embeddingDeploymentName = configuration["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-ada-002";
                
                _logger.LogInformation("DEBUG: Configuration loaded - Endpoint: {Endpoint}", openAIEndpoint);
                
                var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                
                var embeddingRequest = new { input = "Simple test text" };
                var embeddingUrl = $"{openAIEndpoint}/openai/deployments/{embeddingDeploymentName}/embeddings?api-version=2023-05-15";
                
                _logger.LogInformation("DEBUG: About to make request to: {Url}", embeddingUrl);
                
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("api-key", openAIApiKey);
                
                _logger.LogInformation("DEBUG: Headers set, making HTTP request...");
                
                var startTime = DateTime.UtcNow;
                var embeddingResponse = await httpClient.PostAsJsonAsync(embeddingUrl, embeddingRequest);
                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;
                
                _logger.LogInformation("DEBUG: Request completed in {Duration}ms. Status: {Status}", 
                    duration.TotalMilliseconds, embeddingResponse.StatusCode);
                
                if (!embeddingResponse.IsSuccessStatusCode)
                {
                    var errorContent = await embeddingResponse.Content.ReadAsStringAsync();
                    _logger.LogError("DEBUG: Request failed with: {Error}", errorContent);
                    
                    return Ok(new {
                        success = false,
                        status = embeddingResponse.StatusCode,
                        error = errorContent,
                        duration = duration.TotalMilliseconds
                    });
                }
                
                var responseContent = await embeddingResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("DEBUG: Request succeeded, response length: {Length}", responseContent.Length);
                
                return Ok(new {
                    success = true,
                    status = embeddingResponse.StatusCode,
                    duration = duration.TotalMilliseconds,
                    responseLength = responseContent.Length,
                    message = "Request completed successfully"
                });
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError("DEBUG: Request timed out: {Error}", ex.Message);
                return Ok(new {
                    success = false,
                    error = "Request timed out",
                    message = ex.Message
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError("DEBUG: HTTP request failed: {Error}", ex.Message);
                return Ok(new {
                    success = false,
                    error = "HTTP request failed",
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG: Unexpected error");
                return Ok(new {
                    success = false,
                    error = "Unexpected error",
                    message = ex.Message,
                    innerMessage = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Migrate documents from Elasticsearch to Azure AI Search with vector embeddings.
        /// This populates the vector database with existing documents using batch processing.
        /// </summary>
        [HttpPost("migrate-to-vector")]
        public async Task<IActionResult> MigrateToVectorAsync()
        {
            try
            {
                _logger.LogInformation("Admin request to migrate documents to vector search");
                
                // Get all documents from Elasticsearch
                var documents = await _elasticSearchService.SearchDocumentsAsync("*", "default-tenant", new List<string>());
                
                if (documents.Count == 0)
                {
                    return Ok(new { 
                        message = "No documents found in Elasticsearch to migrate",
                        documentsProcessed = 0,
                        timestamp = DateTime.UtcNow
                    });
                }

                _logger.LogInformation("Found {Count} documents in Elasticsearch, starting batch migration", documents.Count);
                
                // Use batch indexing for better performance
                var successCount = await _indexingService.IndexDocumentsBatchAsync(documents);
                
                _logger.LogInformation("Batch migration completed: {SuccessCount}/{TotalCount} documents indexed", 
                    successCount, documents.Count);
                
                return Ok(new { 
                    message = $"Migration completed: {successCount}/{documents.Count} documents indexed",
                    documentsFound = documents.Count,
                    documentsIndexed = successCount,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating documents to vector search");
                return StatusCode(500, new { 
                    message = "Internal server error during migration",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        private List<PartnershipAgent.Core.Models.DocumentResult> GetSampleDocuments()
        {
            return new List<PartnershipAgent.Core.Models.DocumentResult>
            {
                new PartnershipAgent.Core.Models.DocumentResult
                {
                    Id = "doc1",
                    Title = "Partnership Agreement Template",
                    Content = @"Partnership Formation and Scope: All partnerships must be formalized through written agreements that clearly define roles, responsibilities, and expectations.

Revenue Sharing Structure: Partner compensation is structured in multiple tiers based on contribution levels:
- Tier 1 Partners (Strategic): Receive 30-35% of net revenue from direct contributions
- Tier 2 Partners (Operational): Receive 20-25% of net revenue from operational support  
- Tier 3 Partners (Referral): Receive 10-15% of net revenue from referral activities",
                    Category = "templates",
                    TenantId = "tenant-123",
                    Score = 0.95f,
                    LastModified = DateTime.UtcNow.AddDays(-30)
                },
                
                new PartnershipAgent.Core.Models.DocumentResult
                {
                    Id = "doc2", 
                    Title = "Revenue Sharing Guidelines",
                    Content = @"Gross Revenue Determination: Gross revenue includes all income streams directly attributable to partnership activities, including direct sales revenue from partnership-generated clients, recurring subscription revenue from partner referrals, service fees from partner-delivered projects, and commission from third-party integrations.",
                    Category = "guidelines",
                    TenantId = "tenant-123", 
                    Score = 0.87f,
                    LastModified = DateTime.UtcNow.AddDays(-25)
                },

                new PartnershipAgent.Core.Models.DocumentResult
                {
                    Id = "doc3",
                    Title = "Partnership Compliance Requirements", 
                    Content = @"Documentation Standards: Maintain detailed records of all partnership activities, document all revenue streams and partner contributions, preserve communication logs for minimum 7 years, and ensure data privacy compliance (GDPR, CCPA).",
                    Category = "policies",
                    TenantId = "tenant-123",
                    Score = 0.82f, 
                    LastModified = DateTime.UtcNow.AddDays(-20)
                },

                new PartnershipAgent.Core.Models.DocumentResult
                {
                    Id = "doc4",
                    Title = "Standard Partnership Contract",
                    Content = @"Intellectual Property Rights: Each party retains ownership of intellectual property existing prior to partnership formation. Intellectual property developed jointly during partnership activities has shared ownership between contributing parties, revenue sharing applies to IP monetization, licensing decisions require mutual consent, and each party may use joint IP for partnership purposes.",
                    Category = "contracts",
                    TenantId = "tenant-123",
                    Score = 0.91f,
                    LastModified = DateTime.UtcNow.AddDays(-15)
                },

                new PartnershipAgent.Core.Models.DocumentResult
                {
                    Id = "doc5",
                    Title = "Performance Metrics and KPIs",
                    Content = @"Revenue Performance: Monthly Recurring Revenue (MRR) from partner activities, Customer Acquisition Cost (CAC) for partner-generated leads, Customer Lifetime Value (CLV) from partner relationships, and revenue growth rate quarter-over-quarter.",
                    Category = "guidelines", 
                    TenantId = "tenant-123",
                    Score = 0.92f,
                    LastModified = DateTime.UtcNow.AddDays(-10)
                }
            };
        }
    }
}