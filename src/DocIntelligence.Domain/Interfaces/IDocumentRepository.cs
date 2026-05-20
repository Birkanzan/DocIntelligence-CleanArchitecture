using DocIntelligence.Domain.Entities;

namespace DocIntelligence.Domain.Interfaces;

/// <summary>
/// Document repository sözleşmesi.
/// Infrastructure katmanı bunu EF Core ile implemente eder.
/// </summary>
public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Document>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Document>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default);
    Task UpdateAsync(Document document, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirtilen durumdaki belgeleri döndürür (Worker için kullanılır).
    /// </summary>
    Task<IEnumerable<Document>> GetByStatusAsync(
        Domain.Enums.DocumentStatus status,
        CancellationToken cancellationToken = default);
}
