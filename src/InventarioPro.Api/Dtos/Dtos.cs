using InventarioPro.Api.Common;
using InventarioPro.Api.Domain.Enums;

namespace InventarioPro.Api.Dtos;

// ─────────────────────────────────────────────────────────────
//  Productos
//
//  Los DTOs de entrada no incluyen Stock ni Id: eso evita el
//  "mass assignment", donde un cliente manda campos de más y
//  termina modificando algo que no debería (stock, auditoría).
// ─────────────────────────────────────────────────────────────

public record ProductCreateDto(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    decimal Cost,
    int MinStock,
    int CategoryId,
    int? SupplierId,
    // Stock con el que se da de alta el producto. Genera un movimiento inicial.
    int InitialStock = 0);

public record ProductUpdateDto(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    decimal Cost,
    int MinStock,
    int CategoryId,
    int? SupplierId,
    bool IsActive);

public record ProductDto(
    int Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    decimal Cost,
    int Stock,
    int MinStock,
    bool IsActive,
    bool IsLowStock,
    int CategoryId,
    string CategoryName,
    int? SupplierId,
    string? SupplierName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Filtros de búsqueda de productos. Hereda la paginación con topes.</summary>
public class ProductQuery : PageQuery
{
    /// <summary>Busca en SKU, nombre y descripción.</summary>
    public string? Search { get; set; }

    public int? CategoryId { get; set; }
    public int? SupplierId { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>Solo productos por debajo del stock mínimo.</summary>
    public bool? LowStockOnly { get; set; }

    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    /// <summary>Campo de orden: name, sku, price, stock, createdAt.</summary>
    public string SortBy { get; set; } = "name";

    public bool Desc { get; set; }
}

// ─────────────────────────────────────────────────────────────
//  Categorías y proveedores
// ─────────────────────────────────────────────────────────────

public record CategoryCreateDto(string Name, string? Description);

public record CategoryDto(int Id, string Name, string? Description, int ProductCount);

public record SupplierCreateDto(string Name, string? Email, string? Phone, string? Address);

public record SupplierDto(int Id, string Name, string? Email, string? Phone, string? Address, int ProductCount);

// ─────────────────────────────────────────────────────────────
//  Movimientos de stock
// ─────────────────────────────────────────────────────────────

public record StockMovementCreateDto(
    int ProductId,
    MovementType Type,
    // Cantidad positiva. Para Adjustment es el stock final deseado.
    int Quantity,
    string? Reason,
    string? Reference);

public record StockMovementDto(
    long Id,
    int ProductId,
    string ProductSku,
    string ProductName,
    MovementType Type,
    int Quantity,
    int StockAfter,
    string? Reason,
    string? Reference,
    DateTimeOffset CreatedAt,
    string? CreatedBy);

public class StockMovementQuery : PageQuery
{
    public int? ProductId { get; set; }
    public MovementType? Type { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

// ─────────────────────────────────────────────────────────────
//  Reportes
// ─────────────────────────────────────────────────────────────

public record InventoryValuationDto(
    int TotalProducts,
    int TotalUnits,
    decimal TotalCostValue,
    decimal TotalSaleValue,
    decimal PotentialMargin,
    int LowStockCount,
    int OutOfStockCount);

public record CategoryValuationDto(
    int CategoryId,
    string CategoryName,
    int ProductCount,
    int TotalUnits,
    decimal TotalCostValue);

// ─────────────────────────────────────────────────────────────
//  Autenticación
// ─────────────────────────────────────────────────────────────

public record RegisterDto(string Email, string Password, string FullName);

public record LoginDto(string Email, string Password);

public record RefreshDto(string RefreshToken);

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string Email,
    string? FullName,
    IEnumerable<string> Roles);
