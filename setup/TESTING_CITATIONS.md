# Testing Citations Feature

## Overview
The Partnership Agent now includes comprehensive citation functionality that provides detailed references to source documents when answering questions.

## Enhanced Setup

### 1. Sample Documents
The setup now includes 8 comprehensive documents with rich content:

1. **Partnership Agreement Template** (`doc1`) - Templates category
   - Partnership formation requirements
   - Revenue sharing structure (Tier 1: 30-35%, Tier 2: 20-25%, Tier 3: 10-15%)
   - Minimum contribution requirements
   - Performance metrics

2. **Revenue Sharing Guidelines** (`doc2`) - Guidelines category
   - Detailed calculation methods
   - Payment terms (monthly/quarterly)
   - Minimum payment threshold ($100)
   - Tax withholding requirements

3. **Partnership Compliance Requirements** (`doc3`) - Policies category
   - Documentation standards
   - Financial reporting requirements
   - Partner verification processes
   - Audit requirements

4. **Standard Partnership Contract** (`doc4`) - Contracts category
   - Intellectual property rights
   - Liability distribution
   - Termination procedures (90-day notice)
   - Dispute resolution

5. **Partner Onboarding Process** (`doc5`) - Guidelines category
   - Initial assessment criteria
   - Documentation and setup requirements
   - Training and certification (85% minimum score)
   - System integration steps

6. **Dispute Resolution Framework** (`doc6`) - Policies category
   - Direct negotiation process
   - Mediation procedures
   - Arbitration process
   - Escalation triggers

7. **Performance Metrics and KPIs** (`doc7`) - Guidelines category
   - Revenue performance metrics
   - Operational excellence standards (4.5/5.0 customer satisfaction)
   - Strategic alignment measures
   - Review schedules

8. **Data Security and Privacy Policy** (`doc8`) - Policies category
   - Data classification levels
   - Security controls (AES-256 encryption, TLS 1.3)
   - Access controls and auditing
   - Privacy compliance (GDPR, CCPA)

### 2. Updated Elasticsearch Mapping
Enhanced mapping includes:
- Support for `sourcePath`, `lastModified`, and `metadata` fields
- Improved text analysis with English stopwords
- Keyword fields for exact matching

## Testing the Citations Feature

### Quick Start
1. Run the setup script:
   ```bash
   ./setup/start-partnership-agent.sh
   ```

2. Use the dedicated citation test script:
   ```bash
   ./setup/test-citations.sh
   ```

### Manual Testing
Start the console app and try these prompts that should generate rich citations:

#### Revenue-focused Questions:
- "What are the revenue sharing percentages for different partner tiers?"
- "What is the minimum payment threshold for revenue sharing?"
- "How often are revenue sharing payments made?"

#### Compliance Questions:
- "What compliance documentation and audit requirements must partners follow?"
- "What is the minimum credit score requirement for partner verification?"
- "How often are compliance reviews conducted?"

#### Performance Questions:
- "What are the minimum performance standards partners must maintain?"
- "What customer satisfaction score is required?"
- "What is the minimum score required for platform certification?"

#### Process Questions:
- "What is the termination notice period for partnerships?"
- "How many hours of ongoing education are required annually?"
- "What encryption standards are required for data protection?"

## Expected Citation Output

When the citation feature is working correctly, responses should include:

```json
{
  "answer": "Based on the relevant documents...",
  "confidence_level": "high",
  "source_documents": ["Revenue Sharing Guidelines", "Partnership Agreement Template"],
  "citations": [
    {
      "document_id": "doc2",
      "document_title": "Revenue Sharing Guidelines",
      "category": "guidelines",
      "excerpt": "Tier 1 (Strategic Partners): 30% of net revenue, paid monthly",
      "start_position": 1450,
      "end_position": 1502,
      "relevance_score": 0.92,
      "context_before": "Partner shares are distributed according to contribution tiers:",
      "context_after": "- Tier 2 (Operational Partners): 20% of net revenue"
    }
  ],
  "has_complete_answer": true,
  "follow_up_suggestions": [...]
}
```

## Key Citation Features

1. **Document Identification**: Each citation includes document ID, title, and category
2. **Precise Text Excerpts**: Exact text passages that support the answer
3. **Position Tracking**: Character start/end positions for precise referencing
4. **Relevance Scoring**: Confidence scores based on query/answer term matching
5. **Context Awareness**: Before/after text for better understanding
6. **Multiple Sources**: Citations from multiple documents when relevant

## Troubleshooting

### Common Issues:
1. **No Citations Generated**: Ensure Elasticsearch is running and documents are indexed
2. **Empty Response**: Check that the FAQAgent is using the CitationService
3. **API Errors**: Verify user secrets are set and services are registered

### Verification Commands:
```bash
# Check Elasticsearch status
curl -s "http://localhost:9200/_cluster/health"

# Verify document count (should be 8)
curl -s "http://localhost:9200/partnership-documents/_count"

# Test direct search
curl -s "http://localhost:9200/partnership-documents/_search?q=revenue"
```

## Next Steps

The citation feature is now fully functional and integrated into the Partnership Agent. Users can:

1. **Ask detailed questions** about partnership agreements, revenue sharing, compliance, etc.
2. **Receive precise citations** showing exactly where information comes from
3. **Trust the responses** with confidence scores and source verification
4. **Follow up** with related questions based on suggestions

The enhanced document set provides comprehensive coverage of partnership scenarios and will generate meaningful citations for most business partnership questions.