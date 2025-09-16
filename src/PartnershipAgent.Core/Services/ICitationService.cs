using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services;

public interface ICitationService
{
    Task<List<DocumentCitation>> ExtractCitationsAsync(
        string query, 
        string generatedAnswer, 
        List<DocumentResult> sourceDocuments, 
        CancellationToken cancellationToken = default);

    DocumentCitation CreateCitation(
        DocumentResult document,
        string excerpt,
        int startPosition,
        int endPosition,
        double relevanceScore);

    List<string> FindRelevantExcerpts(
        string content,
        string query,
        string answer,
        int maxExcerpts = 3,
        int excerptLength = 200);
}