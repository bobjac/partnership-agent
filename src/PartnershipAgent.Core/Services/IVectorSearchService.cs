using System.Collections.Generic;
using System.Threading.Tasks;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services
{
    /// <summary>
    /// Service for vector-based document search using embeddings.
    /// Provides much faster and more accurate semantic search than text-based search.
    /// </summary>
    public interface IVectorSearchService
    {
        /// <summary>
        /// Searches for documents using vector similarity with pre-computed embeddings.
        /// </summary>
        /// <param name="query">The user query to search for.</param>
        /// <param name="tenantId">The tenant ID for filtering.</param>
        /// <param name="topK">Number of top results to return (default: 5).</param>
        /// <param name="allowedCategories">Categories to filter by.</param>
        /// <returns>List of relevant documents with similarity scores.</returns>
        Task<List<DocumentResult>> SearchDocumentsAsync(string query, string tenantId, int topK = 5, List<string>? allowedCategories = null);

        /// <summary>
        /// Pre-computes and stores embeddings for a document.
        /// Should be called when documents are added/updated.
        /// </summary>
        /// <param name="document">The document to index.</param>
        /// <returns>Success indicator.</returns>
        Task<bool> IndexDocumentAsync(DocumentResult document);

        /// <summary>
        /// Batch index multiple documents for improved performance.
        /// </summary>
        /// <param name="documents">Documents to index.</param>
        /// <returns>Number of successfully indexed documents.</returns>
        Task<int> IndexDocumentsBatchAsync(List<DocumentResult> documents);
    }
}