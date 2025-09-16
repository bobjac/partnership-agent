using FluentAssertions;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;
using Xunit;

namespace PartnershipAgent.Core.Tests.Services;

public class CitationServiceTests
{
    private readonly CitationService _citationService;

    public CitationServiceTests()
    {
        _citationService = new CitationService();
    }

    [Fact]
    public async Task ExtractCitationsAsync_WithRelevantDocuments_ShouldReturnCitations()
    {
        // Arrange
        var query = "What are the revenue sharing requirements?";
        var answer = "Revenue sharing must be calculated based on gross profits with a minimum threshold of $10,000 per quarter.";
        var documents = new List<DocumentResult>
        {
            new DocumentResult
            {
                Id = "doc1",
                Title = "Revenue Sharing Guidelines",
                Content = "Partnership revenue sharing must be calculated based on gross profits. The minimum threshold for revenue sharing is $10,000 per quarter. All calculations must be submitted monthly.",
                Category = "guidelines",
                TenantId = "tenant1"
            },
            new DocumentResult
            {
                Id = "doc2", 
                Title = "Partnership Compliance",
                Content = "Partners must adhere to all compliance requirements including financial reporting and audit trails.",
                Category = "policies",
                TenantId = "tenant1"
            }
        };

        // Act
        var citations = await _citationService.ExtractCitationsAsync(query, answer, documents);

        // Assert
        citations.Should().NotBeEmpty();
        citations.Should().HaveCountGreaterThan(0);
        
        // Should have citations from at least one document
        var revenueDoc = citations.FirstOrDefault(c => c.DocumentTitle == "Revenue Sharing Guidelines");
        if (revenueDoc != null)
        {
            revenueDoc.DocumentId.Should().Be("doc1");
            revenueDoc.Category.Should().Be("guidelines");
            revenueDoc.Excerpt.Should().NotBeEmpty();
            revenueDoc.RelevanceScore.Should().BeGreaterThan(0);
        }
        
        // At minimum, should have citations with valid structure
        citations.First().DocumentId.Should().NotBeEmpty();
        citations.First().DocumentTitle.Should().NotBeEmpty();
        citations.First().Excerpt.Should().NotBeEmpty();
        citations.First().RelevanceScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CreateCitation_WithValidInputs_ShouldReturnCompleteCitation()
    {
        // Arrange
        var document = new DocumentResult
        {
            Id = "doc1",
            Title = "Test Document",
            Content = "This is a test document with some content for testing purposes. The content contains important information.",
            Category = "test",
            TenantId = "tenant1"
        };
        var excerpt = "important information";
        var startPosition = document.Content.IndexOf(excerpt);
        var endPosition = startPosition + excerpt.Length;
        var relevanceScore = 0.85;

        // Act
        var citation = _citationService.CreateCitation(document, excerpt, startPosition, endPosition, relevanceScore);

        // Assert
        citation.DocumentId.Should().Be("doc1");
        citation.DocumentTitle.Should().Be("Test Document");
        citation.Category.Should().Be("test");
        citation.Excerpt.Should().Be("important information");
        citation.StartPosition.Should().Be(startPosition);
        citation.EndPosition.Should().Be(endPosition);
        citation.RelevanceScore.Should().Be(0.85);
        citation.ContextBefore.Should().NotBeEmpty();
        citation.ContextAfter.Should().NotBeEmpty();
    }

    [Fact]
    public void FindRelevantExcerpts_WithMatchingTerms_ShouldReturnRelevantExcerpts()
    {
        // Arrange
        var content = "Partnership agreements must include revenue sharing clauses. Revenue calculations should be based on net profits. The minimum revenue threshold is $5,000 per month. All revenue reports must be submitted quarterly.";
        var query = "revenue sharing requirements";
        var answer = "Revenue sharing must be calculated based on net profits with a minimum threshold.";

        // Act
        var excerpts = _citationService.FindRelevantExcerpts(content, query, answer, maxExcerpts: 2, excerptLength: 100);

        // Assert
        excerpts.Should().NotBeEmpty();
        excerpts.Should().HaveCountLessOrEqualTo(2);
        excerpts.First().Should().Contain("revenue");
    }

    [Fact]
    public void FindRelevantExcerpts_WithNoMatchingTerms_ShouldReturnEmptyList()
    {
        // Arrange
        var content = "Partnership agreements must include compliance clauses. All partners must follow regulatory guidelines.";
        var query = "revenue sharing requirements";
        var answer = "Revenue sharing must be calculated based on net profits.";

        // Act
        var excerpts = _citationService.FindRelevantExcerpts(content, query, answer);

        // Assert
        excerpts.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "", new string[0])]
    [InlineData("revenue sharing requirements", "revenue calculations", new[] { "doc1" })]
    [InlineData("partnership compliance", "compliance guidelines", new[] { "doc2" })]
    public async Task ExtractCitationsAsync_WithVariousInputs_ShouldHandleCorrectly(
        string query, string answer, string[] expectedDocumentIds)
    {
        // Arrange
        var documents = new List<DocumentResult>
        {
            new DocumentResult
            {
                Id = "doc1",
                Title = "Revenue Guidelines",
                Content = "Revenue sharing calculations must be performed monthly.",
                Category = "guidelines",
                TenantId = "tenant1"
            },
            new DocumentResult
            {
                Id = "doc2",
                Title = "Compliance Policies", 
                Content = "Partnership compliance guidelines must be followed at all times.",
                Category = "policies",
                TenantId = "tenant1"
            }
        };

        // Act
        var citations = await _citationService.ExtractCitationsAsync(query, answer, documents);

        // Assert
        if (expectedDocumentIds.Length == 0)
        {
            citations.Should().BeEmpty();
        }
        else
        {
            citations.Select(c => c.DocumentId).Should().Contain(expectedDocumentIds);
        }
    }
}