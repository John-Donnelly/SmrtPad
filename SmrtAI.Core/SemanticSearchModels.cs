namespace SmrtAI.Core;

/// <summary>Represents a semantic-search document snapshot supplied by the host app to the sidebar.</summary>
public sealed record SemanticSearchDocument(int TabId, string TabName, string DocumentText);

/// <summary>Represents a semantic-search result returned across the AI dispatcher boundary.</summary>
public sealed record SemanticSearchResult(int TabId, string ChunkText, float Score);
