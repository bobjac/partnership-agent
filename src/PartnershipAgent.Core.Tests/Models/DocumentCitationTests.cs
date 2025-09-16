using FluentAssertions;
using PartnershipAgent.Core.Models;
using System.Text.Json;
using Xunit;

namespace PartnershipAgent.Core.Tests.Models;

public class DocumentCitationTests
{
    [Fact]
    public void DocumentCitation_ShouldSerializeToJson()
    {
        // Arrange
        var citation = new DocumentCitation
        {
            DocumentId = "doc123",
            DocumentTitle = "Partnership Agreement",
            Category = "contracts",
            Excerpt = "Revenue sharing shall be calculated monthly",
            StartPosition = 150,
            EndPosition = 185,
            RelevanceScore = 0.92,
            ContextBefore = "Section 4.2:",
            ContextAfter = "based on gross profits."
        };

        // Act
        var json = JsonSerializer.Serialize(citation);
        var deserialized = JsonSerializer.Deserialize<DocumentCitation>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.DocumentId.Should().Be("doc123");
        deserialized.DocumentTitle.Should().Be("Partnership Agreement");
        deserialized.Category.Should().Be("contracts");
        deserialized.Excerpt.Should().Be("Revenue sharing shall be calculated monthly");
        deserialized.StartPosition.Should().Be(150);
        deserialized.EndPosition.Should().Be(185);
        deserialized.RelevanceScore.Should().Be(0.92);
        deserialized.ContextBefore.Should().Be("Section 4.2:");
        deserialized.ContextAfter.Should().Be("based on gross profits.");
    }

    [Fact]
    public void DocumentCitation_ShouldHaveCorrectJsonPropertyNames()
    {
        // Arrange
        var citation = new DocumentCitation
        {
            DocumentId = "doc123",
            DocumentTitle = "Test Title",
            Category = "test",
            Excerpt = "Test excerpt",
            StartPosition = 10,
            EndPosition = 20,
            RelevanceScore = 0.5
        };

        // Act
        var json = JsonSerializer.Serialize(citation);

        // Assert
        json.Should().Contain("\"document_id\":");
        json.Should().Contain("\"document_title\":");
        json.Should().Contain("\"category\":");
        json.Should().Contain("\"excerpt\":");
        json.Should().Contain("\"start_position\":");
        json.Should().Contain("\"end_position\":");
        json.Should().Contain("\"relevance_score\":");
        json.Should().Contain("\"context_before\":");
        json.Should().Contain("\"context_after\":");
    }
}