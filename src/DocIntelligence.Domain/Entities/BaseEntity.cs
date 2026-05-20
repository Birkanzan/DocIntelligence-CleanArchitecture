namespace DocIntelligence.Domain.Entities;

/// <summary>
/// Tüm entity'lerin türediği temel sınıf.
/// Audit alanlarını (oluşturma/güncelleme tarihi) içerir.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    public void SetUpdatedAt() => UpdatedAt = DateTime.UtcNow;
}
