using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services;

public class CitationService : ICitationService
{
    private const int DefaultExcerptLength = 200;
    private const int DefaultContextLength = 50;
    private const int MaxExcerptsPerDocument = 3;

    public async Task<List<DocumentCitation>> ExtractCitationsAsync(
        string query, 
        string generatedAnswer, 
        List<DocumentResult> sourceDocuments, 
        CancellationToken cancellationToken = default)
    {
        var citations = new List<DocumentCitation>();

        foreach (var document in sourceDocuments)
        {
            var excerpts = FindRelevantExcerpts(document.Content, query, generatedAnswer);
            
            foreach (var excerpt in excerpts.Take(MaxExcerptsPerDocument))
            {
                var startPosition = document.Content.IndexOf(excerpt, StringComparison.OrdinalIgnoreCase);
                if (startPosition >= 0)
                {
                    var endPosition = startPosition + excerpt.Length;
                    var relevanceScore = CalculateRelevanceScore(excerpt, query, generatedAnswer);
                    
                    var citation = CreateCitation(
                        document,
                        excerpt,
                        startPosition,
                        endPosition,
                        relevanceScore);
                    
                    citations.Add(citation);
                }
            }
        }

        return citations.OrderByDescending(c => c.RelevanceScore).ToList();
    }

    public DocumentCitation CreateCitation(
        DocumentResult document,
        string excerpt,
        int startPosition,
        int endPosition,
        double relevanceScore)
    {
        var contextBefore = ExtractContext(document.Content, startPosition, DefaultContextLength, isAfter: false);
        var contextAfter = ExtractContext(document.Content, endPosition, DefaultContextLength, isAfter: true);

        return new DocumentCitation
        {
            DocumentId = document.Id,
            DocumentTitle = document.Title,
            Category = document.Category,
            Excerpt = excerpt,
            StartPosition = startPosition,
            EndPosition = endPosition,
            RelevanceScore = relevanceScore,
            ContextBefore = contextBefore,
            ContextAfter = contextAfter
        };
    }

    public List<string> FindRelevantExcerpts(
        string content,
        string query,
        string answer,
        int maxExcerpts = 3,
        int excerptLength = DefaultExcerptLength)
    {
        var excerpts = new List<(string text, double score)>();
        var queryTerms = ExtractKeyTerms(query);
        var answerTerms = ExtractKeyTerms(answer);
        
        var sentences = SplitIntoSentences(content);
        
        for (int i = 0; i < sentences.Count; i++)
        {
            var windowText = string.Join(" ", sentences.Skip(i).Take(3));
            
            if (windowText.Length > excerptLength)
            {
                windowText = TruncateToLength(windowText, excerptLength);
            }
            
            var score = CalculateTextRelevance(windowText, queryTerms, answerTerms);
            
            if (score > 0.1)
            {
                excerpts.Add((windowText, score));
            }
        }
        
        return excerpts
            .OrderByDescending(e => e.score)
            .Take(maxExcerpts)
            .Select(e => e.text)
            .ToList();
    }

    private List<string> ExtractKeyTerms(string text)
    {
        var words = Regex.Split(text.ToLowerInvariant(), @"[\s\p{P}]+")
            .Where(w => w.Length > 3)
            .Where(w => !IsStopWord(w))
            .Distinct()
            .ToList();
        
        return words;
    }

    private bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string> 
        { 
            "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", 
            "by", "from", "up", "about", "into", "through", "during", "before", 
            "after", "above", "below", "between", "among", "within", "without",
            "this", "that", "these", "those", "what", "which", "who", "when", 
            "where", "why", "how", "can", "could", "should", "would", "will",
            "have", "has", "had", "is", "are", "was", "were", "be", "been", "being"
        };
        
        return stopWords.Contains(word);
    }

    private List<string> SplitIntoSentences(string text)
    {
        var sentences = Regex.Split(text, @"[.!?]+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();
        
        return sentences;
    }

    private string TruncateToLength(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        
        var truncated = text.Substring(0, maxLength);
        var lastSpace = truncated.LastIndexOf(' ');
        
        return lastSpace > 0 ? truncated.Substring(0, lastSpace) + "..." : truncated + "...";
    }

    private double CalculateTextRelevance(string text, List<string> queryTerms, List<string> answerTerms)
    {
        var textLower = text.ToLowerInvariant();
        var queryMatches = queryTerms.Count(term => textLower.Contains(term));
        var answerMatches = answerTerms.Count(term => textLower.Contains(term));
        
        var queryScore = queryTerms.Count > 0 ? (double)queryMatches / queryTerms.Count : 0;
        var answerScore = answerTerms.Count > 0 ? (double)answerMatches / answerTerms.Count : 0;
        
        return (queryScore * 0.6) + (answerScore * 0.4);
    }

    private double CalculateRelevanceScore(string excerpt, string query, string answer)
    {
        var queryTerms = ExtractKeyTerms(query);
        var answerTerms = ExtractKeyTerms(answer);
        
        return CalculateTextRelevance(excerpt, queryTerms, answerTerms);
    }

    private string ExtractContext(string content, int position, int contextLength, bool isAfter)
    {
        if (isAfter)
        {
            var endPos = Math.Min(position + contextLength, content.Length);
            var context = content.Substring(position, endPos - position);
            return context.Trim();
        }
        else
        {
            var startPos = Math.Max(position - contextLength, 0);
            var context = content.Substring(startPos, position - startPos);
            return context.Trim();
        }
    }
}