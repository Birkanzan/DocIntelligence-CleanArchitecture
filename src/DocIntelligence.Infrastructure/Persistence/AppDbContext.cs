using Microsoft.EntityFrameworkCore;
using DocIntelligence.Domain.Entities;

namespace DocIntelligence.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext. Infrastructure katmanına aittir,
/// Domain veya Application bu sınıfı bilmez.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tüm entity konfigürasyonlarını bu assembly'den otomatik yükle
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
