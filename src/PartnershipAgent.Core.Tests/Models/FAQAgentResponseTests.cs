using FluentAssertions;
using PartnershipAgent.Core.Models;
using System.Text.Json;
using Xunit;

namespace PartnershipAgent.Core.Tests.Models;

public class FAQAgentResponseTests
{
    [Fact]
    public void FAQAgentResponse_WithCitations_ShouldSerializeCorrectly()
    {
        // Arrange
        var citations = new List<DocumentCitation>
        {
            new DocumentCitation
            {
                DocumentId = "doc1",
                DocumentTitle = "Revenue Guidelines",
                Category = "guidelines",
                Excerpt = "Revenue sharing calculations",
                StartPosition = 100,
                EndPosition = 125,
                RelevanceScore = 0.85
            },
            new DocumentCitation
            {
                DocumentId = "doc2",
                DocumentTitle = "Partnership Agreement",
                Category = "contracts", 
                Excerpt = "Monthly revenue reports",
                StartPosition = 200,
                EndPosition = 221,
                RelevanceScore = 0.75
            }
        };

        var response = new FAQAgentResponse
        {
            Answer = "Revenue sharing must be calculated monthly based on gross profits.",
            ConfidenceLevel = "high",
            SourceDocuments = new List<string> { "Revenue Guidelines", "Partnership Agreement" },
            Citations = citations,
            HasCompleteAnswer = true,
            FollowUpSuggestions = new List<string> { "How are profits calculated?", "What are the reporting requirements?" }
        };

        // Act
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<FAQAgentResponse>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Answer.Should().Be("Revenue sharing must be calculated monthly based on gross profits.");
        deserialized.ConfidenceLevel.Should().Be("high");
        deserialized.SourceDocuments.Should().HaveCount(2);
        deserialized.Citations.Should().HaveCount(2);
        deserialized.Citations.First().DocumentId.Should().Be("doc1");
        deserialized.Citations.First().RelevanceScore.Should().Be(0.85);
        deserialized.HasCompleteAnswer.Should().BeTrue();
        deserialized.FollowUpSuggestions.Should().HaveCount(2);
    }

    [Fact]
    public void FAQAgentResponse_ShouldHaveCorrectJsonPropertyNames()
    {
        // Arrange
        var response = new FAQAgentResponse
        {
            Answer = "Test answer",
            ConfidenceLevel = "medium",
            SourceDocuments = new List<string> { "Doc1" },
            Citations = new List<DocumentCitation>(),
            HasCompleteAnswer = false,
            FollowUpSuggestions = new List<string>()
        };

        // Act
        var json = JsonSerializer.Serialize(response);

        // Assert
        json.Should().Contain("\"answer\":");
        json.Should().Contain("\"confidence_level\":");
        json.Should().Contain("\"source_documents\":");
        json.Should().Contain("\"citations\":");
        json.Should().Contain("\"has_complete_answer\":");
        json.Should().Contain("\"follow_up_suggestions\":");
    }

    [Fact]
    public void FAQAgentResponse_WithEmptyCitations_ShouldSerializeCorrectly()
    {
        // Arrange
        var response = new FAQAgentResponse
        {
            Answer = "No relevant information found.",
            ConfidenceLevel = "low",
            SourceDocuments = new List<string>(),
            Citations = new List<DocumentCitation>(),
            HasCompleteAnswer = false,
            FollowUpSuggestions = new List<string>()
        };

        // Act
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<FAQAgentResponse>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Citations.Should().BeEmpty();
        deserialized.SourceDocuments.Should().BeEmpty();
        deserialized.HasCompleteAnswer.Should().BeFalse();
    }
}