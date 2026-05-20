using Microsoft.EntityFrameworkCore;
using DocIntelligence.Domain.Entities;
using DocIntelligence.Domain.Enums;
using DocIntelligence.Domain.Interfaces;
using DocIntelligence.Infrastructure.Persistence;

namespace DocIntelligence.Infrastructure.Repositories;

/// <summary>
/// EF Core tabanlı Document repository implementasyonu.
/// Domain sadece IDocumentRepository interface'ini görür.
/// </summary>
public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _context;

    public DocumentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Documents
            .Include(d => d.ProcessingJobs)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IEnumerable<Document>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Documents
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<Document>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => await _context.Documents
            .Where(d => d.UploadedByUserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<Document>> GetByStatusAsync(
        DocumentStatus status,
        CancellationToken cancellationToken = default)
        => await _context.Documents
            .Where(d => d.Status == status)
            .ToListAsync(cancellationToken);

    public async Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        await _context.Documents.AddAsync(document, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        _context.Documents.Update(document);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents.FindAsync([id], cancellationToken);
        if (document is not null)
        {
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Documents.AnyAsync(d => d.Id == id, cancellationToken);
}
