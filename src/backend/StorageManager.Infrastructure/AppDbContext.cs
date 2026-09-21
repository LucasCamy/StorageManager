using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StorageManager.Core;

namespace StorageManager.Infrastructure;

public sealed class AppUser : IdentityUser<Guid>
{
    public string Name { get; set; } = "";
    public Guid OrganizationId { get; set; }
    public string Role { get; set; } = Roles.Viewer;
    public bool IsActive { get; set; } = true;
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Installation> Installations => Set<Installation>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockEntry> StockEntries => Set<StockEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Organization>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<Installation>().ToTable(t => t.HasCheckConstraint("CK_Installation_Singleton", "\"Id\" = 1"));
        b.Entity<Installation>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<AppUser>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.Role).HasMaxLength(20);
            e.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.ToTable("AspNetUsers", t => t.HasCheckConstraint("CK_User_Role", "\"Role\" IN ('Admin', 'Operator', 'Viewer')"));
        });
        b.Entity<Location>(e =>
        {
            e.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Code).HasMaxLength(40);
            e.Property(x => x.Type).HasMaxLength(60);
            e.Property(x => x.Path).HasMaxLength(2000);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Location>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ParentId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Product>(e =>
        {
            e.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(60);
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.Unit).HasMaxLength(20);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.MinimumStock).HasPrecision(20, 6);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Product_Minimum", "\"MinimumStock\" >= 0");
                t.HasCheckConstraint("CK_Product_Scale", "\"QuantityScale\" BETWEEN 0 AND 6");
                t.HasCheckConstraint("CK_Product_Kind", "\"Kind\" IN ('Consumable', 'Returnable')");
            });
        });
        b.Entity<StockBalance>(e =>
        {
            e.HasKey(x => new { x.OrganizationId, x.ProductId, x.LocationId });
            e.Property(x => x.Quantity).HasPrecision(20, 6);
            e.HasOne<Product>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ProductId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Location>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LocationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.OrganizationId, x.LocationId });
            e.ToTable(t => t.HasCheckConstraint("CK_Stock_NonNegative", "\"Quantity\" >= 0"));
        });
        b.Entity<StockMovement>(e =>
        {
            e.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            e.Property(x => x.Sequence).UseIdentityByDefaultColumn();
            e.HasIndex(x => new { x.OrganizationId, x.Sequence }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt, x.Id });
            e.Property(x => x.Quantity).HasPrecision(20, 6);
            e.Property(x => x.Type).HasMaxLength(20);
            e.Property(x => x.ProductCode).HasMaxLength(60);
            e.Property(x => x.ProductName).HasMaxLength(160);
            e.Property(x => x.LocationPath).HasMaxLength(2000);
            e.Property(x => x.Unit).HasMaxLength(20);
            e.Property(x => x.Reference).HasMaxLength(120);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.Recipient).HasMaxLength(160);
            e.Property(x => x.ActorName).HasMaxLength(160);
            e.HasOne<Product>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ProductId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Location>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LocationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Movement_Quantity", "\"Quantity\" > 0");
                t.HasCheckConstraint("CK_Movement_Type", "\"Type\" IN ('Receipt', 'Consumption')");
            });
        });
        b.Entity<StockEntry>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(20, 6);
            e.Property(x => x.Account).HasMaxLength(20);
            e.HasIndex(x => new { x.OrganizationId, x.MovementId, x.Account }).IsUnique();
            e.HasOne<StockMovement>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.MovementId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ProductId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Location>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LocationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Entry_Account", "(\"Account\" = 'Warehouse' AND \"LocationId\" IS NOT NULL) OR (\"Account\" = 'External' AND \"LocationId\" IS NULL)"));
        });
        b.Entity<IdempotencyRecord>(e =>
        {
            e.HasKey(x => new { x.OrganizationId, x.ActorId, x.Key });
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.RequestHash).HasMaxLength(64);
            e.Property(x => x.ResultJson).HasColumnType("jsonb");
            e.HasOne<StockMovement>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.MovementId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AuditEvent>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(80);
            e.Property(x => x.SubjectId).HasMaxLength(80);
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries().Any(e => (e.Entity is StockMovement or StockEntry or AuditEvent or IdempotencyRecord) && e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("O histórico confirmado é imutável.");
        return base.SaveChangesAsync(cancellationToken);
    }
}
