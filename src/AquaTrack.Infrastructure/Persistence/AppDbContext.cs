using System.Reflection;
using System.Text.Json;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Domain.Common;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AquaTrack.Infrastructure.Persistence;

/// <summary>
/// Single database for all plants.
/// - Tenant filter: plant users only ever see rows with their own PlantId (SRS AUTH-04, NFR-05)
/// - Soft delete: Remove() sets IsDeleted instead of deleting (SRS SYS-03)
/// - Audit fields + audit log for deliveries, bills, payments (SRS SYS-01)
/// Author: Attique
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ICurrentUserService _currentUser;

    private static readonly HashSet<Type> AuditedTypes = new()
    {
        typeof(Delivery), typeof(Bill), typeof(BillAdjustment),
        typeof(Payment), typeof(Deposit), typeof(BottleMovement)
    };

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    // Read by the global query filters on every query (EF re-evaluates them per query).
    public int? CurrentPlantId => _currentUser.PlantId;
    public bool IsSuperAdmin => _currentUser.IsSuperAdmin;

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<BottleStock> BottleStocks => Set<BottleStock>();
    public DbSet<BottleMovement> BottleMovements => Set<BottleMovement>();
    public DbSet<Deposit> Deposits => Set<Deposit>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillLine> BillLines => Set<BillLine>();
    public DbSet<BillAdjustment> BillAdjustments => Set<BillAdjustment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CashHandover> CashHandovers => Set<CashHandover>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All money in PKR with 2 decimals.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureEntities(builder);

        // 1) Tenant filter + FK to Plants for every TenantEntity.
        var applyFilter = typeof(AppDbContext)
            .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        var tenantTypes = builder.Model.GetEntityTypes()
            .Where(t => typeof(TenantEntity).IsAssignableFrom(t.ClrType))
            .ToList();

        foreach (var entityType in tenantTypes)
        {
            applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { builder });

            // SubscriptionPayment already has a Plant navigation; the rest get a shadow FK.
            if (entityType.ClrType != typeof(SubscriptionPayment))
            {
                builder.Entity(entityType.ClrType)
                    .HasOne(typeof(Plant))
                    .WithMany()
                    .HasForeignKey(nameof(TenantEntity.PlantId))
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }

        // 2) Non-tenant tables.
        builder.Entity<Plan>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<Plant>().HasQueryFilter(p => !p.IsDeleted && (IsSuperAdmin || p.Id == CurrentPlantId));
        builder.Entity<AuditLog>().HasQueryFilter(a => IsSuperAdmin || a.PlantId == CurrentPlantId);

        // 3) SQL Server does not allow multiple cascade paths; deletes are soft anyway.
        foreach (var fk in builder.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace?.StartsWith("AquaTrack.Domain") == true)
                     .SelectMany(t => t.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder) where TEntity : TenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e =>
            !e.IsDeleted && (IsSuperAdmin || e.PlantId == CurrentPlantId));
    }

    private static void ConfigureEntities(ModelBuilder b)
    {
        b.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150);
            e.HasIndex(x => x.PlantId);
        });

        b.Entity<Plan>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
        });

        b.Entity<Plant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.OwnerName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.City).HasMaxLength(80).IsRequired();
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.EasypaisaNumber).HasMaxLength(20);
            e.Property(x => x.JazzCashNumber).HasMaxLength(20);
            e.Property(x => x.EasypaisaTitle).HasMaxLength(150);
            e.Property(x => x.JazzCashTitle).HasMaxLength(150);
            e.Property(x => x.BankName).HasMaxLength(100);
            e.Property(x => x.BankAccountTitle).HasMaxLength(150);
            e.Property(x => x.BankAccountNumber).HasMaxLength(40);
        });

        b.Entity<SubscriptionPayment>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasOne(x => x.Plant).WithMany(p => p.SubscriptionPayments).HasForeignKey(x => x.PlantId);
        });

        b.Entity<Area>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.DeliveryBoyId).HasMaxLength(450);
        });

        b.Entity<Customer>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.WhatsApp).HasMaxLength(20);
            e.Property(x => x.Address).HasMaxLength(300).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ScheduleType).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.PlantId, x.AreaId, x.RouteOrder });
            e.HasIndex(x => new { x.PlantId, x.Phone });
        });

        b.Entity<BottleStock>(e =>
        {
            e.HasIndex(x => x.PlantId).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        b.Entity<BottleMovement>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasIndex(x => new { x.CustomerId, x.Date });
        });

        b.Entity<Deposit>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Delivery>(e =>
        {
            e.Property(x => x.DeliveryBoyId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(300);
            e.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20).HasDefaultValue(PaymentMethod.Cash);
            e.Property(x => x.TransactionRef).HasMaxLength(50);
            e.HasIndex(x => new { x.PlantId, x.Date });
            e.HasIndex(x => new { x.CustomerId, x.Date });
            e.HasIndex(x => new { x.DeliveryBoyId, x.Date });
        });

        b.Entity<Bill>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            // One bill per customer per month (soft-deleted bills don't count).
            e.HasIndex(x => new { x.CustomerId, x.Year, x.Month }).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasMany(x => x.Lines).WithOne(l => l.Bill).HasForeignKey(l => l.BillId);
            e.HasMany(x => x.AdjustmentItems).WithOne(a => a.Bill).HasForeignKey(a => a.BillId);
        });

        b.Entity<BillAdjustment>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        });

        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ReceivedById).HasMaxLength(450).IsRequired();
            e.Property(x => x.Note).HasMaxLength(300);
            e.Property(x => x.TransactionRef).HasMaxLength(50);
            e.Property(x => x.Verification).HasConversion<string>().HasMaxLength(20).HasDefaultValue(PaymentVerification.Verified);
            e.Property(x => x.VerifiedById).HasMaxLength(450);
            e.HasIndex(x => new { x.PlantId, x.Verification });
            e.HasIndex(x => new { x.CustomerId, x.Date });
        });

        b.Entity<CashHandover>(e =>
        {
            e.Property(x => x.DeliveryBoyId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasIndex(x => new { x.DeliveryBoyId, x.Date });
        });

        b.Entity<Expense>(e =>
        {
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(300);
        });

        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.Entity).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(50).IsRequired();
            e.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.PlantId, x.At });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyRules()
    {
        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>().ToList())
        {
            // Safety net: a plant user can never write another plant's row.
            if (entry.Entity is TenantEntity tenant && !IsSuperAdmin)
            {
                if (entry.State == EntityState.Added && tenant.PlantId == 0 && CurrentPlantId.HasValue)
                {
                    tenant.PlantId = CurrentPlantId.Value;
                }

                if (entry.State != EntityState.Unchanged && entry.State != EntityState.Detached
                    && CurrentPlantId.HasValue && tenant.PlantId != CurrentPlantId.Value)
                {
                    throw new UnauthorizedAccessException(
                        $"Cannot change {entry.Entity.GetType().Name} of another plant.");
                }
            }

            if (AuditedTypes.Contains(entry.Entity.GetType())
                && entry.State is EntityState.Modified or EntityState.Deleted)
            {
                AuditLogs.Add(BuildAudit(entry, now, userId));
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }

    private AuditLog BuildAudit(EntityEntry<BaseEntity> entry, DateTime now, string? userId)
    {
        var isDelete = entry.State == EntityState.Deleted;

        var changed = entry.Properties
            .Where(p => isDelete || (p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)))
            .ToList();

        return new AuditLog
        {
            PlantId = (entry.Entity as TenantEntity)?.PlantId ?? CurrentPlantId,
            UserId = userId,
            Entity = entry.Entity.GetType().Name,
            EntityId = entry.Entity.Id.ToString(),
            Action = isDelete ? AuditAction.Deleted : AuditAction.Updated,
            OldValues = JsonSerializer.Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)),
            NewValues = isDelete ? null : JsonSerializer.Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)),
            At = now
        };
    }
}
