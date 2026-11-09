namespace AquaTrack.Domain.Common;

/// <summary>
/// Base for every table. Audit fields are filled automatically in AppDbContext.SaveChanges.
/// Author: Attique
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Soft delete flag (SRS SYS-03). Rows are never physically removed by plant users.</summary>
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Base for every table that belongs to one water plant (tenant).
/// A global query filter in AppDbContext limits all queries to the logged-in user's PlantId (SRS AUTH-04).
/// </summary>
public abstract class TenantEntity : BaseEntity
{
    public int PlantId { get; set; }
}
