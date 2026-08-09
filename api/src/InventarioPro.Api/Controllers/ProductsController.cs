using InventarioPro.Api.Common;
using InventarioPro.Api.Dtos;
using InventarioPro.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventarioPro.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
[Produces("application/json")]
public class ProductsController(IProductService products) : ControllerBase
{
    /// <summary>Lista productos con filtros, orden y paginación.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> Search(
        [FromQuery] ProductQuery query, CancellationToken ct)
        => Ok(await products.SearchAsync(query, ct));

    /// <summary>Obtiene un producto por su id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken ct)
        => Ok(await products.GetByIdAsync(id, ct));

    /// <summary>Crea un producto. Requiere rol Admin o Manager.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.CanWrite)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] ProductCreateDto dto, CancellationToken ct)
    {
        var created = await products.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Actualiza los datos de un producto. El stock no se toca acá.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.CanWrite)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Update(
        int id, [FromBody] ProductUpdateDto dto, CancellationToken ct)
        => Ok(await products.UpdateAsync(id, dto, ct));

    /// <summary>Da de baja un producto (borrado lógico). Solo Admin.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await products.DeleteAsync(id, ct);
        return NoContent();
    }
}
