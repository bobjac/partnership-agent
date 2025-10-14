using FluentAssertions;
using PartnershipAgent.Core.Models;
using System.Text.Json;
using Xunit;

namespace PartnershipAgent.Core.Tests.Models;

public class ScopingAgentResponseTests
{
    [Fact]
    public void ScopingAgentResponse_InScope_ShouldSerializeCorrectly()
    {
        // Arrange
        var response = new ScopingAgentResponse
        {
            IsInScope = true,
            ConfidenceLevel = "high",
            Reasoning = "The user is asking about partnership agreements",
            Category = "partnership_inquiry",
            OutOfScopeMessage = null,
            Suggestions = new List<string>
            {
                "What are the revenue sharing terms?",
                "How do I find partnership documents?"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<ScopingAgentResponse>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsInScope.Should().BeTrue();
        deserialized.ConfidenceLevel.Should().Be("high");
        deserialized.Reasoning.Should().Be("The user is asking about partnership agreements");
        deserialized.Category.Should().Be("partnership_inquiry");
        deserialized.OutOfScopeMessage.Should().BeNull();
        deserialized.Suggestions.Should().HaveCount(2);
    }

    [Fact]
    public void ScopingAgentResponse_OutOfScope_ShouldSerializeCorrectly()
    {
        // Arrange
        var response = new ScopingAgentResponse
        {
            IsInScope = false,
            ConfidenceLevel = "high",
            Reasoning = "The user is requesting a joke which is outside the system's scope",
            Category = "joke_request",
            OutOfScopeMessage = "I'm designed to help with partnership agreements, not entertainment.",
            Suggestions = new List<string>
            {
                "What partnership tiers are available?",
                "How are revenue calculations performed?",
                "What are the termination procedures?"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<ScopingAgentResponse>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsInScope.Should().BeFalse();
        deserialized.ConfidenceLevel.Should().Be("high");
        deserialized.Reasoning.Should().Contain("joke");
        deserialized.Category.Should().Be("joke_request");
        deserialized.OutOfScopeMessage.Should().NotBeNullOrEmpty();
        deserialized.Suggestions.Should().HaveCount(3);
    }

    [Fact]
    public void ScopingAgentResponse_ShouldHaveCorrectJsonPropertyNames()
    {
        // Arrange
        var response = new ScopingAgentResponse
        {
            IsInScope = true,
            ConfidenceLevel = "medium",
            Reasoning = "Test reasoning",
            Category = "test_category",
            OutOfScopeMessage = "Test message",
            Suggestions = new List<string> { "Test suggestion" }
        };

        // Act
        var json = JsonSerializer.Serialize(response);

        // Assert
        json.Should().Contain("\"isInScope\":");
        json.Should().Contain("\"confidenceLevel\":");
        json.Should().Contain("\"reasoning\":");
        json.Should().Contain("\"category\":");
        json.Should().Contain("\"outOfScopeMessage\":");
        json.Should().Contain("\"suggestions\":");
    }

    [Fact]
    public void ScopingAgentResponse_WithEmptySuggestions_ShouldSerializeCorrectly()
    {
        // Arrange
        var response = new ScopingAgentResponse
        {
            IsInScope = true,
            ConfidenceLevel = "low",
            Reasoning = "Borderline request",
            Category = "unknown",
            OutOfScopeMessage = null,
            Suggestions = new List<string>()
        };

        // Act
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<ScopingAgentResponse>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Suggestions.Should().BeEmpty();
        deserialized.IsInScope.Should().BeTrue();
    }

    [Fact]
    public void ScopingAgentResponse_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var response = new ScopingAgentResponse();

        // Assert
        response.IsInScope.Should().BeFalse(); // default for bool
        response.ConfidenceLevel.Should().Be("medium");
        response.Reasoning.Should().Be(string.Empty);
        response.Category.Should().Be(string.Empty);
        response.OutOfScopeMessage.Should().BeNull();
        response.Suggestions.Should().NotBeNull().And.BeEmpty();
    }

    [Theory]
    [InlineData("partnership_inquiry")]
    [InlineData("financial_question")]
    [InlineData("off_topic")]
    [InlineData("general_chat")]
    [InlineData("joke_request")]
    public void ScopingAgentResponse_DifferentCategories_ShouldSerializeCorrectly(string category)
    {
        // Arrange
        var response = new ScopingAgentResponse
        {
            IsInScope = category.Contains("partnership") || category.Contains("financial"),
            ConfidenceLevel = "high",
            Reasoning = $"Category: {category}",
            Category = category,
            OutOfScopeMessage = null,
            Suggestions = new List<string>()
        };

        // Act
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<ScopingAgentResponse>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Category.Should().Be(category);
    }
}
