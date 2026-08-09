using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using InventarioPro.Api.Domain.Enums;

namespace InventarioPro.Api.Domain.Entities;

/// <summary>Campos de auditoría comunes a todas las entidades persistidas.</summary>
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Id del usuario que creó el registro. Null para datos de seed.</summary>
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Borrado lógico: nunca se borra físicamente un producto con historial.</summary>
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class Category : AuditableEntity
{
    public int Id { get; set; }

    [MaxLength(80)]
    public required string Name { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Supplier : AuditableEntity
{
    public int Id { get; set; }

    [MaxLength(120)]
    public required string Name { get; set; }

    [MaxLength(120)]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Código interno del producto. Único entre los no eliminados.</summary>
    [MaxLength(40)]
    public required string Sku { get; set; }

    [MaxLength(150)]
    public required string Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Precio de venta. decimal(18,2): nunca double para dinero.</summary>
    public decimal Price { get; set; }

    /// <summary>Costo de compra, usado para valorizar el inventario.</summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// Stock actual. Solo lo modifica StockService dentro de una transacción,
    /// nunca se escribe directo desde un endpoint de producto.
    /// </summary>
    public int Stock { get; private set; }

    /// <summary>Umbral de reposición: por debajo de esto el producto aparece en alertas.</summary>
    public int MinStock { get; set; }

    public bool IsActive { get; set; } = true;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();

    /// <summary>
    /// Token de concurrencia optimista (xmin de PostgreSQL). Evita que dos operaciones
    /// simultáneas sobre el mismo producto pisen el stock una a la otra.
    /// </summary>
    public uint Version { get; set; }

    /// <summary>
    /// Único punto donde cambia el stock. Devuelve false si la operación
    /// dejaría el stock en negativo.
    /// </summary>
    public bool TryApplyDelta(int delta)
    {
        var result = Stock + delta;
        if (result < 0) return false;
        Stock = result;
        return true;
    }

    /// <summary>Fija el stock a un valor absoluto. Solo para ajustes de inventario.</summary>
    public void SetStock(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Stock = value;
    }
}

/// <summary>
/// Registro inmutable de cada cambio de stock. Es la fuente de verdad:
/// Product.Stock es un valor derivado que debe poder reconstruirse desde acá.
/// </summary>
public class StockMovement
{
    public long Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public MovementType Type { get; set; }

    /// <summary>Cantidad siempre positiva. El signo lo determina Type.</summary>
    public int Quantity { get; set; }

    /// <summary>Stock resultante después de aplicar el movimiento.</summary>
    public int StockAfter { get; set; }

    [MaxLength(300)]
    public string? Reason { get; set; }

    /// <summary>Remito, factura o documento de respaldo.</summary>
    [MaxLength(60)]
    public string? Reference { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? CreatedBy { get; set; }
}

public class ApplicationUser : IdentityUser
{
    [MaxLength(120)]
    public string? FullName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

/// <summary>
/// Refresh token con rotación. Se guarda el hash, nunca el token en claro:
/// si alguien lee la base no puede reutilizarlos.
/// </summary>
public class RefreshToken
{
    public long Id { get; set; }

    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>SHA-256 del token entregado al cliente.</summary>
    [MaxLength(88)]
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Hash del token que reemplazó a este, para detectar reuso de tokens robados.</summary>
    [MaxLength(88)]
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
