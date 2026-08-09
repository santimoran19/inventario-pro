using InventarioPro.Api.Common;
using InventarioPro.Api.Data;
using InventarioPro.Api.Domain.Entities;
using InventarioPro.Api.Domain.Enums;
using InventarioPro.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace InventarioPro.Api.Services;

public interface IStockService
{
    Task<StockMovementDto> RegisterAsync(StockMovementCreateDto dto, CancellationToken ct = default);
    Task<PagedResult<StockMovementDto>> SearchAsync(StockMovementQuery query, CancellationToken ct = default);
}

/// <summary>
/// Único componente autorizado a modificar el stock de un producto.
///
/// Cada operación es atómica: o se guardan juntos el movimiento y el nuevo stock,
/// o no se guarda nada. Sin esto, un error a mitad de camino dejaría el stock
/// modificado sin el movimiento que lo justifique.
/// </summary>
public class StockService(AppDbContext db, ILogger<StockService> logger) : IStockService
{
    private const int MaxRetries = 3;

    public async Task<StockMovementDto> RegisterAsync(
        StockMovementCreateDto dto, CancellationToken ct = default)
    {
        if (dto.Quantity < 0)
            throw new DomainException("La cantidad no puede ser negativa.");

        if (dto.Type != MovementType.Adjustment && dto.Quantity == 0)
            throw new DomainException("La cantidad debe ser mayor a cero.");

        // Reintentos ante conflicto de concurrencia: si dos operarios descuentan
        // stock del mismo producto a la vez, una de las dos reintenta con datos frescos
        // en lugar de fallar o —peor— pisar el valor de la otra.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await RegisterCoreAsync(dto, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRetries)
            {
                logger.LogWarning(
                    "Conflicto de concurrencia en producto {ProductId}, reintento {Attempt}/{Max}",
                    dto.ProductId, attempt, MaxRetries);

                // Se descarta el estado en memoria para releer desde la base.
                db.ChangeTracker.Clear();
                await Task.Delay(25 * attempt, ct);
            }
        }
    }

    private async Task<StockMovementDto> RegisterCoreAsync(
        StockMovementCreateDto dto, CancellationToken ct)
    {
        // EnableRetryOnFailure obliga a envolver las transacciones explícitas en la
        // execution strategy: si no, EF no sabe cómo reintentar la operación completa.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
            await RegisterInTransactionAsync(dto, ct));
    }

    private async Task<StockMovementDto> RegisterInTransactionAsync(
        StockMovementCreateDto dto, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var product = await db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == dto.ProductId, ct)
            ?? throw new NotFoundException("Producto", dto.ProductId);

        if (!product.IsActive)
            throw new ConflictException(
                $"El producto '{product.Name}' está inactivo y no admite movimientos de stock.");

        var quantity = dto.Quantity;

        switch (dto.Type)
        {
            case MovementType.In:
                if (!product.TryApplyDelta(quantity))
                    throw new ConflictException("El movimiento dejaría el stock en un valor inválido.");
                break;

            case MovementType.Out:
                if (!product.TryApplyDelta(-quantity))
                    throw new ConflictException(
                        $"Stock insuficiente para '{product.Name}': hay {product.Stock} unidades y se intentan retirar {quantity}.");
                break;

            case MovementType.Adjustment:
                // En un ajuste, Quantity es el stock final contado físicamente.
                // Se registra la diferencia para que el historial siga cuadrando.
                var difference = quantity - product.Stock;
                product.SetStock(quantity);
                quantity = Math.Abs(difference);
                break;

            default:
                throw new DomainException("Tipo de movimiento no reconocido.");
        }

        var movement = new StockMovement
        {
            ProductId = product.Id,
            Type = dto.Type,
            Quantity = quantity,
            StockAfter = product.Stock,
            Reason = dto.Reason?.Trim(),
            Reference = dto.Reference?.Trim()
        };

        db.StockMovements.Add(movement);

        // Un solo SaveChanges para producto y movimiento: si falla la validación de
        // concurrencia sobre el producto, tampoco se inserta el movimiento.
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new StockMovementDto(
            movement.Id,
            product.Id,
            product.Sku,
            product.Name,
            movement.Type,
            movement.Quantity,
            movement.StockAfter,
            movement.Reason,
            movement.Reference,
            movement.CreatedAt,
            movement.CreatedBy);
    }

    public async Task<PagedResult<StockMovementDto>> SearchAsync(
        StockMovementQuery query, CancellationToken ct = default)
    {
        var q = db.StockMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .AsQueryable();

        if (query.ProductId is { } productId)
            q = q.Where(m => m.ProductId == productId);

        if (query.Type is { } type)
            q = q.Where(m => m.Type == type);

        if (query.From is { } from)
            q = q.Where(m => m.CreatedAt >= from);

        if (query.To is { } to)
            q = q.Where(m => m.CreatedAt <= to);

        var total = await q.CountAsync(ct);

        var entities = await q
            .OrderByDescending(m => m.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = entities.Select(m => new StockMovementDto(
            m.Id,
            m.ProductId,
            m.Product?.Sku ?? string.Empty,
            m.Product?.Name ?? string.Empty,
            m.Type,
            m.Quantity,
            m.StockAfter,
            m.Reason,
            m.Reference,
            m.CreatedAt,
            m.CreatedBy)).ToList();

        return new PagedResult<StockMovementDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = total
        };
    }
}
