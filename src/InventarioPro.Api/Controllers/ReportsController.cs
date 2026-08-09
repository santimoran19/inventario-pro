using InventarioPro.Api.Data;
using InventarioPro.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventarioPro.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
[Produces("application/json")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    /// <summary>Valorización general del inventario: unidades, costo, venta y margen potencial.</summary>
    [HttpGet("valuation")]
    [ProducesResponseType(typeof(InventoryValuationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryValuationDto>> Valuation(CancellationToken ct)
    {
        // Una sola pasada por la tabla en lugar de seis consultas separadas.
        var data = await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalProducts = g.Count(),
                TotalUnits = g.Sum(p => p.Stock),
                TotalCost = g.Sum(p => p.Cost * p.Stock),
                TotalSale = g.Sum(p => p.Price * p.Stock),
                LowStock = g.Count(p => p.Stock <= p.MinStock && p.Stock > 0),
                OutOfStock = g.Count(p => p.Stock == 0)
            })
            .FirstOrDefaultAsync(ct);

        if (data is null)
            return Ok(new InventoryValuationDto(0, 0, 0m, 0m, 0m, 0, 0));

        return Ok(new InventoryValuationDto(
            data.TotalProducts,
            data.TotalUnits,
            data.TotalCost,
            data.TotalSale,
            data.TotalSale - data.TotalCost,
            data.LowStock,
            data.OutOfStock));
    }

    /// <summary>Valorización desagregada por categoría.</summary>
    [HttpGet("valuation/by-category")]
    [ProducesResponseType(typeof(IEnumerable<CategoryValuationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryValuationDto>>> ValuationByCategory(
        CancellationToken ct)
    {
        var result = await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .GroupBy(p => new { p.CategoryId, CategoryName = p.Category!.Name })
            .Select(g => new CategoryValuationDto(
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Count(),
                g.Sum(p => p.Stock),
                g.Sum(p => p.Cost * p.Stock)))
            .OrderByDescending(x => x.TotalCostValue)
            .ToListAsync(ct);

        return Ok(result);
    }

    /// <summary>Productos que alcanzaron o perforaron su stock mínimo.</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> LowStock(CancellationToken ct)
    {
        var entities = await db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.IsActive && p.Stock <= p.MinStock)
            .OrderBy(p => p.Stock)
            .ToListAsync(ct);

        var items = entities.Select(p => new ProductDto(
            p.Id, p.Sku, p.Name, p.Description, p.Price, p.Cost, p.Stock, p.MinStock,
            p.IsActive, true, p.CategoryId, p.Category?.Name ?? string.Empty,
            p.SupplierId, p.Supplier?.Name, p.CreatedAt, p.UpdatedAt));

        return Ok(items);
    }
}
