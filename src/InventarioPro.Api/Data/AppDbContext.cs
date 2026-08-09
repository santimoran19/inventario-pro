using System.Security.Claims;
using InventarioPro.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventarioPro.Api.Data;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IHttpContextAccessor? httpContextAccessor = null)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>(e =>
        {
            e.Property(p => p.Sku).IsRequired().HasMaxLength(40);
            e.Property(p => p.Name).IsRequired().HasMaxLength(150);
            e.Property(p => p.Description).HasMaxLength(1000);

            // Dinero siempre decimal con precisión explícita: con double aparecen
            // errores de redondeo al sumar precios.
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.Cost).HasPrecision(18, 2);

            // Stock tiene setter privado: EF lo escribe por la propiedad, y el
            // resto del código solo puede cambiarlo vía TryApplyDelta/SetStock.
            e.Property(p => p.Stock).IsRequired();

            // xmin es la columna de sistema de PostgreSQL para concurrencia optimista.
            e.Property(p => p.Version).IsRowVersion().HasColumnName("xmin");

            // SKU único solo entre productos vivos: permite reusar el código
            // de un producto dado de baja.
            e.HasIndex(p => p.Sku)
             .IsUnique()
             .HasFilter("\"IsDeleted\" = false");

            e.HasIndex(p => p.Name);
            e.HasIndex(p => new { p.CategoryId, p.IsActive });

            e.HasOne(p => p.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(p => p.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Supplier)
             .WithMany(s => s.Products)
             .HasForeignKey(p => p.SupplierId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasQueryFilter(p => !p.IsDeleted);
        });

        builder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(80);
            e.HasIndex(c => c.Name).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasQueryFilter(c => !c.IsDeleted);
        });

        builder.Entity<Supplier>(e =>
        {
            e.Property(s => s.Name).IsRequired().HasMaxLength(120);
            e.HasIndex(s => s.Name);
            e.HasQueryFilter(s => !s.IsDeleted);
        });

        builder.Entity<StockMovement>(e =>
        {
            e.Property(m => m.Reason).HasMaxLength(300);
            e.Property(m => m.Reference).HasMaxLength(60);

            e.HasIndex(m => new { m.ProductId, m.CreatedAt });
            e.HasIndex(m => m.CreatedAt);

            // Los movimientos no se borran: son el historial de auditoría.
            e.HasOne(m => m.Product)
             .WithMany(p => p.Movements)
             .HasForeignKey(m => m.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.Property(t => t.TokenHash).IsRequired().HasMaxLength(88);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.UserId);

            e.HasOne(t => t.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    /// <summary>
    /// Completa CreatedAt/UpdatedAt y el usuario responsable automáticamente,
    /// para que ningún servicio se olvide de hacerlo.
    /// </summary>
    private void ApplyAuditInfo()
    {
        var userId = httpContextAccessor?.HttpContext?.User
            ?.FindFirstValue(ClaimTypes.NameIdentifier);

        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    // CreatedAt/CreatedBy son inmutables una vez escritos.
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Property(x => x.CreatedBy).IsModified = false;
                    break;

                // Un DELETE sobre una entidad auditable se convierte en borrado lógico.
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }
}
