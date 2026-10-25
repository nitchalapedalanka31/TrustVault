namespace TrustVault.Core.Interfaces;

public class RecipientSuggestion
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string SourceModel { get; set; } = "DeterministicRulesEngine-v1";
}

public interface IAiSuggestionService
{
    Task<IEnumerable<RecipientSuggestion>> GetSuggestionsAsync(
        Guid documentId, 
        Guid requesterUserId, 
        CancellationToken cancellationToken = default);
}
