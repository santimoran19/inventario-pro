using InventarioPro.Api.Common;
using InventarioPro.Api.Dtos;
using InventarioPro.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventarioPro.Api.Controllers;

[ApiController]
[Route("api/stock")]
[Authorize]
[Produces("application/json")]
public class StockController(IStockService stock) : ControllerBase
{
    /// <summary>Historial de movimientos con filtros por producto, tipo y rango de fechas.</summary>
    [HttpGet("movements")]
    [ProducesResponseType(typeof(PagedResult<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockMovementDto>>> Movements(
        [FromQuery] StockMovementQuery query, CancellationToken ct)
        => Ok(await stock.SearchAsync(query, ct));

    /// <summary>
    /// Registra un movimiento de stock (entrada, salida o ajuste).
    /// Es la única vía para modificar el stock de un producto.
    /// </summary>
    [HttpPost("movements")]
    [Authorize(Roles = Roles.CanWrite)]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementDto>> Register(
        [FromBody] StockMovementCreateDto dto, CancellationToken ct)
    {
        var movement = await stock.RegisterAsync(dto, ct);
        return CreatedAtAction(nameof(Movements), new { productId = movement.ProductId }, movement);
    }
}
