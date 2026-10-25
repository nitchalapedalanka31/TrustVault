using Microsoft.EntityFrameworkCore;
using TrustVault.Core.Enums;
using TrustVault.Core.Interfaces;
using TrustVault.Infrastructure.Data;

namespace TrustVault.Infrastructure.Services;

public class AiSuggestionService : IAiSuggestionService
{
    private readonly TrustVaultDbContext _dbContext;

    public AiSuggestionService(TrustVaultDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<RecipientSuggestion>> GetSuggestionsAsync(
        Guid documentId, 
        Guid requesterUserId, 
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .Include(d => d.Grants)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            return Enumerable.Empty<RecipientSuggestion>();
        }

        // Get currently granted user IDs and the owner ID to exclude them
        var existingRecipientIds = document.Grants
            .Where(g => g.IsActive && g.RevokedAtUtc == null)
            .Select(g => g.UserId)
            .ToHashSet();

        existingRecipientIds.Add(document.OwnerId);

        // Find active users who are candidates
        var candidateUsers = await _dbContext.Users
            .Where(u => u.IsActive && !existingRecipientIds.Contains(u.Id))
            .AsNoTracking()
            .Take(10)
            .ToListAsync(cancellationToken);

        var suggestions = new List<RecipientSuggestion>();

        foreach (var user in candidateUsers)
        {
            // Deterministic contextual ranking:
            // Check if user has collaborated on documents in the same category
            var sharedCategoryCount = await _dbContext.DocumentGrants
                .Include(g => g.Document)
                .CountAsync(g => g.UserId == user.Id && 
                                 g.IsActive && 
                                 g.Document != null && 
                                 g.Document.Category == document.Category, cancellationToken);

            double score = 0.5 + Math.Min(0.4, sharedCategoryCount * 0.1);
            string reason = sharedCategoryCount > 0
                ? $"Frequently accesses documents in '{document.Category}' ({sharedCategoryCount} active collaborations)."
                : "Active team member within the organization.";

            suggestions.Add(new RecipientSuggestion
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Reason = reason,
                ConfidenceScore = Math.Round(score, 2),
                SourceModel = "DeterministicContextRanker-v1.0"
            });
        }

        return suggestions.OrderByDescending(s => s.ConfidenceScore);
    }
}
