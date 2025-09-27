using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PartnershipAgent.Core.Models
{
    /// <summary>
    /// Document model optimized for vector search with pre-computed embeddings.
    /// </summary>
    public class VectorDocument
    {
        /// <summary>
        /// Unique document identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        /// <summary>
        /// Document title for display purposes.
        /// </summary>
        [JsonPropertyName("title")]
        public required string Title { get; set; }

        /// <summary>
        /// Main document content.
        /// </summary>
        [JsonPropertyName("content")]
        public required string Content { get; set; }

        /// <summary>
        /// Document category for filtering.
        /// </summary>
        [JsonPropertyName("category")]
        public required string Category { get; set; }

        /// <summary>
        /// Tenant ID for multi-tenant filtering.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public required string TenantId { get; set; }

        /// <summary>
        /// Pre-computed content embedding vector (1536 dimensions for text-embedding-ada-002).
        /// </summary>
        [JsonPropertyName("contentVector")]
        public float[]? ContentVector { get; set; }

        /// <summary>
        /// When the document was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the document was last updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }
    }
}