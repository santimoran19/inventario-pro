using InventarioPro.Api.Common;
using InventarioPro.Api.Data;
using InventarioPro.Api.Domain.Entities;
using InventarioPro.Api.Domain.Enums;
using InventarioPro.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace InventarioPro.Api.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> SearchAsync(ProductQuery query, CancellationToken ct = default);
    Task<ProductDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(ProductCreateDto dto, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(int id, ProductUpdateDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class ProductService(AppDbContext db) : IProductService
{
    public async Task<PagedResult<ProductDto>> SearchAsync(
        ProductQuery query, CancellationToken ct = default)
    {
        // AsNoTracking: es una consulta de solo lectura, no hace falta
        // que EF mantenga el change tracker de cada fila.
        var q = db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();

            // EF.Functions.ILike se traduce a ILIKE de PostgreSQL con parámetros.
            // Al ser parametrizado no hay riesgo de inyección SQL.
            var pattern = $"%{term}%";

            q = q.Where(p =>
                EF.Functions.ILike(p.Sku, pattern) ||
                EF.Functions.ILike(p.Name, pattern) ||
                (p.Description != null && EF.Functions.ILike(p.Description, pattern)));
        }

        if (query.CategoryId is { } categoryId)
            q = q.Where(p => p.CategoryId == categoryId);

        if (query.SupplierId is { } supplierId)
            q = q.Where(p => p.SupplierId == supplierId);

        if (query.IsActive is { } isActive)
            q = q.Where(p => p.IsActive == isActive);

        if (query.LowStockOnly == true)
            q = q.Where(p => p.Stock <= p.MinStock);

        if (query.MinPrice is { } minPrice)
            q = q.Where(p => p.Price >= minPrice);

        if (query.MaxPrice is { } maxPrice)
            q = q.Where(p => p.Price <= maxPrice);

        q = ApplySort(q, query.SortBy, query.Desc);

        var total = await q.CountAsync(ct);

        // Se materializa primero y se mapea en memoria: EF no puede traducir
        // una llamada a método estático dentro de un Select a SQL.
        var entities = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = entities.Select(Map).ToList();

        return new PagedResult<ProductDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = total
        };
    }

    /// <summary>
    /// Ordenamiento por whitelist. Nunca se concatena el valor recibido dentro de
    /// una consulta: si el campo no está en la lista, se cae al orden por defecto.
    /// </summary>
    private static IQueryable<Product> ApplySort(IQueryable<Product> q, string? sortBy, bool desc)
        => (sortBy?.ToLowerInvariant()) switch
        {
            "sku"       => desc ? q.OrderByDescending(p => p.Sku)       : q.OrderBy(p => p.Sku),
            "price"     => desc ? q.OrderByDescending(p => p.Price)     : q.OrderBy(p => p.Price),
            "stock"     => desc ? q.OrderByDescending(p => p.Stock)     : q.OrderBy(p => p.Stock),
            "createdat" => desc ? q.OrderByDescending(p => p.CreatedAt) : q.OrderBy(p => p.CreatedAt),
            _           => desc ? q.OrderByDescending(p => p.Name)      : q.OrderBy(p => p.Name)
        };

    public async Task<ProductDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Producto", id);

        return Map(product);
    }

    public async Task<ProductDto> CreateAsync(ProductCreateDto dto, CancellationToken ct = default)
    {
        var sku = dto.Sku.Trim();

        if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
            throw new ConflictException($"Ya existe un producto con el SKU '{sku}'.");

        if (!await db.Categories.AnyAsync(c => c.Id == dto.CategoryId, ct))
            throw new NotFoundException("Categoría", dto.CategoryId);

        if (dto.SupplierId is { } supplierId &&
            !await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
            throw new NotFoundException("Proveedor", supplierId);

        var product = new Product
        {
            Sku = sku,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Price = dto.Price,
            Cost = dto.Cost,
            MinStock = dto.MinStock,
            CategoryId = dto.CategoryId,
            SupplierId = dto.SupplierId
        };

        // El stock inicial entra como movimiento, no como asignación directa:
        // así el historial explica de dónde salió cada unidad desde el día uno.
        if (dto.InitialStock > 0)
            product.TryApplyDelta(dto.InitialStock);

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        if (dto.InitialStock > 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                Type = MovementType.In,
                Quantity = dto.InitialStock,
                StockAfter = product.Stock,
                Reason = "Stock inicial de alta"
            });

            await db.SaveChangesAsync(ct);
        }

        return await GetByIdAsync(product.Id, ct);
    }

    public async Task<ProductDto> UpdateAsync(int id, ProductUpdateDto dto, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Producto", id);

        var sku = dto.Sku.Trim();

        if (!string.Equals(product.Sku, sku, StringComparison.OrdinalIgnoreCase) &&
            await db.Products.AnyAsync(p => p.Sku == sku && p.Id != id, ct))
            throw new ConflictException($"Ya existe otro producto con el SKU '{sku}'.");

        if (!await db.Categories.AnyAsync(c => c.Id == dto.CategoryId, ct))
            throw new NotFoundException("Categoría", dto.CategoryId);

        if (dto.SupplierId is { } supplierId &&
            !await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
            throw new NotFoundException("Proveedor", supplierId);

        product.Sku = sku;
        product.Name = dto.Name.Trim();
        product.Description = dto.Description?.Trim();
        product.Price = dto.Price;
        product.Cost = dto.Cost;
        product.MinStock = dto.MinStock;
        product.CategoryId = dto.CategoryId;
        product.SupplierId = dto.SupplierId;
        product.IsActive = dto.IsActive;
        // Stock queda deliberadamente afuera: solo se cambia vía movimientos.

        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Producto", id);

        if (product.Stock > 0)
            throw new ConflictException(
                $"No se puede dar de baja '{product.Name}' porque todavía tiene {product.Stock} unidades en stock.");

        // SaveChanges lo convierte en borrado lógico (ver AppDbContext.ApplyAuditInfo).
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
    }

    private static ProductDto Map(Product p) => new(
        p.Id,
        p.Sku,
        p.Name,
        p.Description,
        p.Price,
        p.Cost,
        p.Stock,
        p.MinStock,
        p.IsActive,
        p.Stock <= p.MinStock,
        p.CategoryId,
        p.Category?.Name ?? string.Empty,
        p.SupplierId,
        p.Supplier?.Name,
        p.CreatedAt,
        p.UpdatedAt);
}
